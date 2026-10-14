using System.Collections.Generic;
using UnityEngine;

// SquishBody 뼈 체인을 하나의 띠 메시로 그리는 렌더러.
// 스킨의 sprite를 그리고, 충돌 시 메시의 눌림과 출렁임을 계산한다.
[RequireComponent(typeof(SquishBody))]
public class SquishMesh : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private SquishBody body;
    [SerializeField] private SquishSkinSO skin;

    [Header("렌더 정렬")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 10;

    [Header("표면 분할")]
    [Tooltip("뼈 구간당 표면 분할 수. 높이면 눌림의 윤곽이 매끄러워진다. 플레이 시작 시 반영된다.")]
    [Range(1, 4)][SerializeField] private int surfaceSubdivisions = 2;

    [Header("스퀴시 On/Off")]
    [Tooltip("메시의 출렁임·눌림만 전환한다. 꺼도 뼈의 굽힘과 타격음은 유지된다. 런타임 변경 가능.")]
    [SerializeField] private bool squishEnabled = true;

    [Header("버터 스퀴시")]
    [Tooltip("높일수록 쉽게 눌리고 변형 범위가 커진다. 0이면 변형하지 않는다.")]
    [Range(0f, 1f)] [SerializeField] private float softness = 0.8f;
    [Tooltip("한 번 눌린 뒤 약 95% 복원되는 기준 시간(초). 낮을수록 빠르다. 잔반동에 따라 실제 시간은 조금 달라진다.")]
    [Range(0.05f, 1f)] [SerializeField] private float recoveryTime = 0.18f;
    [Tooltip("0이면 반동 없이 복원한다. 높일수록 원래 형태를 지나쳐 잠깐 출렁인다.")]
    [Range(0f, 1f)] [SerializeField] private float rebound = 0f;
    [Tooltip("충돌 순간 눌림의 강도. 0이면 새 충돌에 반응하지 않는다. 최대 변형은 말랑함에 따라 제한된다.")]
    [Range(0f, 2f)] [SerializeField] private float impactStrength = 1f;

    private Mesh _mesh;
    private Material _material;
    private GameObject _visualGo;
    private Vector3[] _vertices;
    private Color[] _colors;
    private MeshRenderer _renderer;

    private IReadOnlyList<Rigidbody2D> _bones;
    private float _halfLength;
    private float _halfWidth;
    private Color _tint = Color.white;

    private SquishDeformationSolver _solver;
    private int _subdivisions;
    private int _ringCount;
    // 목표 정점은 월드 좌표. Solver의 상대 변형을 더한 뒤 로컬 메시로 출력한다.
    private Vector2[] _goals;
    private Vector2[] _renderGoals;
    private Vector2[] _offsets;
    private bool _wasSquishEnabled;
    private bool _subscribed;

    public bool SquishEnabled
    {
        get => squishEnabled;
        set => SetSquishEnabled(value);
    }

    public void SetSquishEnabled(bool value)
    {
        squishEnabled = value;
        if (_mesh == null || _wasSquishEnabled == value) return;
        ResetSimulation();
        WriteMesh();
    }

    private void Awake()
    {
        if (body == null)
            body = GetComponent<SquishBody>();

        if (body == null || skin == null || skin.sprite == null)
        {
            Debug.LogError("[SquishMesh] body / skin sprite가 지정되지 않았습니다.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        if (!enabled) return;
        if (!body.IsReady || body.BoneRigidbodies.Count < 2 || body.BoneRestSamples.Count < 2)
        {
            Debug.LogError("[SquishMesh] SquishBody가 준비되지 않았습니다.", this);
            enabled = false;
            return;
        }

        _bones = body.BoneRigidbodies;
        _halfLength = body.Length * 0.5f;
        _halfWidth = body.Width * 0.5f;
        _subdivisions = Mathf.Clamp(surfaceSubdivisions, 1, 4);
        _ringCount = _bones.Count * _subdivisions + 1;

        BuildMesh();
        BuildSimulation();
        ResetSimulation();
        WriteMesh();
        SubscribeCollision();
    }

    private void OnEnable()
    {
        if (_mesh == null) return; // 최초 초기화는 Start에서 한다.
        ResetSimulation();
        WriteMesh();
        _visualGo.SetActive(true);
        SubscribeCollision();
    }

    private void OnDisable()
    {
        UnsubscribeCollision();
        _solver?.Reset();
        if (_visualGo != null) _visualGo.SetActive(false);
    }

    private void FixedUpdate() => Simulate(Time.fixedDeltaTime);

    private void LateUpdate()
    {
        if (_mesh == null) return;
        // Inspector 변경도 코드 API와 동일하게 잔여 관성을 초기화한다.
        if (_wasSquishEnabled != squishEnabled) ResetSimulation();
        ConfigureSolver();
        WriteMesh();
    }

    public void SetTint(Color color)
    {
        _tint = color;
        if (_mesh == null) return;
        for (int i = 0; i < _colors.Length; i++) _colors[i] = color;
        _mesh.colors = _colors;
    }

    public void ResetShape()
    {
        SetTint(Color.white);
        ResetSimulation();
        WriteMesh();
    }

    public void ApplySkin(SquishSkinSO newSkin)
    {
        if (newSkin == null || newSkin.sprite == null)
        {
            Debug.LogWarning("[SquishMesh] 스프라이트가 없는 스킨은 적용할 수 없습니다.", this);
            return;
        }

        if (newSkin == skin) return;

        skin = newSkin;
        enabled = true; // Awake에서 skin 미지정으로 비활성화됐을 수 있으니 복구.

        if (_mesh == null || _material == null) return;

        _material.mainTexture = skin.sprite.texture;
        _mesh.uv = BuildRingUVs(_ringCount);
    }

    private void BuildMesh()
    {
        _visualGo = new GameObject("SquishVisual");
        _visualGo.transform.SetParent(transform, false);
        _visualGo.transform.localPosition = Vector3.zero;
        _visualGo.transform.localRotation = Quaternion.identity;
        _visualGo.transform.localScale = Vector3.one;

        var filter = _visualGo.AddComponent<MeshFilter>();
        var renderer = _visualGo.AddComponent<MeshRenderer>();
        _renderer = renderer;

        int rings = _ringCount;
        int vertexCount = rings * 2;

        _vertices = new Vector3[vertexCount];
        _colors = SolidColors(vertexCount, _tint);
        var uvs = BuildRingUVs(rings);
        var normals = new Vector3[vertexCount];
        var triangles = new int[(rings - 1) * 6];

        for (int r = 0; r < rings; r++)
        {
            normals[r * 2] = Vector3.back;
            normals[r * 2 + 1] = Vector3.back;
        }

        for (int i = 0; i < rings - 1; i++)
        {
            int v = i * 2;
            triangles[i * 6 + 0] = v;
            triangles[i * 6 + 1] = v + 1;
            triangles[i * 6 + 2] = v + 2;
            triangles[i * 6 + 3] = v + 1;
            triangles[i * 6 + 4] = v + 3;
            triangles[i * 6 + 5] = v + 2;
        }

        _mesh = new Mesh { name = "SquishMesh", hideFlags = HideFlags.DontSave };
        _mesh.MarkDynamic();
        _mesh.vertices = _vertices;
        _mesh.uv = uvs;
        _mesh.colors = _colors;
        _mesh.normals = normals;
        _mesh.triangles = triangles;
        _mesh.RecalculateBounds();

        filter.sharedMesh = _mesh;

        _material = CreateSpriteMaterial(skin.sprite.texture);
        renderer.sharedMaterial = _material;
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private bool UpdateGoals(Vector2[] goals, bool physicsPose)
    {
        if (_bones == null || goals == null) return false;

        int rings = _ringCount;
        for (int r = 0; r < rings; r++)
        {
            if (!RingPose(r, physicsPose, out Vector2 center, out Vector2 side))
                return false;

            Vector3 localCenter = transform.InverseTransformPoint(center);
            Vector3 localSide = transform.InverseTransformDirection(side.normalized) * _halfWidth;
            localCenter.z = 0f;
            localSide.z = 0f;

            // 기존 Simple의 폭/Transform 스케일 처리를 그대로 기준 형상으로 사용한다.
            goals[r * 2] = transform.TransformPoint(localCenter - localSide);
            goals[r * 2 + 1] = transform.TransformPoint(localCenter + localSide);
        }

        return true;
    }

    private bool RingPose(int ring, bool physicsPose, out Vector2 center, out Vector2 side)
    {
        int before = ring / _subdivisions;
        int after = Mathf.Min(before + 1, _bones.Count);
        float t = (ring % _subdivisions) / (float)_subdivisions;
        if (!BaseRingPose(before, physicsPose, out center, out side)) return false;
        if (t == 0f) return true;
        if (!BaseRingPose(after, physicsPose, out Vector2 nextCenter, out Vector2 nextSide)) return false;
        center = Vector2.Lerp(center, nextCenter, t);
        Vector2 blended = Vector2.Lerp(side, nextSide, t);
        if (blended.sqrMagnitude > 0.0001f) side = blended.normalized;
        return true;
    }

    private bool BaseRingPose(int ring, bool physicsPose, out Vector2 center, out Vector2 side)
    {
        center = Vector2.zero;
        side = Vector2.up;

        Rigidbody2D before = ring > 0 ? _bones[ring - 1] : null;
        Rigidbody2D after = ring < _bones.Count ? _bones[ring] : null;

        if (ring > 0 && before == null) return false;
        if (ring < _bones.Count && after == null) return false;

        if (before == null)
        {
            center = RingPoint(after, 0, -_halfLength, physicsPose);
            side = BoneSide(after, physicsPose);
        }
        else if (after == null)
        {
            center = RingPoint(before, _bones.Count - 1, _halfLength, physicsPose);
            side = BoneSide(before, physicsPose);
        }
        else
        {
            float x = RingLocalX(ring);
            Vector2 a = RingPoint(before, ring - 1, x, physicsPose);
            Vector2 b = RingPoint(after, ring, x, physicsPose);
            center = (a + b) * 0.5f;

            Vector2 beforeSide = BoneSide(before, physicsPose);
            Vector2 averagedSide = beforeSide + BoneSide(after, physicsPose);
            side = averagedSide.sqrMagnitude > 0.0001f ? averagedSide.normalized : beforeSide;
        }

        return true;
    }

    private static Vector2 BoneSide(Rigidbody2D bone, bool physicsPose)
    {
        if (!physicsPose) return bone.transform.up;
        float angle = bone.rotation * Mathf.Deg2Rad;
        return new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
    }

    private Vector2 RingPoint(Rigidbody2D bone, int boneIndex, float localX, bool physicsPose)
    {
        SpineSample rest = body.BoneRestSamples[boneIndex];
        Vector2 side = BoneSide(bone, physicsPose);
        Vector2 axis = new Vector2(side.y, -side.x);
        Vector2 position = physicsPose ? bone.position : (Vector2)bone.transform.position;
        return position + axis * (localX - rest.position.x);
    }

    private float RingLocalX(int ring)
    {
        int rings = _bones.Count + 1;
        float t = rings > 1 ? ring / (float)(rings - 1) : 0f;
        return Mathf.Lerp(-_halfLength, _halfLength, t);
    }

    private void BuildSimulation()
    {
        _goals = new Vector2[_vertices.Length];
        _renderGoals = new Vector2[_vertices.Length];
        _offsets = new Vector2[_vertices.Length];
        _solver = new SquishDeformationSolver(_ringCount);
        ConfigureSolver();
    }

    private void ConfigureSolver() => _solver?.Configure(softness, recoveryTime, rebound, impactStrength);

    private void ResetSimulation()
    {
        _solver?.Reset();
        _wasSquishEnabled = squishEnabled;
    }

    private void Simulate(float dt)
    {
        if (_solver == null) return;
        if (!squishEnabled || _wasSquishEnabled != squishEnabled)
        {
            ResetSimulation();
            return;
        }
        ConfigureSolver();
        _solver.Step(dt);
    }

    private void WriteMesh()
    {
        if (_mesh == null || !UpdateGoals(_renderGoals, false)) return;
        if (squishEnabled)
        {
            float alpha = Time.fixedDeltaTime > 0f
                ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1f;
            _solver.WriteOffsets(_renderGoals, _offsets, alpha);
        }

        for (int i = 0; i < _vertices.Length; i++)
        {
            Vector2 point = _renderGoals[i] + (squishEnabled ? _offsets[i] : Vector2.zero);
            Vector3 local = transform.InverseTransformPoint(point);
            local.z = 0f;
            _vertices[i] = local;
        }
        _mesh.vertices = _vertices;
        _mesh.RecalculateBounds();
        _renderer.sortingLayerName = sortingLayerName;
        _renderer.sortingOrder = sortingOrder;
    }

    private void SubscribeCollision()
    {
        if (_subscribed || body == null) return;
        body.OnSegmentCollision += OnSegmentCollision;
        _subscribed = true;
    }

    private void UnsubscribeCollision()
    {
        if (!_subscribed) return;
        if (body != null) body.OnSegmentCollision -= OnSegmentCollision;
        _subscribed = false;
    }

    // 링(rings) 개수에 맞춰 띠 메시의 UV를 스킨 스프라이트 기준으로 계산한다.
    private Vector2[] BuildRingUVs(int rings)
    {
        var uvs = new Vector2[rings * 2];
        ComputeUVRect(skin.sprite, out Vector2 uvMin, out Vector2 uvSize);
        for (int r = 0; r < rings; r++)
        {
            float along = rings > 1 ? r / (float)(rings - 1) : 0f;
            uvs[r * 2] = uvMin + new Vector2(along * uvSize.x, 0f);
            uvs[r * 2 + 1] = uvMin + new Vector2(along * uvSize.x, uvSize.y);
        }
        return uvs;
    }

    private static void ComputeUVRect(Sprite sprite, out Vector2 uvMin, out Vector2 uvSize)
    {
        Texture2D tex = sprite.texture;
        Rect rect = sprite.rect;

        uvMin = new Vector2(rect.xMin / tex.width, rect.yMin / tex.height);
        uvSize = new Vector2(rect.width / tex.width, rect.height / tex.height);
    }

    private static Color[] SolidColors(int count, Color color)
    {
        var colors = new Color[count];
        for (int i = 0; i < count; i++)
            colors[i] = color;
        return colors;
    }

    private static Material CreateSpriteMaterial(Texture texture)
    {
        var probe = new GameObject("SpriteMaterialProbe") { hideFlags = HideFlags.HideAndDontSave };
        Material source = probe.AddComponent<SpriteRenderer>().sharedMaterial;

        Material material = source != null ? new Material(source) : new Material(FallbackShader());
        Destroy(probe);

        material.hideFlags = HideFlags.DontSave;
        material.mainTexture = texture;
        return material;
    }

    private static Shader FallbackShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        return shader;
    }

    private void OnDestroy()
    {
        UnsubscribeCollision();

        if (_visualGo != null)
            Destroy(_visualGo);

        if (_material != null)
            Destroy(_material);

        if (_mesh != null)
            Destroy(_mesh);
    }

    private void OnSegmentCollision(Vector2 point, Vector2 normal, float impact)
    {
        if (!squishEnabled || _solver == null) return;
        if (_wasSquishEnabled != squishEnabled) ResetSimulation();
        ConfigureSolver();
        if (UpdateGoals(_goals, true)) _solver.AddImpact(_goals, point, normal, impact);
    }
}
