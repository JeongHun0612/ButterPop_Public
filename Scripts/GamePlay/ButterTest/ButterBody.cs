using System;
using System.Collections.Generic;
using UnityEngine;

// 버터 스퀴시의 "물리 뼈대". 가로 막대를 길이 방향으로 N등분한 강체 세그먼트 체인.
// - 이웃 세그먼트는 HingeJoint2D(이음새 고정)로 연결 → 늘어나지 않고 회전(=구부러짐)만 한다.
// - FixedUpdate에서 수동 굽힘 스프링 토크로 직선으로 부드럽게 복원한다(소성 없음, 점탄성 느낌).
// - 시각(스퀴시)은 ButterSquishMesh가 이 세그먼트들에 스키닝해 입힌다. 물리는 여기서만 담당.
public class ButterBody : MonoBehaviour
{
    [Header("형태")]
    [Tooltip("막대 전체 길이 (가로, 월드 단위)")]
    [SerializeField] private float length = 2.4f;
    [Tooltip("막대 두께 (세로, 월드 단위). 세그먼트 원 콜라이더 지름이 된다.")]
    [SerializeField] private float thickness = 0.6f;
    [Tooltip("물리 세그먼트 수. 많을수록 휘는 곡선이 매끈(9~11 권장).")]
    [Range(2, 24)]
    [SerializeField] private int segmentCount = 11;

    [Header("질량 / 중력")]
    [SerializeField] private float totalMass = 1.2f;
    [SerializeField] private float gravityScale = 1.5f;

    [Header("굽힘 (구부러짐 · 조연)")]
    [Tooltip("굽힘 On/Off. 끄면 이음새를 잠가 뻣뻣한 직선 막대가 된다. (Play 중 실시간 토글 가능)")]
    [SerializeField] private bool bendEnabled = true;
    [Tooltip("직선으로 되돌리는 세기. 높을수록 막대 형태를 유지(살짝만 처짐), 낮을수록 축 늘어짐.")]
    [SerializeField] private float bendStiffness = 60f;
    [Tooltip("굽힘 감쇠. 높을수록 출렁임 없이 물컹하게 멈춘다(점탄성).")]
    [SerializeField] private float bendDamping = 8f;
    [Tooltip("이음새 하나가 꺾일 수 있는 최대 각(도). 접힘/뒤집힘 방지 하드캡.")]
    [SerializeField] private float maxBendAnglePerJoint = 18f;

    [Header("물성")]
    [Range(0f, 1f)]
    [SerializeField] private float bounciness = 0.1f;
    [Range(0f, 1f)]
    [SerializeField] private float friction = 0.6f;

    private readonly List<Rigidbody2D> _segments = new();
    private readonly List<Collider2D> _colliders = new();
    private readonly List<HingeJoint2D> _hinges = new();
    private float[] _restCenterX;   // 세그먼트 로컬 x (직선 배치 기준)
    private float _spacing;
    private float _radius;
    private bool _appliedBend;      // 현재 hinge에 반영된 굽힘 상태 (런타임 토글 감지용)

    public event Action<Vector2, Vector2, float> OnSegmentCollision;   // 접촉점, 법선, 충격 크기

    public int SegmentCount => segmentCount;
    public float Length => length;
    public float Thickness => thickness;
    public IReadOnlyList<Rigidbody2D> Segments => _segments;
    public IReadOnlyList<float> RestCenterX => _restCenterX;

    // 세그먼트 속도 중 최댓값 (안착 판정용: 하나라도 출렁이면 미안착).
    public float AggregateSpeed
    {
        get
        {
            float max = 0f;
            for (int i = 0; i < _segments.Count; i++)
                max = Mathf.Max(max, _segments[i].linearVelocity.magnitude);
            return max;
        }
    }

    public Bounds WorldBounds
    {
        get
        {
            if (_colliders.Count == 0) return new Bounds(transform.position, Vector3.zero);
            Bounds b = _colliders[0].bounds;
            for (int i = 1; i < _colliders.Count; i++)
                b.Encapsulate(_colliders[i].bounds);
            return b;
        }
    }

    private void Awake()
    {
        Build();
    }

    private void FixedUpdate()
    {
        if (bendEnabled != _appliedBend)
            ApplyBendState();

        if (bendEnabled)
            ApplyBendingSprings();
    }

