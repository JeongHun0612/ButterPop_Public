using System;
using System.Collections.Generic;
using UnityEngine;

// 스퀴시 오브젝트의 물리 뼈대(일반형). SquishShapeSO의 스파인을 리샘플링해 강체 세그먼트 체인을 만든다.
// - ButterBody를 임의 형태(곡선 스파인 · 가변 폭)로 일반화한 버전.
// - 굽힘 스프링 rest각 = 스파인의 원래 곡률 → 버터는 직선, 바나나는 초승달을 유지하며 그 주위로 말랑거린다.
// - 시각(스퀴시)은 SquishMesh가 이 뼈들에 스키닝해 입힌다.
public class SquishBody : MonoBehaviour
{
    [Header("형태 정의")]
    [SerializeField] private SquishShapeSO shape;

    [Header("굽힘 (런타임 토글)")]
    [Tooltip("Awake에서 Shape.bendEnabled로 초기화된다. Play 중 이 값을 바꾸면 실시간 토글되는 스크래치 값.")]
    [SerializeField] private bool bendEnabled = true;

    private const float MinRadius = 0.05f;

    private readonly List<Rigidbody2D> _bones = new();
    private readonly List<Collider2D> _colliders = new();
    private readonly List<HingeJoint2D> _hinges = new();
    private SpineSample[] _boneRest;   // 뼈 rest 포즈 (shape-local)
    private float[] _restRelAngle;     // 이음새별 rest 상대각 (곡률)
    private bool _appliedBend;

    public event Action<Vector2, Vector2, float> OnSegmentCollision;

    public SquishShapeSO Shape => shape;
    public IReadOnlyList<Rigidbody2D> Bones => _bones;
    public IReadOnlyList<SpineSample> BoneRestSamples => _boneRest;

