using System;
using System.Collections.Generic;
using UnityEngine;

// 스퀴시 오브젝트의 물리 뼈대(일반형). 스파인 제어점을 리샘플링해 강체 세그먼트 체인을 만든다.
// - 굽힘 스프링 rest각 = 스파인의 원래 곡률 → 버터는 직선, 바나나는 초승달을 유지하며 그 주위로 말랑거린다.
// - 시각(스퀴시)은 SquishMesh가 이 뼈들에 스키닝해 입힌다.
public class SquishBody : MonoBehaviour
{
    [Header("스파인 (끌대 중심선) — sprite-local, 순서대로")]
    [SerializeField] private SpinePoint[] controlPoints;
    [Tooltip("제어점 사이를 Catmull-Rom으로 매끄럽게 이을지. 끄면 직선 연결.")]
    [SerializeField] private bool smoothSpine = true;

    [Header("물리 뼈")]
    [Tooltip("스파인을 리샘플링해 만들 강체 세그먼트 수 (9~11 권장)")]
    [Range(2, 32)]
    [SerializeField] private int boneCount = 11;

    [Header("질량 / 중력 / 물성")]
    [SerializeField] private float totalMass = 1.2f;
    [SerializeField] private float gravityScale = 1.5f;
    [Range(0f, 1f)]
    [SerializeField] private float bounciness = 0.1f;
    [Range(0f, 1f)]
    [SerializeField] private float friction = 0.6f;

    [Header("굽힘")]
    [Tooltip("구부러질지 여부. 끄면 rest 곡률을 유지하는 뻣뻣한 스퀴시. 버터/베이컨처럼 드레이핑이 필요하면 켠다. 런타임 토글 가능.")]
    [SerializeField] private bool bendEnabled = true;
    [Tooltip("스파인 원래 곡률로 되돌리는 세기.")]
    [SerializeField] private float bendStiffness = 60f;
    [SerializeField] private float bendDamping = 8f;
    [Tooltip("이음새 하나가 rest 각에서 벗어날 수 있는 최대 각(도)")]
    [SerializeField] private float maxBendAnglePerJoint = 18f;

    private const float MinRadius = 0.05f;
    private const float MinScale = 0.01f;

    private readonly List<Rigidbody2D> _bones = new();
    private readonly List<Collider2D> _colliders = new();
    private readonly List<HingeJoint2D> _hinges = new();
    private SpineSample[] _boneRest;   // 뼈 rest 포즈 (shape-local, 스폰 스케일 반영됨)
    private SpinePoint[] _scaledControl;   // controlPoints × spawnScale (Build 1회)
    private float _spawnScale = 1f;
    private float _spawnRotationDeg = 0f;
    private float[] _restRelAngle;     // 이음새별 rest 상대각 (곡률)
    private bool _appliedBend;

    public event Action<Vector2, Vector2, float> OnSegmentCollision;
    public event Action<Collision2D> OnSegmentCollisionEnter;
    public event Action<Collision2D> OnSegmentCollisionExit;

    public SpinePoint[] ControlPoints => controlPoints;              // 원본(native) — 에디터 프리뷰·기즈모용
    public SpinePoint[] EffectiveControlPoints => _scaledControl ?? controlPoints;   // 스폰 스케일 반영 — 런타임 메쉬용
    public bool SmoothSpine => smoothSpine;
    public float SpawnScale => _spawnScale;
    public float SpawnRotationDeg => _spawnRotationDeg;
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
        if (controlPoints == null || controlPoints.Length < 2)
        {
            Debug.LogError("[SquishBody] controlPoints가 2개 이상 필요합니다.");
            enabled = false;
            return;
        }

