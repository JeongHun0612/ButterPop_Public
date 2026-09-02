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

    [Header("스퀴시 On/Off")]
    [Tooltip("메시의 출렁임·눌림만 전환한다. 꺼도 뼈의 굽힘과 타격음은 유지된다. 런타임 변경 가능.")]
    [SerializeField] private bool squishEnabled = true;

    [Header("스퀴시 복원 / 형상")]
    [Tooltip("목표 형상으로 돌아가는 강도. 높을수록 복원이 빠르다.")]
    [Range(0f, 1f)] [SerializeField] private float followStiffness = 0.08f;
    [Range(0f, 1f)] [SerializeField] private float structuralStiffness = 0.65f;
    [Range(0f, 1f)] [SerializeField] private float shearStiffness = 0.25f;
    [Tooltip("외곽의 2D 면적을 보존하는 강도.")]
    [Range(0f, 1f)] [SerializeField] private float pressureStiffness = 0.85f;
    [Min(0.01f)] [SerializeField] private float inflate = 1f;

    [Header("스퀴시 시뮬레이션")]
    [Range(1, 16)] [SerializeField] private int constraintIterations = 6;
    [Tooltip("충돌 후 남는 관성을 줄이는 비율. 높을수록 잔진동이 빨리 사라진다.")]
    [Range(0f, 1f)] [SerializeField] private float velocityDamping = 0.25f;
    [Tooltip("뼈를 따르는 목표 정점에서 벗어날 수 있는 최대 월드 거리.")]
    [Min(0f)] [SerializeField] private float maxDeformation = 0.08f;

    [Header("스퀴시 충돌 반응")]
    [Min(0f)] [SerializeField] private float impulseScale = 0.5f;
    [Tooltip("충돌이 주변 정점에 전달되는 월드 반경. 성긴 띠 메시의 정점 간격을 고려해 설정한다.")]
    [Min(0.01f)] [SerializeField] private float impulseRadius = 0.18f;
    [Tooltip("한 물리 스텝에서 정점별로 누적할 수 있는 충격 속도의 최댓값.")]
    [Min(0f)] [SerializeField] private float maxImpulse = 6f;

    private Mesh _mesh;
    private Material _material;
    private GameObject _visualGo;
    private Vector3[] _vertices;

    private IReadOnlyList<Rigidbody2D> _bones;
    private float _halfLength;
    private float _halfWidth;
    private Color _tint = Color.white;

    // 시뮬레이션은 월드 좌표, 메시 출력은 기존 자식 Transform의 로컬 좌표.
    private Vector2[] _positions;
    private Vector2[] _previousPositions;
    private Vector2[] _goals;
    private Vector2[] _previousGoals;
    private Vector2[] _renderGoals;
    private Vector2[] _pendingKicks;
    private Vector2[] _areaGradients;
    private int[] _boundary;
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
        ClearPendingKicks();
        if (_visualGo != null) _visualGo.SetActive(false);
    }

    private void FixedUpdate() => Simulate(Time.fixedDeltaTime);

    private void LateUpdate()
    {
        if (_mesh == null) return;
        // Inspector 변경도 코드 API와 동일하게 잔여 관성을 초기화한다.
        if (_wasSquishEnabled != squishEnabled) ResetSimulation();
        WriteMesh();
    }

    public void SetTint(Color color)
    {
        _tint = color;
        if (_mesh != null)
            _mesh.colors = SolidColors(_vertices.Length, _tint);
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
        _mesh.uv = BuildRingUVs(_bones.Count + 1);
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

        int rings = _bones.Count + 1;
        int vertexCount = rings * 2;

        _vertices = new Vector3[vertexCount];
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
        _mesh.colors = SolidColors(vertexCount, _tint);
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

        int rings = _bones.Count + 1;
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
        return bone.position + axis * (localX - rest.position.x);
    }

    private float RingLocalX(int ring)
    {
        int rings = _bones.Count + 1;
        float t = rings > 1 ? ring / (float)(rings - 1) : 0f;
        return Mathf.Lerp(-_halfLength, _halfLength, t);
    }

    private void BuildSimulation()
    {
        int count = _vertices.Length;
        _positions = new Vector2[count];
        _previousPositions = new Vector2[count];
        _goals = new Vector2[count];
        _previousGoals = new Vector2[count];
        _renderGoals = new Vector2[count];
        _pendingKicks = new Vector2[count];
        _areaGradients = new Vector2[count];
        _boundary = new int[count];

        int rings = count / 2;
        for (int r = 0; r < rings; r++)
        {
            _boundary[r] = r * 2;
            _boundary[rings + r] = (rings - 1 - r) * 2 + 1;
        }
    }

    private void ResetSimulation()
    {
        if (!UpdateGoals(_goals, true)) return;
        SnapToGoals();
    }

    private void SnapToGoals()
    {
        for (int i = 0; i < _positions.Length; i++)
        {
            _positions[i] = _goals[i];
            _previousPositions[i] = _goals[i];
            _previousGoals[i] = _goals[i];
        }
        _wasSquishEnabled = squishEnabled;
        ClearPendingKicks();
    }

    private void ClearPendingKicks()
    {
        if (_pendingKicks != null)
            System.Array.Clear(_pendingKicks, 0, _pendingKicks.Length);
    }

    private void Simulate(float dt)
    {
        if (_positions == null || dt <= 0f || !UpdateGoals(_goals, true)) return;
        StepSimulation(dt);
    }

    // 목표 형상 갱신과 분리해 정점 상태만 진행한다.
    private void StepSimulation(float dt)
    {
        if (!squishEnabled || _wasSquishEnabled != squishEnabled)
        {
            SnapToGoals();
            return;
        }

        // 리스폰 등으로 목표가 순간이동하면 모든 정점을 함께 초기화한다.
        float teleportDistance = Mathf.Max(1f, maxDeformation * 2f);
        for (int i = 0; i < _positions.Length; i++)
        {
            if ((_goals[i] - _previousGoals[i]).sqrMagnitude > teleportDistance * teleportDistance)
            {
                SnapToGoals();
                return;
            }
        }

        float keep = 1f - Mathf.Clamp01(velocityDamping);
        for (int i = 0; i < _positions.Length; i++)
        {
            Vector2 velocity = (_positions[i] - _previousPositions[i]) * keep;
            _previousPositions[i] = _positions[i];
            _positions[i] += velocity + _pendingKicks[i] * dt;
            _previousGoals[i] = _goals[i];
        }
        ClearPendingKicks();

        int iterations = Mathf.Clamp(constraintIterations, 1, 16);
        for (int it = 0; it < iterations; it++)
        {
            ProjectDistances();
            float follow = Mathf.Clamp01(followStiffness);
            for (int i = 0; i < _positions.Length; i++)
                _positions[i] += follow * (_goals[i] - _positions[i]);
            ProjectPressure();
        }

        float limit = Mathf.Max(0f, maxDeformation);
        for (int i = 0; i < _positions.Length; i++)
        {
            if (!IsFinite(_positions[i]))
            {
                _positions[i] = _goals[i];
                _previousPositions[i] = _goals[i];
                continue;
            }

            Vector2 offset = _positions[i] - _goals[i];
            if (offset.sqrMagnitude > limit * limit)
            {
                _positions[i] = _goals[i] + Vector2.ClampMagnitude(offset, limit);
                _previousPositions[i] = _positions[i];
            }
        }
    }

    private void ProjectDistances()
    {
        float structural = Mathf.Clamp01(structuralStiffness);
        float shear = Mathf.Clamp01(shearStiffness);
        for (int v = 0; v < _positions.Length; v += 2)
        {
            ProjectDistance(v, v + 1, structural);
            if (v + 2 >= _positions.Length) continue;
            ProjectDistance(v, v + 2, structural);
            ProjectDistance(v + 1, v + 3, structural);
            ProjectDistance(v, v + 3, shear);
            ProjectDistance(v + 1, v + 2, shear);
        }
    }

    private void ProjectDistance(int a, int b, float stiffness)
    {
        Vector2 delta = _positions[b] - _positions[a];
        float length = delta.magnitude;
        if (length < 0.00001f) return;
        float rest = (_goals[b] - _goals[a]).magnitude;
        Vector2 correction = 0.5f * stiffness * ((length - rest) / length) * delta;
        _positions[a] += correction;
        _positions[b] -= correction;
    }

    private void ProjectPressure()
    {
        if (pressureStiffness <= 0f) return;
        float targetArea = SignedArea(_goals) * Mathf.Max(0.01f, inflate);
        // 극단적인 굽힘으로 목표 외곽이 퇴화하면 압력 보정을 생략한다.
        if (Mathf.Abs(targetArea) < 0.00001f) return;
        float error = SignedArea(_positions) - targetArea;
        float sumGradientSquared = 0f;
        int count = _boundary.Length;
        for (int k = 0; k < count; k++)
        {
            Vector2 prev = _positions[_boundary[(k - 1 + count) % count]];
            Vector2 next = _positions[_boundary[(k + 1) % count]];
            Vector2 gradient = 0.5f * new Vector2(next.y - prev.y, prev.x - next.x);
            _areaGradients[k] = gradient;
            sumGradientSquared += gradient.sqrMagnitude;
        }
        if (sumGradientSquared < 0.00000001f) return;

        float correction = Mathf.Clamp01(pressureStiffness) * error / sumGradientSquared;
        for (int k = 0; k < count; k++)
            _positions[_boundary[k]] -= correction * _areaGradients[k];
    }

    private float SignedArea(Vector2[] points)
    {
        float sum = 0f;
        Vector2 origin = points[_boundary[0]];
        for (int k = 0; k < _boundary.Length; k++)
        {
            Vector2 a = points[_boundary[k]] - origin;
            Vector2 b = points[_boundary[(k + 1) % _boundary.Length]] - origin;
            sum += a.x * b.y - b.x * a.y;
        }
        return sum * 0.5f;
    }

    private void QueueImpulse(Vector2 point, Vector2 normal, float impact)
    {
        if (!squishEnabled || _positions == null || !IsFinite(point) || !IsFinite(normal)
            || float.IsNaN(impact) || float.IsInfinity(impact)) return;

        float cap = Mathf.Max(0f, maxImpulse);
        float kick = Mathf.Clamp(impact * impulseScale, 0f, cap);
        if (kick <= 0f) return;
        float radius = Mathf.Max(0.01f, impulseRadius);
        float invRadiusSquared = 1f / (radius * radius);
        Vector2 direction = normal.normalized;
        for (int i = 0; i < _positions.Length; i++)
        {
            float distanceSquared = (_positions[i] - point).sqrMagnitude;
            float weight = Mathf.Exp(-distanceSquared * invRadiusSquared);
            _pendingKicks[i] = Vector2.ClampMagnitude(_pendingKicks[i] + direction * (kick * weight), cap);
        }
    }

    private void WriteMesh()
    {
        if (_mesh == null || !UpdateGoals(_renderGoals, false)) return;
        for (int i = 0; i < _vertices.Length; i++)
        {
            // 최신 렌더 포즈 위에 물리 스텝에서 계산한 변형만 더한다.
            Vector2 offset = squishEnabled ? _positions[i] - _goals[i] : Vector2.zero;
            Vector3 local = transform.InverseTransformPoint(_renderGoals[i] + offset);
            local.z = 0f;
            _vertices[i] = local;
        }
        _mesh.vertices = _vertices;
        _mesh.RecalculateBounds();
    }

    private static bool IsFinite(Vector2 value) =>
        !float.IsNaN(value.x) && !float.IsNaN(value.y)
        && !float.IsInfinity(value.x) && !float.IsInfinity(value.y);

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

    private void OnSegmentCollision(Vector2 point, Vector2 normal, float impact) =>
        QueueImpulse(point, normal, impact);
}
