using System.Collections.Generic;
using UnityEngine;

// 버터 스퀴시의 "살". ButterBody 세그먼트 체인에 격자 메쉬를 선형 블렌드 스키닝(뼈 2개)으로 입힌 뒤,
// 그 위에서 PBD(Verlet) + 면적보존 압력 구속을 돌려 눌리면 옆으로 불룩(스퀴시)하고 출렁이게 한다.
// - goal(목표 정점 위치)이 휜 뼈대를 따라 휘므로, 구부러짐은 물리(뼈대)에서, 스퀴시는 여기서 나온다.
// - 물리 판정에는 개입하지 않는 순수 비주얼. (JellyPressureBlob2D를 체인 스키닝으로 확장한 버전)
public class ButterSquishMesh : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private ButterBody butterBody;
    [Tooltip("버터 스프라이트 (막대 형태, 이 메쉬가 시각을 담당)")]
    [SerializeField] private Sprite butterSprite;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 0;

    [Header("스퀴시 On/Off")]
    [Tooltip("끄면 정점이 뼈대 목표에 딱 붙어 출렁임·눌림이 사라진다(딱딱한 스킨). 굽힘은 그대로 보임. (Play 중 실시간 토글)")]
    [SerializeField] private bool squishEnabled = true;

    [Header("메쉬 해상도")]
    [Tooltip("길이 방향 정점 수 (많을수록 표면 매끈, 18~24 권장)")]
    [SerializeField] private int cols = 20;
    [Tooltip("두께 방향 정점 수 (4~5 권장)")]
    [SerializeField] private int rows = 5;

    [Header("팔로우 앵커 (뼈대 추종)")]
    [Range(0f, 1f)]
    [SerializeField] private float followStiffness = 0.05f;

    [Header("형상 구속 (거리)")]
    [Range(0f, 1f)]
    [SerializeField] private float structuralStiffness = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float shearStiffness = 0.5f;

    [Header("압력 (부피 보존 · 스퀴시 주연)")]
    [Tooltip("눌린 만큼 옆으로 불룩하게 되돌리는 세기. 스트레스볼 손맛의 핵심.")]
    [Range(0f, 1f)]
    [SerializeField] private float pressureStiffness = 0.85f;
    [SerializeField] private float inflate = 1f;

    [Header("시뮬레이션")]
    [Range(1, 16)]
    [SerializeField] private int constraintIterations = 6;
    [Tooltip("속도 감쇠. 낮을수록 오래 꿀렁(말랑이). 높을수록 빨리 멈춤.")]
    [Range(0f, 1f)]
    [SerializeField] private float velocityDamping = 0.04f;
    [Tooltip("정점이 목표에서 벗어날 수 있는 최대 거리 (발산 방지)")]
    [SerializeField] private float maxDeformation = 0.5f;

    [Header("충돌 임펄스 (착지 팝)")]
    [SerializeField] private float impulseScale = 0.5f;
    [SerializeField] private float impulseRadius = 0.4f;
    [SerializeField] private float maxImpulse = 6f;

    private struct DistanceConstraint
    {
        public int a;
        public int b;
        public float stiffness;
    }

    // 정점 → 뼈 2개 스키닝 바인드. offsetA/B는 각 뼈의 rest(회전0) 로컬 오프셋.
    private struct Skin
    {
        public int boneA;
        public int boneB;
        public float weight;    // boneB 쪽 가중치 (0=A, 1=B)
        public Vector2 offsetA;
        public Vector2 offsetB;
    }

    private struct PendingImpulse
    {
        public Vector2 point;
        public Vector2 normal;
        public float magnitude;
    }

    private Mesh _mesh;

    private Skin[] _skin;
    private Vector2[] _pos;
    private Vector2[] _prevPos;
    private Vector2[] _goal;
    private Vector2[] _prevGoal;
    private Vector3[] _verts;

    private readonly List<DistanceConstraint> _distances = new();
    private int[] _boundary;
    private readonly List<PendingImpulse> _impulses = new();

    private IReadOnlyList<Rigidbody2D> _bones;

    private int VertexCount => cols * rows;

    private void Awake()
    {
        if (butterBody == null || butterSprite == null)
        {
            Debug.LogError("[ButterSquishMesh] butterBody 또는 butterSprite가 지정되지 않았습니다.");
            enabled = false;
            return;
        }
        if (cols < 2 || rows < 2)
        {
            Debug.LogError("[ButterSquishMesh] cols · rows는 최소 2 이상이어야 합니다.");
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        if (!enabled) return;

        _bones = butterBody.Segments;   // Awake에서 이미 빌드됨
        BuildMesh();
        BuildConstraints();
        BuildBoundary();
        ResetShape();

        butterBody.OnSegmentCollision += OnSegmentCollision;
    }

    private void FixedUpdate()
    {
        Simulate(Time.fixedDeltaTime);
    }

    private void LateUpdate() => WriteMesh();

    private void OnDestroy()
    {
        if (butterBody != null)
            butterBody.OnSegmentCollision -= OnSegmentCollision;
    }

    // 변형을 지우고 현재 뼈대 자세로 초기화한다.
    public void ResetShape()
    {
        if (_pos == null) return;

        for (int i = 0; i < VertexCount; i++)
        {
            Vector2 g = SkinnedGoal(i);
            _pos[i] = g;
            _prevPos[i] = g;
            _prevGoal[i] = g;
        }
        _impulses.Clear();
    }

    private void BuildMesh()
    {
        var meshGo = new GameObject("ButterSquishMesh");
        meshGo.transform.SetParent(null);   // 월드 공간 렌더 (부모 스케일 영향 제거)
        meshGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        meshGo.transform.localScale = Vector3.one;

        var filter = meshGo.AddComponent<MeshFilter>();
        var renderer = meshGo.AddComponent<MeshRenderer>();
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = sortingOrder;
        renderer.sharedMaterial = CreateMaterial(butterSprite);

        float halfL = butterBody.Length * 0.5f;
        float halfT = butterBody.Thickness * 0.5f;

        var tex = butterSprite.texture;
        var r = butterSprite.rect;
        Vector2 uvMin = new Vector2(r.xMin / tex.width, r.yMin / tex.height);
        Vector2 uvMax = new Vector2(r.xMax / tex.width, r.yMax / tex.height);

        int vc = VertexCount;
        _skin = new Skin[vc];
        _pos = new Vector2[vc];
        _prevPos = new Vector2[vc];
        _goal = new Vector2[vc];
        _prevGoal = new Vector2[vc];
        _verts = new Vector3[vc];
        var uvs = new Vector2[vc];

        for (int row = 0; row < rows; row++)
        {
            float ty = (float)row / (rows - 1);
            for (int col = 0; col < cols; col++)
            {
                float tx = (float)col / (cols - 1);
                int i = row * cols + col;

                Vector2 rest = new Vector2(Mathf.Lerp(-halfL, halfL, tx), Mathf.Lerp(-halfT, halfT, ty));
                _skin[i] = BindSkin(rest);

                uvs[i] = new Vector2(Mathf.Lerp(uvMin.x, uvMax.x, tx), Mathf.Lerp(uvMin.y, uvMax.y, ty));
                _verts[i] = rest;
            }
        }

        var tris = new List<int>((cols - 1) * (rows - 1) * 6);
        for (int row = 0; row < rows - 1; row++)
        {
            for (int col = 0; col < cols - 1; col++)
            {
                int i0 = row * cols + col;
                int i1 = i0 + 1;
                int i2 = i0 + cols;
                int i3 = i2 + 1;
                tris.Add(i0); tris.Add(i2); tris.Add(i1);
                tris.Add(i1); tris.Add(i2); tris.Add(i3);
            }
        }

        _mesh = new Mesh { name = "ButterSquishMesh" };
        _mesh.MarkDynamic();
        _mesh.vertices = _verts;
        _mesh.uv = uvs;
        _mesh.triangles = tris.ToArray();
        _mesh.RecalculateBounds();
        filter.mesh = _mesh;
    }

    // 버터 로컬 rest 위치를 인접한 두 세그먼트에 바인딩한다.
    private Skin BindSkin(Vector2 rest)
    {
        var centersX = butterBody.RestCenterX;
        int n = centersX.Count;

        int a = 0;
        while (a < n - 2 && centersX[a + 1] < rest.x) a++;   // rest.x가 [a, a+1] 구간에 오도록
        int b = Mathf.Min(a + 1, n - 1);

        float denom = centersX[b] - centersX[a];
        float w = denom > 1e-6f ? Mathf.Clamp01((rest.x - centersX[a]) / denom) : 0f;

        // rest는 회전0 기준이라, 각 뼈 로컬 오프셋 = rest - 뼈 rest중심 (y는 그대로).
        return new Skin
        {
            boneA = a,
            boneB = b,
            weight = w,
            offsetA = new Vector2(rest.x - centersX[a], rest.y),
            offsetB = new Vector2(rest.x - centersX[b], rest.y)
        };
    }

    // 현재 뼈대 자세로 정점의 목표 위치(선형 블렌드 스키닝) 계산.
    private Vector2 SkinnedGoal(int i)
    {
        var s = _skin[i];
        var ra = _bones[s.boneA];
        var rb = _bones[s.boneB];

        Vector2 gA = ra.position + Rotate(s.offsetA, ra.rotation * Mathf.Deg2Rad);
        Vector2 gB = rb.position + Rotate(s.offsetB, rb.rotation * Mathf.Deg2Rad);
        return Vector2.Lerp(gA, gB, s.weight);
    }

    private Material CreateMaterial(Sprite sprite)
    {
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        var mat = new Material(shader);
        mat.mainTexture = sprite.texture;
        return mat;
    }

    private void BuildConstraints()
    {
        _distances.Clear();
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int i = row * cols + col;
                if (col < cols - 1) AddDistance(i, i + 1, structuralStiffness);
                if (row < rows - 1) AddDistance(i, i + cols, structuralStiffness);
                if (col < cols - 1 && row < rows - 1)
                {
                    AddDistance(i, i + cols + 1, shearStiffness);
                    AddDistance(i + 1, i + cols, shearStiffness);
                }
            }
        }
    }

    private void AddDistance(int a, int b, float stiffness) =>
        _distances.Add(new DistanceConstraint { a = a, b = b, stiffness = stiffness });

    private void BuildBoundary()
    {
        var loop = new List<int>(2 * (cols + rows) - 4);
        for (int col = 0; col < cols; col++) loop.Add(col);
        for (int row = 1; row < rows; row++) loop.Add(row * cols + (cols - 1));
        for (int col = cols - 2; col >= 0; col--) loop.Add((rows - 1) * cols + col);
        for (int row = rows - 2; row >= 1; row--) loop.Add(row * cols);
        _boundary = loop.ToArray();
    }

    private void Simulate(float dt)
    {
        if (_pos == null || dt <= 0f) return;

        int vc = VertexCount;

        // 스퀴시 Off: 정점을 뼈대 목표에 그대로 붙임(딱딱한 스킨). 굽힘 변형만 반영.
        if (!squishEnabled)
        {
            for (int i = 0; i < vc; i++)
            {
                Vector2 g = SkinnedGoal(i);
                _pos[i] = g;
                _prevPos[i] = g;
                _prevGoal[i] = g;
                _goal[i] = g;
            }
            _impulses.Clear();
            return;
        }

        // 목표 위치 갱신 + 텔레포트(리셋/스폰) 스냅.
        const float teleportDistSqr = 1f;
        for (int i = 0; i < vc; i++)
        {
            Vector2 g = SkinnedGoal(i);
            if ((g - _prevGoal[i]).sqrMagnitude > teleportDistSqr)
            {
                _pos[i] = g;
                _prevPos[i] = g;
            }
            _goal[i] = g;
            _prevGoal[i] = g;
        }

        // Verlet 예측 (외력은 팔로우 앵커 구속이 대신).
        float keep = 1f - velocityDamping;
        for (int i = 0; i < vc; i++)
        {
            Vector2 velocity = (_pos[i] - _prevPos[i]) * keep;
            _prevPos[i] = _pos[i];
            _pos[i] += velocity;
        }

        DrainImpulses();

        for (int it = 0; it < constraintIterations; it++)
        {
            ProjectDistances();
            ProjectFollow();
            ProjectPressure();
        }

        for (int i = 0; i < vc; i++)
        {
            if (float.IsNaN(_pos[i].x) || float.IsNaN(_pos[i].y))
            {
                _pos[i] = _goal[i];
                _prevPos[i] = _goal[i];
                continue;
            }

            Vector2 off = _pos[i] - _goal[i];
            float m = off.magnitude;
            if (m > maxDeformation)
            {
                _pos[i] = _goal[i] + off * (maxDeformation / m);
                _prevPos[i] = _pos[i];
            }
        }
    }

    // 이웃 거리를 목표 형상(goal)의 길이로 되돌린다. rest를 매 프레임 goal에서 뽑아 회전·굽힘에 자동 대응.
    private void ProjectDistances()
    {
        foreach (var c in _distances)
        {
            Vector2 d = _pos[c.b] - _pos[c.a];
            float len = d.magnitude;
            if (len < 1e-5f) continue;

            float rest = (_goal[c.b] - _goal[c.a]).magnitude;
            Vector2 corr = 0.5f * c.stiffness * ((len - rest) / len) * d;
            _pos[c.a] += corr;
            _pos[c.b] -= corr;
        }
    }

    private void ProjectFollow()
    {
        if (followStiffness <= 0f) return;

        for (int i = 0; i < VertexCount; i++)
            _pos[i] += followStiffness * (_goal[i] - _pos[i]);
    }

    // 전역 면적 보존. 눌려서 넓이가 줄면 경계 정점을 바깥 법선으로 밀어 부피(=스퀴시 볼륨)를 되돌린다.
    private void ProjectPressure()
    {
        if (pressureStiffness <= 0f || _boundary == null) return;

        int n = _boundary.Length;
        if (n < 3) return;

        float restArea = SignedArea(_goal) * inflate;
        float area = SignedArea(_pos);
        float c = area - restArea;

        float sumGradSq = 0f;
        for (int k = 0; k < n; k++)
        {
            Vector2 prev = _pos[_boundary[(k - 1 + n) % n]];
            Vector2 next = _pos[_boundary[(k + 1) % n]];
            Vector2 grad = 0.5f * new Vector2(next.y - prev.y, prev.x - next.x);
            sumGradSq += grad.sqrMagnitude;
        }
        if (sumGradSq < 1e-8f) return;

        float lambda = c / sumGradSq;
        for (int k = 0; k < n; k++)
        {
            Vector2 prev = _pos[_boundary[(k - 1 + n) % n]];
            Vector2 next = _pos[_boundary[(k + 1) % n]];
            Vector2 grad = 0.5f * new Vector2(next.y - prev.y, prev.x - next.x);
            _pos[_boundary[k]] -= pressureStiffness * lambda * grad;
        }
    }

    private float SignedArea(Vector2[] src)
    {
        int n = _boundary.Length;
        float sum = 0f;
        for (int k = 0; k < n; k++)
        {
            Vector2 p = src[_boundary[k]];
            Vector2 q = src[_boundary[(k + 1) % n]];
            sum += p.x * q.y - q.x * p.y;
        }
        return 0.5f * sum;
    }

    private void DrainImpulses()
    {
        if (_impulses.Count == 0) return;

        foreach (var imp in _impulses)
        {
            float kick = Mathf.Min(imp.magnitude * impulseScale, maxImpulse);
            if (kick <= 0f) continue;

            float invR2 = 1f / (impulseRadius * impulseRadius);
            for (int i = 0; i < VertexCount; i++)
            {
                float d2 = (_pos[i] - imp.point).sqrMagnitude;
                float w = Mathf.Exp(-d2 * invR2);
                _pos[i] += imp.normal * (kick * w * Time.fixedDeltaTime);
            }
        }
        _impulses.Clear();
    }

    private void WriteMesh()
    {
        if (_mesh == null) return;

        // 월드 공간 렌더: 정점 = 시뮬 위치 그대로.
        for (int i = 0; i < VertexCount; i++)
            _verts[i] = _pos[i];

        _mesh.vertices = _verts;
        _mesh.RecalculateBounds();
    }

    private void OnSegmentCollision(Vector2 point, Vector2 normal, float magnitude)
    {
        _impulses.Add(new PendingImpulse { point = point, normal = normal, magnitude = magnitude });
    }

    private static Vector2 Rotate(Vector2 v, float rad)
    {
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
        return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
    }

    private void OnDrawGizmosSelected()
    {
        if (_pos == null) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < VertexCount; i++)
            Gizmos.DrawSphere(_pos[i], 0.02f);
    }
}