        Build();
    }

    private void FixedUpdate()
    {
        if (bendEnabled != _appliedBend)
            ApplyBendState();

        if (bendEnabled)
            ApplyBendingSprings();
    }

    // shape-local 원점을 basePosition에 놓고 그 점을 중심으로 회전시켜 배치하는 간편 오버로드.
    public void ResetLayout(Vector2 basePosition, float rotationDeg)
        => ResetLayout(basePosition, Vector2.zero, rotationDeg);

    // 임의의 피벗 점을 중심으로 rest 포즈를 rotationDeg만큼 회전시켜 배치한다.
    // pivotWorld: 회전 중심의 월드 좌표. pivotLocal: 같은 피벗의 shape-local 좌표.
    public void ResetLayout(Vector2 pivotWorld, Vector2 pivotLocal, float rotationDeg)
    {
        float rad = rotationDeg * Mathf.Deg2Rad;
        for (int i = 0; i < _bones.Count; i++)
        {
            var rb = _bones[i];
            rb.position = pivotWorld + Rotate(_boneRest[i].position - pivotLocal, rad);
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
        // 스폰 크기·회전을 SquishVisual Transform에서 읽어 지오메트리에 굽는다(균일 스케일).
        ReadSpawnPoseFromVisual();
        _scaledControl = ScalePoints(controlPoints, _spawnScale);

        // 물리 뼈는 끝단 반폭만큼 인셋 → 원형 콜라이더 캡이 비주얼 밖으로 튀지 않는다(캡슐 모델).
        float insetStart = 0f, insetEnd = 0f;
        if (_scaledControl.Length > 0)
        {
            insetStart = _scaledControl[0].halfWidth;
            insetEnd = _scaledControl[_scaledControl.Length - 1].halfWidth;
        }

        _boneRest = SpineSampler.Resample(_scaledControl, boneCount, smoothSpine, insetStart, insetEnd);
        int n = _boneRest.Length;

        var mat = new PhysicsMaterial2D("Squishy") { bounciness = bounciness, friction = friction };
        float boneMass = totalMass / n;

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

    // 스폰 스케일(균일)·회전을 SquishVisual 오브젝트의 Transform에서 읽는다.
    // 이 값은 Build 때 소비돼 뼈 지오메트리에 구워지고, 런타임엔 SquishMesh가 비주얼 Transform을 원점 리셋한다.
    // (프리팹에서 SquishVisual을 스케일/회전해 저작 → 미리보기 = 런타임)
    private void ReadSpawnPoseFromVisual()
    {
        _spawnScale = 1f;
        _spawnRotationDeg = 0f;

        var mesh = GetComponent<SquishMesh>();
        var vr = mesh != null ? mesh.VisualRenderer : null;
        if (vr == null) return;

        var t = vr.transform;
        Vector3 ls = t.lossyScale;
        _spawnScale = Mathf.Max(ls.x, MinScale);
        if (!Mathf.Approximately(ls.x, ls.y))
            Debug.LogWarning($"[SquishBody] SquishVisual 스케일이 비균일(x={ls.x:0.###}, y={ls.y:0.###})입니다. 원형 콜라이더 특성상 x를 균일 스케일로 사용합니다.");

        _spawnRotationDeg = t.eulerAngles.z;
    }

    // 제어점 위치·반폭을 균일하게 s배. s==1이면 원본을 그대로 반환(복사·할당 없음).
    private static SpinePoint[] ScalePoints(SpinePoint[] src, float s)
    {
        if (src == null || Mathf.Approximately(s, 1f)) return src;

        var arr = new SpinePoint[src.Length];
        for (int i = 0; i < src.Length; i++)
            arr[i] = new SpinePoint { position = src[i].position * s, halfWidth = src[i].halfWidth * s };
        return arr;
    }

    // 세그먼트 뼈 하나 생성: Rigidbody2D + 원형 콜라이더 + 충돌 릴레이. rest 샘플로 위치·각도·반지름 결정.
    private Rigidbody2D MakeBone(int index, float mass, PhysicsMaterial2D mat)
    {
        var s = _boneRest[index];

        var go = new GameObject($"Bone_{index}");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(s.position.x, s.position.y, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, s.angleDeg);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.mass = mass;
        rb.gravityScale = gravityScale;
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

    // 이웃한 두 뼈를 힌지로 연결. 두 뼈의 중점을 각 뼈의 로컬 좌표로 역회전해 앵커로 지정 → rest 포즈에서 정확히 맞물린다.
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
        hinge.useLimits = true;

        _hinges.Add(hinge);
    }

    // 힌지 각도 제한 갱신 — bend 끄면 0(rest 곡률에 고정=뻣뻣), 켜면 ±maxBendAnglePerJoint. bend 토글 시 재적용.
    private void ApplyBendState()
    {
        float limit = bendEnabled ? maxBendAnglePerJoint : 0f;
        for (int i = 0; i < _hinges.Count; i++)
            _hinges[i].limits = new JointAngleLimits2D { min = -limit, max = limit };
        _appliedBend = bendEnabled;
    }

    // 각 이음새를 rest 상대각으로 되돌리는 PD 토크(비례=bendStiffness, 미분=bendDamping)를 건다. 매 물리 스텝 적분.
    private void ApplyBendingSprings()
    {
        for (int i = 0; i < _bones.Count - 1; i++)
        {
            var a = _bones[i];
            var b = _bones[i + 1];

            float relDeg = Mathf.DeltaAngle(a.rotation, b.rotation);
            float errDeg = Mathf.DeltaAngle(_restRelAngle[i], relDeg);
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

    // 편집 시점 실루엣 미리보기 — 스프라이트 없이도 형태·배치를 눈으로 확인할 수 있게 한다.
    private void OnDrawGizmos()
    {
        if (Application.isPlaying || controlPoints == null || controlPoints.Length < 2) return;

        int resolution = 20;
        var meshComp = GetComponent<SquishMesh>();
        if (meshComp != null) resolution = Mathf.Max(meshComp.MeshCols, 2);

        // SquishVisual이 있으면 그 Transform 공간에 그린다 → 스케일·회전이 baked 메쉬와 일치.
        Transform vt = (meshComp != null && meshComp.VisualRenderer != null)
            ? meshComp.VisualRenderer.transform
            : transform;

        var cols = SpineSampler.Resample(controlPoints, resolution, smoothSpine);
        int n = cols.Length;

        Gizmos.color = new Color(1f, 0.85f, 0.25f, 0.9f);

        Vector3 prevTop = default, prevBot = default;
        for (int i = 0; i < n; i++)
        {
            var s = cols[i];
            Vector2 normal = new Vector2(-Mathf.Sin(s.angleDeg * Mathf.Deg2Rad), Mathf.Cos(s.angleDeg * Mathf.Deg2Rad));
            Vector3 top = vt.TransformPoint(s.position + normal * s.halfWidth);
            Vector3 bot = vt.TransformPoint(s.position - normal * s.halfWidth);

            if (i == 0) Gizmos.DrawLine(top, bot);
            else
            {
                Gizmos.DrawLine(prevTop, top);
                Gizmos.DrawLine(prevBot, bot);
            }
            if (i == n - 1) Gizmos.DrawLine(top, bot);

            prevTop = top;
            prevBot = bot;
        }
    }
}
