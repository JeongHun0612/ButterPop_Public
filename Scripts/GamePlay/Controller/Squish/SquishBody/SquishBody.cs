using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// 뼈 rest 포즈의 스키닝 기준점.
public struct SpineSample
{
    public Vector2 position;
    public float halfWidth;
}

public partial class SquishBody : MonoBehaviour
{
    public enum ColliderShape
    {
        Circle,
        Box
    }

    private sealed class Bone
    {
        public Rigidbody2D Rigidbody;
        public Collider2D Collider;
        public HingeJoint2D Hinge;
        public SpineSample Rest;
    }

    [Header("매달림 기준")]
    [Tooltip("비워두면 자기 Transform 기준으로 그린다. 지정하면 홀더 기준 아래 방향으로 Gizmo를 그린다.")]
    [SerializeField] private Transform holderAnchor;

    [Header("형태 (고정 직사각형, shape-local · 월드 단위)")]
    [Tooltip("긴 축(중심선) 전체 길이")]
    [Min(0.01f)]
    [SerializeField] private float length = 14.3f;
    [Tooltip("짧은 축 전체 폭(두께)")]
    [Min(0.01f)]
    [SerializeField] private float width = 3.3f;

    [Header("물리 뼈")]
    [Tooltip("뼈 콜라이더 형태. Circle=둥근 소시지(잘 구르고 매끈), Box=평평(잘 안착하고 모서리에 걸림).")]
    [SerializeField] private ColliderShape colliderShape = ColliderShape.Circle;
    [Tooltip("중심선을 리샘플링해 만들 강체 세그먼트 수 (5~11 권장)")]
    [Range(2, 32)]
    [SerializeField] private int boneCount = 6;

    [Header("물리")]
    [Min(0.001f)]
    [SerializeField] private float totalMass = 1.2f;
    [SerializeField] private float gravityScale = 1.5f;
    [Min(0f)]
    [SerializeField] private float linearDamping = 0.1f;
    [Min(0f)]
    [SerializeField] private float angularDamping = 2f;
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

    [Header("홀더 잡기")]
    [Tooltip("홀더가 직접 잡아(Kinematic, 각도 완전 고정) 함께 이동시킬 앞쪽 뼈 개수. 0이면 아무 뼈도 안 잡고 아래 HingeJoint로 매달려 자유 스윙, 1이면 첫 뼈, 2면 앞 두 뼈까지 홀더에 고정되고 나머지는 물리로 흔들린다.")]
    [Min(0)]
    [SerializeField] private int grabbedBoneCount = 0;
    [Tooltip("grabbedBoneCount=0(안 잡음)일 때만 사용. 머리 뼈를 홀더에 HingeJoint로 매달 때 머리가 회전할 수 있는 최대 각(강성). 작을수록 뻣뻣, 90이면 제한 없이 자유 스윙.")]
    [Range(0f, 90f)]
    [SerializeField] private float headSwingLimit = 35f;

    private readonly List<Bone> _bones = new();
    private readonly List<Rigidbody2D> _rigidbodies = new();
    private readonly List<SpineSample> _restSamples = new();
    private bool _isReady;
    private HingeJoint2D _holderJoint;

    public event Action<Vector2, Vector2, float> OnSegmentCollision;
    public event Action<Collision2D> OnSegmentCollisionEnter;
    public event Action<Collision2D> OnSegmentCollisionExit;

    public float Length => length;
    public float Width => width;
    public int BoneCount => boneCount;
    public bool IsReady => _isReady;
    public IReadOnlyList<Rigidbody2D> BoneRigidbodies => _rigidbodies;
    public IReadOnlyList<SpineSample> BoneRestSamples => _restSamples;

    // 가장 빠른 뼈의 속력. 하나라도 움직이면 아직 멈춘 게 아니므로 평균이 아니라 최댓값을 쓴다.
    public float MaxBoneSpeed
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

    // 전체 뼈를 rest 포즈로 즉시 스냅
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

    // leading edge(매달린 위쪽 끝) 기준으로 rest 포즈 스냅
    public void ResetPoseFromLeadingEdge(Vector2 leadingEdgeWorld, float angleDeg)
    {
        Vector2 axis = (Vector2)(Quaternion.Euler(0f, 0f, angleDeg) * Vector3.right);
        SnapPose(leadingEdgeWorld + axis * (length * 0.5f), angleDeg);
        SyncBoneTransforms();
        Physics2D.SyncTransforms();
    }

    public void ClearVelocities()
    {
        for (int i = 0; i < _bones.Count; i++)
        {
            var rb = _bones[i].Rigidbody;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    public void ClampVelocities(float maxSpeed)
    {
        if (maxSpeed <= 0f) return;

        for (int i = 0; i < _bones.Count; i++)
        {
            Vector2 velocity = _bones[i].Rigidbody.linearVelocity;
            if (velocity.magnitude > maxSpeed)
                _bones[i].Rigidbody.linearVelocity = velocity.normalized * maxSpeed;
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

    private void SyncBoneTransforms()
    {
        for (int i = 0; i < _bones.Count; i++)
        {
            var rb = _bones[i].Rigidbody;
            rb.transform.SetPositionAndRotation(rb.position, Quaternion.Euler(0f, 0f, rb.rotation));
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

    // 이웃한 뼈를 rest 상대각으로 되돌리는 PD 토크.
    private void ApplyBendingSprings()
    {
        for (int i = 0; i < _bones.Count - 1; i++)
        {
            var a = _bones[i].Rigidbody;
            var b = _bones[i + 1].Rigidbody;

            // rest 상대각이 0이라 이웃 간 상대각이 곧 오차각이다.
            // DeltaAngle이 이미 [-180, 180]으로 정규화해 돌려주므로 추가 보정은 필요 없다.
            float errDeg = Mathf.DeltaAngle(a.rotation, b.rotation);
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
