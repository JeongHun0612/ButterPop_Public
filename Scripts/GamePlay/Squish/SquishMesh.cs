using System.Collections.Generic;
using UnityEngine;

// 스퀴시 오브젝트의 "살"(일반형). SquishBody 뼈 체인에 스파인 스윕 격자를 스키닝하고,
// 그 위에서 PBD(Verlet) + 면적보존 압력으로 눌리고 출렁이게 한다.
public class SquishMesh : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private SquishBody body;
    [Tooltip("비주얼 스킨 (스프라이트 + 타격음). SquishSound와 같은 SO를 물려 사용.")]
    [SerializeField] private SquishSkinSO skin;

    [Header("비주얼 오브젝트")]
    [Tooltip("프리팹에 미리 배치한 비주얼 오브젝트. MeshFilter + MeshRenderer가 달린 자식 GO를 지정. 비우면 런타임에 자동 생성.")]
    [SerializeField] private MeshRenderer visualRenderer;

    [Header("스퀴시 메쉬 해상도")]
    [Tooltip("스파인 방향 정점 수 (표면 매끈함)")]
    [Range(2, 64)]
    [SerializeField] private int meshCols = 20;
    [Tooltip("폭 방향 정점 수 (스퀴시 볼륨)")]
    [Range(2, 16)]
    [SerializeField] private int meshRows = 5;

    [Header("스퀴시 On/Off")]
    [Tooltip("끄면 정점이 뼈대 목표에 딱 붙어 출렁임·눌림이 사라진다(딱딱한 스킨). 굽힘은 그대로. (실시간 토글)")]
    [SerializeField] private bool squishEnabled = true;

    [Header("팔로우 앵커")]
    [Range(0f, 1f)]
    [SerializeField] private float followStiffness = 0.05f;

    [Header("형상 구속 (거리)")]
    [Range(0f, 1f)]
    [SerializeField] private float structuralStiffness = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float shearStiffness = 0.5f;

    [Header("압력 (부피 보존 · 스퀴시 주연)")]
    [Range(0f, 1f)]
    [SerializeField] private float pressureStiffness = 0.85f;
    [SerializeField] private float inflate = 1f;

    [Header("시뮬레이션")]
    [Range(1, 16)]
    [SerializeField] private int constraintIterations = 6;
    [Range(0f, 1f)]
    [SerializeField] private float velocityDamping = 0.04f;
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

    private struct Skin
    {
        public int boneA;
        public int boneB;
        public float weight;
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
    private GameObject _visualGo;

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
    private IReadOnlyList<SpineSample> _boneRest;

    private int _cols;
    private int _rows;
    private int VertexCount => _cols * _rows;

    public int MeshCols => meshCols;
    public int MeshRows => meshRows;
    public SquishSkinSO SkinSO => skin;
    public MeshRenderer VisualRenderer => visualRenderer;

    private void Awake()
    {
        if (body == null) body = GetComponent<SquishBody>();

        if (body == null || ResolveSprite() == null)
        {
            Debug.LogError("[SquishMesh] body / skin sprite가 지정되지 않았습니다.");
            enabled = false;
        }
    }

    private Sprite ResolveSprite() => (skin != null) ? skin.sprite : null;

    private Rect ResolveOpaqueUVRect()
        => (skin != null && skin.sprite != null) ? skin.opaqueUVRect : new Rect(0f, 0f, 1f, 1f);

    private void Start()
    {
        if (!enabled) return;

        _bones = body.Bones;
        _boneRest = body.BoneRestSamples;
        _cols = meshCols;
        _rows = meshRows;

        BuildMesh();
        BuildConstraints();
        BuildBoundary();
        ResetShape();

        body.OnSegmentCollision += OnSegmentCollision;
    }

    private void FixedUpdate() => Simulate(Time.fixedDeltaTime);

    private void LateUpdate() => WriteMesh();

    private void OnDestroy()
    {
        if (body != null)
            body.OnSegmentCollision -= OnSegmentCollision;

        if (_visualGo != null)
            Destroy(_visualGo);
    }

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

    public void SetTint(Color c)
    {
        if (_mesh == null) return;
        _mesh.colors = SolidColors(c);
    }

    private void BuildMesh()
    {
        var sprite = ResolveSprite();

        MeshFilter filter;
        MeshRenderer renderer;

        if (visualRenderer != null)
        {
            // 프리팹에 미리 배치된 비주얼 오브젝트 사용
            _visualGo = visualRenderer.gameObject;
            filter = _visualGo.GetComponent<MeshFilter>();
            if (filter == null) filter = _visualGo.AddComponent<MeshFilter>();
            renderer = visualRenderer;
        }
        else
        {
            // 비주얼 오브젝트 자동 생성
            _visualGo = new GameObject("SquishVisual");
            filter = _visualGo.AddComponent<MeshFilter>();
            renderer = _visualGo.AddComponent<MeshRenderer>();
        }

        // 월드 공간 렌더 (정점이 월드 좌표라 부모 트랜스폼과 겹치면 이중 오프셋 → 원점 identity로 떼어냄)
        _visualGo.transform.SetParent(null);
        _visualGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        _visualGo.transform.localScale = Vector3.one;

        ApplyMaterial(renderer, sprite);

        // rest 지오메트리(정점·UV·삼각형). 뼈가 스폰 스케일로 구워지므로 메쉬 그리드도 동일 스케일(EffectiveControlPoints)로 맞춘다.
        BuildRestGeometry(body.EffectiveControlPoints, body.SmoothSpine, _cols, _rows, sprite, ResolveOpaqueUVRect(),
            out _verts, out var uvs, out var tris);

        int vc = VertexCount;
        _skin = new Skin[vc];
        _pos = new Vector2[vc];
        _prevPos = new Vector2[vc];
        _goal = new Vector2[vc];
        _prevGoal = new Vector2[vc];

        // 정점을 뼈에 스키닝 바인딩 (뼈 rest 포즈 필요 → 런타임 전용).
        for (int col = 0; col < _cols; col++)
        {
            float fc = _cols > 1 ? col / (float)(_cols - 1) : 0f;
            for (int row = 0; row < _rows; row++)
            {
                int i = row * _cols + col;
                _skin[i] = BindSkin(fc, _verts[i]);
            }
        }

        _mesh = new Mesh { name = "SquishMesh" };
        _mesh.MarkDynamic();
        _mesh.vertices = _verts;
        _mesh.uv = uvs;
        _mesh.colors = SolidColors(Color.white);
        _mesh.triangles = tris;
        _mesh.RecalculateBounds();
        filter.mesh = _mesh;
    }

    // 미리 배정된 재질이 있으면 재사용(인스턴스화 후 텍스처만 스킨으로 교체) → 런타임 Shader.Find 제거.
    // 없으면 기존처럼 런타임 생성.
    private void ApplyMaterial(MeshRenderer renderer, Sprite sprite)
    {
        if (renderer.sharedMaterial != null)
        {
            renderer.material.mainTexture = sprite.texture;
            return;
        }
        renderer.sharedMaterial = CreateMaterial(sprite);
    }

    // rest 지오메트리를 컨트롤 포인트+스프라이트만으로 생성한다(뼈 무관).
    // 런타임 BuildMesh와 에디터 프리뷰 메쉬 굽기가 공유해 동일한 형태를 보장한다.
    public static void BuildRestGeometry(SpinePoint[] controlPoints, bool smooth, int cols, int rows,
        Sprite sprite, Rect opaqueUVRect,
        out Vector3[] verts, out Vector2[] uvs, out int[] tris)
    {
        cols = Mathf.Max(cols, 2);
        rows = Mathf.Max(rows, 2);
        int vc = cols * rows;

        var samples = SpineSampler.Resample(controlPoints, cols, smooth);
        verts = new Vector3[vc];
        var rest = new Vector2[vc];

        for (int col = 0; col < cols; col++)
        {
            var s = samples[col];
            Vector2 normal = new Vector2(-Mathf.Sin(s.angleDeg * Mathf.Deg2Rad), Mathf.Cos(s.angleDeg * Mathf.Deg2Rad));
            for (int row = 0; row < rows; row++)
            {
                float v = rows > 1 ? Mathf.Lerp(-1f, 1f, row / (float)(rows - 1)) : 0f;
                int i = row * cols + col;
                rest[i] = s.position + normal * (v * s.halfWidth);
                verts[i] = rest[i];
            }
        }

        uvs = BuildUVs(sprite, opaqueUVRect, rest);
        tris = BuildTriangles(cols, rows);
    }

    // 에디터에서 프리팹 MeshFilter에 넣을 rest 메쉬 자산을 만든다.
    public static Mesh BuildPreviewMesh(SpinePoint[] controlPoints, bool smooth, int cols, int rows,
        Sprite sprite, Rect opaqueUVRect)
    {
        BuildRestGeometry(controlPoints, smooth, cols, rows, sprite, opaqueUVRect,
            out var verts, out var uvs, out var tris);

        var mesh = new Mesh { name = "SquishMesh" };
        var colors = new Color[verts.Length];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    private Skin BindSkin(float fc, Vector2 rest)
    {
        int n = _boneRest.Count;
        float boneFloat = fc * (n - 1);
        int a = Mathf.Clamp(Mathf.FloorToInt(boneFloat), 0, n - 1);
        int b = Mathf.Min(a + 1, n - 1);
        float w = boneFloat - a;

        return new Skin
        {
            boneA = a,
            boneB = b,
            weight = w,
            offsetA = InverseRotate(rest - _boneRest[a].position, _boneRest[a].angleDeg),
            offsetB = InverseRotate(rest - _boneRest[b].position, _boneRest[b].angleDeg)
        };
    }

    private Vector2 SkinnedGoal(int i)
    {
        var s = _skin[i];
        var ra = _bones[s.boneA];
        var rb = _bones[s.boneB];

        Vector2 gA = ra.position + Rotate(s.offsetA, ra.rotation * Mathf.Deg2Rad);
        Vector2 gB = rb.position + Rotate(s.offsetB, rb.rotation * Mathf.Deg2Rad);
        return Vector2.Lerp(gA, gB, s.weight);
    }

    private static Vector2[] BuildUVs(Sprite sprite, Rect o, Vector2[] rest)
    {
        var tex = sprite.texture;
        var r = sprite.rect;

        Vector2 spriteUvMin = new Vector2(r.xMin / tex.width, r.yMin / tex.height);
        Vector2 spriteUvSize = new Vector2(r.width / tex.width, r.height / tex.height);
        Vector2 uvMin = spriteUvMin + new Vector2(o.xMin * spriteUvSize.x, o.yMin * spriteUvSize.y);
        Vector2 uvSize = new Vector2(o.width * spriteUvSize.x, o.height * spriteUvSize.y);

        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        for (int i = 0; i < rest.Length; i++)
        {
            min = Vector2.Min(min, rest[i]);
            max = Vector2.Max(max, rest[i]);
        }
        Vector2 size = max - min;
        if (size.x < 1e-5f) size.x = 1f;
        if (size.y < 1e-5f) size.y = 1f;

        var uvs = new Vector2[rest.Length];
        for (int i = 0; i < rest.Length; i++)
        {
            float nx = (rest[i].x - min.x) / size.x;
            float ny = (rest[i].y - min.y) / size.y;
            uvs[i] = uvMin + new Vector2(nx * uvSize.x, ny * uvSize.y);
        }
        return uvs;
    }

    private static int[] BuildTriangles(int cols, int rows)
    {
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
        return tris.ToArray();
    }

    private Color[] SolidColors(Color c)
    {
        var colors = new Color[VertexCount];
        for (int i = 0; i < colors.Length; i++) colors[i] = c;
        return colors;
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
        for (int row = 0; row < _rows; row++)
        {
            for (int col = 0; col < _cols; col++)
            {
                int i = row * _cols + col;
                if (col < _cols - 1) AddDistance(i, i + 1, structuralStiffness);
                if (row < _rows - 1) AddDistance(i, i + _cols, structuralStiffness);
                if (col < _cols - 1 && row < _rows - 1)
                {
                    AddDistance(i, i + _cols + 1, shearStiffness);
                    AddDistance(i + 1, i + _cols, shearStiffness);
                }
            }
        }
    }

    private void AddDistance(int a, int b, float stiffness) =>
        _distances.Add(new DistanceConstraint { a = a, b = b, stiffness = stiffness });

    private void BuildBoundary()
    {
        var loop = new List<int>(2 * (_cols + _rows) - 4);
        for (int col = 0; col < _cols; col++) loop.Add(col);
        for (int row = 1; row < _rows; row++) loop.Add(row * _cols + (_cols - 1));
        for (int col = _cols - 2; col >= 0; col--) loop.Add((_rows - 1) * _cols + col);
        for (int row = _rows - 2; row >= 1; row--) loop.Add(row * _cols);
        _boundary = loop.ToArray();
    }

    private void Simulate(float dt)
    {
        if (_pos == null || dt <= 0f) return;

        int vc = VertexCount;

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

    private static Vector2 InverseRotate(Vector2 v, float angleDeg) => Rotate(v, -angleDeg * Mathf.Deg2Rad);

    private void OnDrawGizmosSelected()
    {
        if (_pos == null) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < VertexCount; i++)
            Gizmos.DrawSphere(_pos[i], 0.02f);
    }
}
