using System;
using System.Collections.Generic;
using UnityEngine;

// 뼈 rest 포즈의 스키닝 기준점.
public struct SpineSample
{
    public Vector2 position;
    public float halfWidth;
}

// 스퀴시 오브젝트의 물리 뼈대. 프리팹에 구운 강체 세그먼트 체인을 채택해 구동한다.
public class SquishBody : MonoBehaviour
{
    private sealed class Bone
    {
        public Rigidbody2D Rigidbody;
        public CircleCollider2D Collider;
        public HingeJoint2D Hinge;
        public SpineSample Rest;
    }

    [Header("형태 (고정 직사각형, shape-local · 월드 단위)")]
    [Tooltip("긴 축(중심선) 전체 길이")]
    [Min(0.01f)]
    [SerializeField] private float length = 14.3f;
    [Tooltip("짧은 축 전체 폭(두께)")]
    [Min(0.01f)]
    [SerializeField] private float width = 3.3f;

    [Header("물리 뼈")]
    [Tooltip("중심선을 리샘플링해 만들 강체 세그먼트 수 (5~11 권장)")]
    [Range(2, 32)]
    [SerializeField] private int boneCount = 6;

    [Header("질량 / 중력 / 물성")]
    [Min(0.001f)]
    [SerializeField] private float totalMass = 1.2f;
    [SerializeField] private float gravityScale = 1.5f;
    [Range(0f, 1f)]
    [SerializeField] private float bounciness = 0.1f;
    [Range(0f, 1f)]
    [SerializeField] private float friction = 0.6f;

    [Header("굽힘")]
    [Tooltip("평평한 직선(rest각 0)으로 되돌리는 세기.")]
    [Min(0f)]
    [SerializeField] private float bendStiffness = 60f;
    [Min(0f)]
    [SerializeField] private float bendDamping = 8f;
    [Tooltip("이음새 하나가 rest 각(0)에서 벗어날 수 있는 최대 각(도)")]
    [Range(0f, 180f)]
    [SerializeField] private float maxBendAnglePerJoint = 18f;

    private readonly List<Bone> _bones = new();
    private readonly List<Rigidbody2D> _rigidbodies = new();
    private readonly List<SpineSample> _restSamples = new();
    private bool _isReady;

    public event Action<Vector2, Vector2, float> OnSegmentCollision;
    public event Action<Collision2D> OnSegmentCollisionEnter;
    public event Action<Collision2D> OnSegmentCollisionExit;

    public float Length => length;
    public float Width => width;
    public int BoneCount => boneCount;
    public bool IsReady => _isReady;
    public IReadOnlyList<Rigidbody2D> BoneRigidbodies => _rigidbodies;
    public IReadOnlyList<SpineSample> BoneRestSamples => _restSamples;

    public float AggregateSpeed
    {
        get
        {
            float max = 0f;
            for (int i = 0; i < _bones.Count; i++)
                max = Mathf.Max(max, _bones[i].Rigidbody.linearVelocity.magnitude);
            return max;
        }
    }

    public Bounds WorldBounds
    {
        get
        {
            if (_bones.Count == 0) return new Bounds(transform.position, Vector3.zero);
            Bounds b = _bones[0].Collider.bounds;
            for (int i = 1; i < _bones.Count; i++)
                b.Encapsulate(_bones[i].Collider.bounds);
            return b;
        }
    }

    private void Awake()
    {
        if (length <= 0f || width <= 0f)
        {
            Debug.LogError("[SquishBody] length·width는 0보다 커야 합니다.");
            enabled = false;
            return;
        }

        Build();
    }

    private void FixedUpdate() => ApplyBendingSprings();

