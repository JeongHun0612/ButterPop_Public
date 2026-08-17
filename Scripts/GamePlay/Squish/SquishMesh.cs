using System.Collections.Generic;
using UnityEngine;

// 스퀴시 오브젝트의 "살"(일반형). SquishBody 뼈 체인에 스파인 스윕 격자를 스키닝하고,
// 그 위에서 PBD(Verlet) + 면적보존 압력으로 눌리고 출렁이게 한다.
// - ButterSquishMesh를 임의 형태로 일반화: rest = 스파인 스윕, UV = 평면 매핑, 바인딩 = 호 파라미터 매칭.
// - 소프트바디 시뮬(구속 투영)은 ButterSquishMesh와 동일 — goal만 스파인 스윕으로 바뀐다.
public class SquishMesh : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private SquishBody body;
    [Tooltip("비주얼 스킨. 스프라이트·불투명영역을 여기서 우선 읽고, 없으면 Shape의 것을 사용(하위호환). SquishSound와 같은 SO를 물려 사용.")]
    [SerializeField] private SquishSkinSO skin;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 0;

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

    private void Awake()
    {
        if (body == null) body = GetComponent<SquishBody>();

        if (body == null || body.Shape == null || ResolveSprite() == null)
        {
            Debug.LogError("[SquishMesh] body / Shape / sprite(skin 또는 Shape)가 지정되지 않았습니다.");
            enabled = false;
        }
    }

    // 스프라이트/불투명영역은 skin 우선, 없으면 Shape 폴백(하위호환).
    private Sprite ResolveSprite()
        => (skin != null && skin.sprite != null) ? skin.sprite
            : (body != null && body.Shape != null) ? body.Shape.sprite : null;

    private Rect ResolveOpaqueUVRect()
        => (skin != null && skin.sprite != null) ? skin.opaqueUVRect
            : (body != null && body.Shape != null) ? body.Shape.opaqueUVRect : new Rect(0f, 0f, 1f, 1f);

    private void Start()
    {
        if (!enabled) return;

        _bones = body.Bones;
        _boneRest = body.BoneRestSamples;
        _cols = body.Shape.meshCols;
        _rows = body.Shape.meshRows;

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

    // 정점 컬러로 전체를 균일 틴트한다(Unlit 셰이더가 텍스처 × 정점컬러). 타는 연출 등에서 사용.
    // 정점 컬러는 시뮬/WriteMesh가 덮어쓰지 않으므로 한 번 설정하면 유지된다. 원복은 Color.white.
    public void SetTint(Color c)
    {
        if (_mesh == null) return;
        _mesh.colors = SolidColors(c);
    }

    private void BuildMesh()
    {
        var sprite = ResolveSprite();

        var meshGo = new GameObject("SquishMesh");
        meshGo.transform.SetParent(null);   // 월드 공간 렌더
        meshGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        meshGo.transform.localScale = Vector3.one;

        var filter = meshGo.AddComponent<MeshFilter>();
        var renderer = meshGo.AddComponent<MeshRenderer>();
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = sortingOrder;
        renderer.sharedMaterial = CreateMaterial(sprite);

        // 스파인을 메쉬 열 수로 리샘플링 → 스윕으로 rest 정점 생성.
        var cols = SpineSampler.Resample(body.Shape.controlPoints, _cols, body.Shape.smoothSpine);

        int vc = VertexCount;
        _skin = new Skin[vc];
        _pos = new Vector2[vc];
        _prevPos = new Vector2[vc];
        _goal = new Vector2[vc];
        _prevGoal = new Vector2[vc];
        _verts = new Vector3[vc];
        var rest = new Vector2[vc];

        for (int col = 0; col < _cols; col++)
        {
            var s = cols[col];
            Vector2 normal = new Vector2(-Mathf.Sin(s.angleDeg * Mathf.Deg2Rad), Mathf.Cos(s.angleDeg * Mathf.Deg2Rad));
            float fc = _cols > 1 ? col / (float)(_cols - 1) : 0f;

            for (int row = 0; row < _rows; row++)
            {
                float v = _rows > 1 ? Mathf.Lerp(-1f, 1f, row / (float)(_rows - 1)) : 0f;
                int i = row * _cols + col;

                rest[i] = s.position + normal * (v * s.halfWidth);
                _skin[i] = BindSkin(fc, rest[i]);
                _verts[i] = rest[i];
            }
        }

        _mesh = new Mesh { name = "SquishMesh" };
        _mesh.MarkDynamic();
        _mesh.vertices = _verts;
        _mesh.uv = BuildUVs(sprite, rest);
        _mesh.colors = SolidColors(Color.white);
        _mesh.triangles = BuildTriangles();
        _mesh.RecalculateBounds();
        filter.mesh = _mesh;
    }

    // rest 정점을 호 파라미터(fc)로 인접한 뼈 2개에 바인딩. 각 뼈 rest 프레임의 로컬 오프셋 저장.
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

    // UV: rest 위치의 bounding box를 스프라이트의 불투명(아트) 영역에 평면 매핑.
    // opaqueUVRect로 여백이 큰 스프라이트도 실제 그림 영역에만 정확히 정합된다.
    private Vector2[] BuildUVs(Sprite sprite, Vector2[] rest)
    {
        var tex = sprite.texture;
        var r = sprite.rect;
        Rect o = ResolveOpaqueUVRect();

        // 스프라이트 rect → 그 안의 불투명 서브영역 (텍스처 0~1).
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

    private int[] BuildTriangles()
    {
        var tris = new List<int>((_cols - 1) * (_rows - 1) * 6);
        for (int row = 0; row < _rows - 1; row++)
        {
            for (int col = 0; col < _cols - 1; col++)
            {
                int i0 = row * _cols + col;
                int i1 = i0 + 1;
                int i2 = i0 + _cols;
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