    public void ResetLayout(Vector2 basePosition, float rotationDeg)
    {
        float rad = rotationDeg * Mathf.Deg2Rad;
        for (int i = 0; i < _segments.Count; i++)
        {
            var rb = _segments[i];
            rb.position = basePosition + Rotate(new Vector2(_restCenterX[i], 0f), rad);
            rb.rotation = rotationDeg;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    public void SetKinematic(bool kinematic)
    {
        var type = kinematic ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        for (int i = 0; i < _segments.Count; i++)
        {
            _segments[i].bodyType = type;
            if (kinematic)
            {
                _segments[i].linearVelocity = Vector2.zero;
                _segments[i].angularVelocity = 0f;
            }
        }
    }

    public void SetLinearVelocityAll(Vector2 velocity)
    {
        for (int i = 0; i < _segments.Count; i++)
            _segments[i].linearVelocity = velocity;
    }

    public void AddImpulseAll(Vector2 impulse, float randomTorque)
    {
        for (int i = 0; i < _segments.Count; i++)
        {
            _segments[i].AddForce(impulse, ForceMode2D.Impulse);
            if (randomTorque > 0f)
                _segments[i].AddTorque(UnityEngine.Random.Range(-randomTorque, randomTorque), ForceMode2D.Impulse);
        }
    }

    // 각 세그먼트를 mouse world delta만큼 강체 이동 (드래그용). Kinematic 상태에서 호출.
    public void MoveAll(Vector2 worldDelta)
    {
        for (int i = 0; i < _segments.Count; i++)
            _segments[i].MovePosition(_segments[i].position + worldDelta);
    }

    private void Build()
    {
        _radius = thickness * 0.5f;
        _spacing = segmentCount > 1 ? (length - thickness) / (segmentCount - 1) : 0f;
        float halfSpan = _spacing * (segmentCount - 1) * 0.5f;

        _restCenterX = new float[segmentCount];
        for (int i = 0; i < segmentCount; i++)
            _restCenterX[i] = -halfSpan + i * _spacing;

        var mat = new PhysicsMaterial2D("ButterSquishy") { bounciness = bounciness, friction = friction };
        float segMass = totalMass / segmentCount;

        for (int i = 0; i < segmentCount; i++)
            _segments.Add(MakeSegment(i, segMass, mat));

        // 이음새 hinge: 두 세그먼트를 중점에 핀으로 고정 → 회전만 허용.
        for (int i = 0; i < segmentCount - 1; i++)
            ConnectHinge(_segments[i], _segments[i + 1]);

        // 자기충돌 제거 (월드와의 충돌은 유지).
        for (int i = 0; i < _colliders.Count; i++)
            for (int j = i + 1; j < _colliders.Count; j++)
                Physics2D.IgnoreCollision(_colliders[i], _colliders[j]);

        ApplyBendState();
    }

    // 굽힘 On: 이음새를 ±maxBend 안에서 자유 회전 / Off: 각을 0으로 잠가 뻣뻣한 직선 막대.
    private void ApplyBendState()
    {
        float limit = bendEnabled ? maxBendAnglePerJoint : 0f;
        for (int i = 0; i < _hinges.Count; i++)
            _hinges[i].limits = new JointAngleLimits2D { min = -limit, max = limit };
        _appliedBend = bendEnabled;
    }

    private Rigidbody2D MakeSegment(int index, float mass, PhysicsMaterial2D mat)
    {
        var go = new GameObject($"Seg_{index}");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(_restCenterX[index], 0f, 0f);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.mass = mass;
        rb.gravityScale = gravityScale;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = _radius;
        col.sharedMaterial = mat;
        _colliders.Add(col);

        var relay = go.AddComponent<ButterSegmentRelay>();
        relay.Init(this);

        return rb;
    }

    private void ConnectHinge(Rigidbody2D a, Rigidbody2D b)
    {
        var hinge = a.gameObject.AddComponent<HingeJoint2D>();
        hinge.connectedBody = b;
        hinge.autoConfigureConnectedAnchor = false;
        hinge.anchor = new Vector2(_spacing * 0.5f, 0f);          // A 로컬: 이음새(중점) 방향
        hinge.connectedAnchor = new Vector2(-_spacing * 0.5f, 0f); // B 로컬: 이음새(중점) 방향
        hinge.enableCollision = false;
        hinge.useLimits = true;   // 실제 min/max는 ApplyBendState가 설정

        _hinges.Add(hinge);
    }

    // 이웃 세그먼트 상대각을 0(직선)으로 되돌리는 굽힘 스프링. 관성 정규화로 세그먼트 수 무관 일관.
    private void ApplyBendingSprings()
    {
        for (int i = 0; i < _segments.Count - 1; i++)
        {
            var a = _segments[i];
            var b = _segments[i + 1];

            float relDeg = Mathf.DeltaAngle(a.rotation, b.rotation);
            float relVelDeg = b.angularVelocity - a.angularVelocity;

            float t = -bendStiffness * (relDeg * Mathf.Deg2Rad) - bendDamping * (relVelDeg * Mathf.Deg2Rad);
            b.AddTorque(t * b.inertia);
            a.AddTorque(-t * a.inertia);
        }
    }

    internal void ReportCollision(Collision2D collision)
    {
        if (collision.contactCount == 0) return;

        var contact = collision.GetContact(0);
        OnSegmentCollision?.Invoke(contact.point, contact.normal, collision.relativeVelocity.magnitude);
    }

    private static Vector2 Rotate(Vector2 v, float rad)
    {
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
        return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
    }
}
