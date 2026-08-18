using System.Collections.Generic;
using UnityEngine;

// SquishBody 뼈 체인 위에 스키닝되는 스퀴시 메쉬.
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

    [Header("렌더 정렬")]
    [Tooltip("비주얼 메쉬의 Sorting Layer. Handle 등 다른 SpriteRenderer와 같은 축으로 비교된다.")]
    [SerializeField] private string sortingLayerName = "Default";
    [Tooltip("Order in Layer. Handle Sprite보다 크게 주면 위로 올라온다.")]
    [SerializeField] private int sortingOrder = 10;

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
    public string SortingLayerName => sortingLayerName;
    public int SortingOrder => sortingOrder;
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
        if (!body.IsReady || body.BoneRigidbodies.Count < 2 || body.BoneRestSamples.Count < 2)
        {
            Debug.LogError("[SquishMesh] SquishBody가 준비되지 않았습니다. 뼈를 굽고 프리팹 구성을 확인하세요.");
            enabled = false;
            return;
        }

        _bones = body.BoneRigidbodies;
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
            _visualGo = visualRenderer.gameObject;
            filter = _visualGo.GetComponent<MeshFilter>();
            if (filter == null) filter = _visualGo.AddComponent<MeshFilter>();
            renderer = visualRenderer;
        }
        else
        {
            _visualGo = new GameObject("SquishVisual");
            filter = _visualGo.AddComponent<MeshFilter>();
            renderer = _visualGo.AddComponent<MeshRenderer>();
        }

        // 정점은 월드 좌표로 쓰므로 렌더러 Transform은 원점에 둔다.
        _visualGo.transform.SetParent(null);
        _visualGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        _visualGo.transform.localScale = Vector3.one;

        ApplyMaterial(renderer, sprite);

        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = sortingOrder;

        BuildRestGeometry(body.Length, body.Width, _cols, _rows, sprite, ResolveOpaqueUVRect(),
            out _verts, out var uvs, out var tris);

        int vc = VertexCount;
        _skin = new Skin[vc];
        _pos = new Vector2[vc];
        _prevPos = new Vector2[vc];
        _goal = new Vector2[vc];
        _prevGoal = new Vector2[vc];

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

    // 프리팹 재질이 있으면 텍스처만 현재 스킨으로 교체한다.
    private void ApplyMaterial(MeshRenderer renderer, Sprite sprite)
    {
        if (renderer.sharedMaterial != null)
        {
            renderer.material.mainTexture = sprite.texture;
            return;
        }
        renderer.sharedMaterial = CreateMaterial(sprite);
    }

    // 런타임 메쉬와 에디터 프리뷰가 공유하는 rest 직사각형 지오메트리.
    public static void BuildRestGeometry(float length, float width, int cols, int rows,
        Sprite sprite, Rect opaqueUVRect,
        out Vector3[] verts, out Vector2[] uvs, out int[] tris)
    {
        cols = Mathf.Max(cols, 2);
        rows = Mathf.Max(rows, 2);
        int vc = cols * rows;

        float hx = length * 0.5f;
        float hy = width * 0.5f;
        ComputeUVRect(sprite, opaqueUVRect, out Vector2 uvMin, out Vector2 uvSize);

        verts = new Vector3[vc];
        uvs = new Vector2[vc];

        for (int col = 0; col < cols; col++)
        {
            float fu = col / (float)(cols - 1);
            float x = Mathf.Lerp(-hx, hx, fu);
            for (int row = 0; row < rows; row++)
            {
                float fv = row / (float)(rows - 1);
                float y = Mathf.Lerp(-hy, hy, fv);
                int i = row * cols + col;
                verts[i] = new Vector3(x, y, 0f);
                uvs[i] = uvMin + new Vector2(fu * uvSize.x, fv * uvSize.y);
            }
        }

        tris = BuildTriangles(cols, rows);
    }

    public static Mesh BuildPreviewMesh(float length, float width, int cols, int rows,
        Sprite sprite, Rect opaqueUVRect)
    {
        BuildRestGeometry(length, width, cols, rows, sprite, opaqueUVRect,
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
            offsetA = rest - _boneRest[a].position,
            offsetB = rest - _boneRest[b].position
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

    // 스프라이트 내부 불투명 영역을 텍스처 UV로 변환한다.
    private static void ComputeUVRect(Sprite sprite, Rect o, out Vector2 uvMin, out Vector2 uvSize)
    {
        var tex = sprite.texture;
        var r = sprite.rect;

        Vector2 spriteUvMin = new Vector2(r.xMin / tex.width, r.yMin / tex.height);
        Vector2 spriteUvSize = new Vector2(r.width / tex.width, r.height / tex.height);
        uvMin = spriteUvMin + new Vector2(o.xMin * spriteUvSize.x, o.yMin * spriteUvSize.y);
        uvSize = new Vector2(o.width * spriteUvSize.x, o.height * spriteUvSize.y);
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

    private void OnDrawGizmosSelected()
    {
        if (_pos == null) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < VertexCount; i++)
            Gizmos.DrawSphere(_pos[i], 0.02f);
    }
}