    public float AggregateSpeed
    {
        get
        {
            float max = 0f;
            for (int i = 0; i < _bones.Count; i++)
                max = Mathf.Max(max, _bones[i].linearVelocity.magnitude);
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
        if (shape == null)
        {
            Debug.LogError("[SquishBody] SquishShapeSO가 지정되지 않았습니다.");
            enabled = false;
            return;
        }

        bendEnabled = shape.bendEnabled;   // 형태가 굽힘 여부를 정의 (인스펙터 값은 런타임 토글용)
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
        for (int i = 0; i < _bones.Count; i++)
        {
            var rb = _bones[i];
            rb.position = basePosition + Rotate(_boneRest[i].position, rad);
            rb.rotation = rotationDeg + _boneRest[i].angleDeg;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    public void SetKinematic(bool kinematic)
    {
        var type = kinematic ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        for (int i = 0; i < _bones.Count; i++)
        {
            _bones[i].bodyType = type;
            if (kinematic)
            {
                _bones[i].linearVelocity = Vector2.zero;
                _bones[i].angularVelocity = 0f;
            }
        }
    }

    public void SetLinearVelocityAll(Vector2 velocity)
    {
        for (int i = 0; i < _bones.Count; i++)
            _bones[i].linearVelocity = velocity;
    }

    public void AddImpulseAll(Vector2 impulse, float randomTorque)
    {
        for (int i = 0; i < _bones.Count; i++)
        {
            _bones[i].AddForce(impulse, ForceMode2D.Impulse);
            if (randomTorque > 0f)
                _bones[i].AddTorque(UnityEngine.Random.Range(-randomTorque, randomTorque), ForceMode2D.Impulse);
        }
    }

    public void MoveAll(Vector2 worldDelta)
    {
        for (int i = 0; i < _bones.Count; i++)
            _bones[i].MovePosition(_bones[i].position + worldDelta);
    }

    internal void ReportCollision(Collision2D collision)
    {
        if (collision.contactCount == 0) return;

        var contact = collision.GetContact(0);
        OnSegmentCollision?.Invoke(contact.point, contact.normal, collision.relativeVelocity.magnitude);
    }

    private void Build()
    {
        _boneRest = SpineSampler.Resample(shape.controlPoints, shape.boneCount, shape.smoothSpine);
        int n = _boneRest.Length;

        var mat = new PhysicsMaterial2D("Squishy") { bounciness = shape.bounciness, friction = shape.friction };
        float boneMass = shape.totalMass / n;

        for (int i = 0; i < n; i++)
            _bones.Add(MakeBone(i, boneMass, mat));

        _restRelAngle = new float[Mathf.Max(n - 1, 0)];
        for (int i = 0; i < n - 1; i++)
        {
            _restRelAngle[i] = Mathf.DeltaAngle(_boneRest[i].angleDeg, _boneRest[i + 1].angleDeg);
            ConnectHinge(i);
        }

        for (int i = 0; i < _colliders.Count; i++)
            for (int j = i + 1; j < _colliders.Count; j++)
                Physics2D.IgnoreCollision(_colliders[i], _colliders[j]);

        ApplyBendState();
    }

    private Rigidbody2D MakeBone(int index, float mass, PhysicsMaterial2D mat)
    {
        var s = _boneRest[index];

        var go = new GameObject($"Bone_{index}");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(s.position.x, s.position.y, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, s.angleDeg);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.mass = mass;
        rb.gravityScale = shape.gravityScale;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = Mathf.Max(s.halfWidth, MinRadius);
        col.sharedMaterial = mat;
        _colliders.Add(col);

        var relay = go.AddComponent<SquishSegmentRelay>();
        relay.Init(this);

        return rb;
    }

    // 이음새 hinge: 두 뼈의 중점에 핀. 뼈가 임의 각도라 각 뼈 로컬 프레임으로 앵커를 역변환한다.
    private void ConnectHinge(int i)
    {
        var a = _bones[i];
        var b = _bones[i + 1];
        Vector2 pa = _boneRest[i].position;
        Vector2 pb = _boneRest[i + 1].position;
        Vector2 mid = 0.5f * (pa + pb);

        var hinge = a.gameObject.AddComponent<HingeJoint2D>();
        hinge.connectedBody = b;
        hinge.autoConfigureConnectedAnchor = false;
        hinge.anchor = Rotate(mid - pa, -_boneRest[i].angleDeg * Mathf.Deg2Rad);
        hinge.connectedAnchor = Rotate(mid - pb, -_boneRest[i + 1].angleDeg * Mathf.Deg2Rad);
        hinge.enableCollision = false;
        hinge.useLimits = true;   // 실제 min/max는 ApplyBendState. 한계는 생성 시 rest각 기준(상대).

        _hinges.Add(hinge);
    }

    private void ApplyBendState()
    {
        float limit = bendEnabled ? shape.maxBendAnglePerJoint : 0f;
        for (int i = 0; i < _hinges.Count; i++)
            _hinges[i].limits = new JointAngleLimits2D { min = -limit, max = limit };
        _appliedBend = bendEnabled;
    }

    // 이웃 상대각을 rest 곡률로 되돌리는 굽힘 스프링. 관성 정규화로 뼈 수 무관 일관.
    private void ApplyBendingSprings()
    {
        for (int i = 0; i < _bones.Count - 1; i++)
        {
            var a = _bones[i];
            var b = _bones[i + 1];

            float relDeg = Mathf.DeltaAngle(a.rotation, b.rotation);
            float errDeg = Mathf.DeltaAngle(_restRelAngle[i], relDeg);   // rest각에서 벗어난 정도
            float relVelDeg = b.angularVelocity - a.angularVelocity;

            float t = -shape.bendStiffness * (errDeg * Mathf.Deg2Rad) - shape.bendDamping * (relVelDeg * Mathf.Deg2Rad);
            b.AddTorque(t * b.inertia);
            a.AddTorque(-t * a.inertia);
        }
    }

    private static Vector2 Rotate(Vector2 v, float rad)
    {
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
        return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
    }
}