    // 뼈 전체를 rest 포즈 기준으로 즉시 배치한다.
    public void SnapPose(Vector2 originWorld, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        for (int i = 0; i < _bones.Count; i++)
        {
            var rb = _bones[i].Rigidbody;
            rb.position = originWorld + Rotate(_bones[i].Rest.position, rad);
            rb.rotation = angleDeg;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    public void RotatePoseAround(Vector2 pivotWorld, float angleDeg)
    {
        if (Mathf.Approximately(angleDeg, 0f)) return;

        float rad = angleDeg * Mathf.Deg2Rad;
        for (int i = 0; i < _bones.Count; i++)
        {
            var rb = _bones[i].Rigidbody;
            rb.position = pivotWorld + Rotate(rb.position - pivotWorld, rad);
            rb.rotation += angleDeg;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    public void SetKinematic(bool kinematic)
    {
        var type = kinematic ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        for (int i = 0; i < _bones.Count; i++)
        {
            var rb = _bones[i].Rigidbody;
            rb.bodyType = type;
            if (kinematic)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
    }

    internal void ReportCollisionEnter(Collision2D collision)
    {
        OnSegmentCollisionEnter?.Invoke(collision);

        if (collision.contactCount == 0) return;

        var contact = collision.GetContact(0);
        OnSegmentCollision?.Invoke(contact.point, contact.normal, collision.relativeVelocity.magnitude);
    }

    internal void ReportCollisionExit(Collision2D collision) => OnSegmentCollisionExit?.Invoke(collision);

    private void Build()
    {
        if (!TryLoadBakedBones())
        {
            enabled = false;
            return;
        }

        for (int i = 0; i < _bones.Count; i++)
            for (int j = i + 1; j < _bones.Count; j++)
                Physics2D.IgnoreCollision(_bones[i].Collider, _bones[j].Collider);

        ApplyBendLimits();
        _isReady = true;
    }

    // 프리팹에 미리 구운 뼈를 불러오고 런타임 물성을 적용한다.
    private bool TryLoadBakedBones()
    {
        _isReady = false;
        ClearBoneCaches();

        var relays = new List<SquishSegmentRelay>();
        foreach (Transform child in transform)
        {
            var relay = child.GetComponent<SquishSegmentRelay>();
            if (relay != null) relays.Add(relay);
        }
        if (relays.Count < 2)
        {
            Debug.LogError("[SquishBody] 구운 뼈가 없습니다. 인스펙터의 '뼈 굽기'로 프리팹에 뼈를 구우세요.");
            return false;
        }

        relays.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

        int n = relays.Count;
        if (n != boneCount)
            Debug.LogWarning($"[SquishBody] 구운 뼈 수({n})가 현재 boneCount({boneCount})와 다릅니다. 형태 값을 바꿨다면 뼈를 다시 구우세요.", this);

        if (!ValidateBakedBoneChain(relays))
        {
            ClearBoneCaches();
            return false;
        }

        var mat = new PhysicsMaterial2D("Squishy") { bounciness = bounciness, friction = friction };
        float boneMass = totalMass / n;

        for (int i = 0; i < n; i++)
        {
            var t = relays[i].transform;
            var rb = t.GetComponent<Rigidbody2D>();
            var col = t.GetComponent<CircleCollider2D>();
            if (rb == null || col == null)
            {
                Debug.LogError($"[SquishBody] 구운 뼈 '{t.name}'에 Rigidbody2D/CircleCollider2D가 없습니다. 뼈를 다시 구우세요.");
                return false;
            }

            ConfigureBoneRigidbody(rb, boneMass);
            col.sharedMaterial = mat;
            relays[i].Init(this);

            var rest = new SpineSample { position = t.localPosition, halfWidth = col.radius };
            _bones.Add(new Bone
            {
                Rigidbody = rb,
                Collider = col,
                Hinge = t.GetComponent<HingeJoint2D>(),
                Rest = rest
            });
            _rigidbodies.Add(rb);
            _restSamples.Add(rest);
        }
        return true;
    }

    private void ConfigureBoneRigidbody(Rigidbody2D rb, float mass)
    {
        rb.mass = mass;
        rb.gravityScale = gravityScale;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void ApplyBendLimits()
    {
        var limits = new JointAngleLimits2D { min = -maxBendAnglePerJoint, max = maxBendAnglePerJoint };
        for (int i = 0; i < _bones.Count; i++)
            if (_bones[i].Hinge != null)
            {
                _bones[i].Hinge.useLimits = true;
                _bones[i].Hinge.enableCollision = false;
                _bones[i].Hinge.limits = limits;
            }
    }

    private void ClearBoneCaches()
    {
        _bones.Clear();
        _rigidbodies.Clear();
        _restSamples.Clear();
    }

    private bool ValidateBakedBoneChain(List<SquishSegmentRelay> relays)
    {
        for (int i = 0; i < relays.Count; i++)
        {
            var t = relays[i].transform;
            var rb = t.GetComponent<Rigidbody2D>();
            var col = t.GetComponent<CircleCollider2D>();
            if (rb == null || col == null)
            {
                Debug.LogError($"[SquishBody] 구운 뼈 '{t.name}'에 Rigidbody2D/CircleCollider2D가 없습니다. 뼈를 다시 구우세요.", this);
                return false;
            }

            var hinge = t.GetComponent<HingeJoint2D>();
            if (i < relays.Count - 1)
            {
                var nextRb = relays[i + 1].GetComponent<Rigidbody2D>();
                if (hinge == null)
                {
                    Debug.LogError($"[SquishBody] 구운 뼈 '{t.name}'에 다음 뼈와 연결된 HingeJoint2D가 없습니다. 뼈를 다시 구우세요.", this);
                    return false;
                }
                if (hinge.connectedBody != nextRb)
                {
                    Debug.LogError($"[SquishBody] 구운 뼈 '{t.name}'의 HingeJoint2D가 다음 뼈와 연결되어 있지 않습니다. 뼈를 다시 구우세요.", this);
                    return false;
                }
            }
            else if (hinge != null)
            {
                Debug.LogWarning($"[SquishBody] 마지막 구운 뼈 '{t.name}'에 불필요한 HingeJoint2D가 있습니다. 뼈를 다시 굽는 것을 권장합니다.", this);
            }
        }

        return true;
    }

    // 이웃한 뼈를 rest 상대각으로 되돌리는 PD 토크.
    private void ApplyBendingSprings()
    {
        for (int i = 0; i < _bones.Count - 1; i++)
        {
            var a = _bones[i].Rigidbody;
            var b = _bones[i + 1].Rigidbody;

            float relDeg = Mathf.DeltaAngle(a.rotation, b.rotation);
            float errDeg = Mathf.DeltaAngle(0f, relDeg);
            float relVelDeg = b.angularVelocity - a.angularVelocity;

            float t = -bendStiffness * (errDeg * Mathf.Deg2Rad) - bendDamping * (relVelDeg * Mathf.Deg2Rad);
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
