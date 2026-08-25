using System.Collections.Generic;
using UnityEngine;

// SquishBody 뼈 체인을 하나의 띠 메시로 그리는 렌더러.
// 스킨(SquishSkinSO)의 sprite로 그리고, 충돌 시 hitClip을 재생한다(ASMR).
[RequireComponent(typeof(SquishBody))]
public class SquishSimpleMesh : MonoBehaviour
{
    // 여러 세그먼트가 동시에 충돌해도 소리는 한 번만 (중복 재생 방지).
    private const float MinHitInterval = 0.3f;

    [Header("참조")]
    [SerializeField] private SquishBody body;
    [SerializeField] private SquishSkinSO skin;

    [Header("렌더 정렬")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 10;

    [Header("타격 사운드")]
    [Tooltip("이 충돌 속도 이하는 무음 처리한다(미세 접촉 클릭 방지).")]
    [SerializeField] private float minImpactVelocity = 5f;
    [Tooltip("이 충돌 속도에서 최대 볼륨에 도달한다.")]
    [SerializeField] private float maxImpactVelocity = 20f;
    [Tooltip("minImpactVelocity 지점의 볼륨.")]
    [SerializeField] private float minVolume = 0.2f;
    [Tooltip("maxImpactVelocity 이상에서의 볼륨.")]
    [SerializeField] private float maxVolume = 1f;

    private Mesh _mesh;
    private Material _material;
    private GameObject _visualGo;
    private Vector3[] _vertices;

    private IReadOnlyList<Rigidbody2D> _bones;
    private float _halfLength;
    private float _halfWidth;
    private Color _tint = Color.white;
    private float _lastHitTime = -999f;

    private void Awake()
    {
        if (body == null)
            body = GetComponent<SquishBody>();

        if (skin == null)
        {
            var existingMesh = GetComponent<SquishMesh>();
            if (existingMesh != null)
                skin = existingMesh.SkinSO;
        }

        if (body == null || skin == null || skin.sprite == null)
        {
            Debug.LogError("[SquishSimpleMesh] body / skin sprite가 지정되지 않았습니다.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        if (!enabled) return;
        if (!body.IsReady || body.BoneRigidbodies.Count < 2 || body.BoneRestSamples.Count < 2)
        {
            Debug.LogError("[SquishSimpleMesh] SquishBody가 준비되지 않았습니다.", this);
            enabled = false;
            return;
        }

        _bones = body.BoneRigidbodies;
        _halfLength = body.Length * 0.5f;
        _halfWidth = body.Width * 0.5f;

        BuildMesh();

        body.OnSegmentCollision += OnSegmentCollision;
    }

    private void LateUpdate()
    {
        if (_mesh == null || !UpdateVertices()) return;

        _mesh.vertices = _vertices;
        _mesh.RecalculateBounds();
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

        if (_mesh == null || !UpdateVertices()) return;

        _mesh.vertices = _vertices;
        _mesh.RecalculateBounds();
    }

    public void ApplySkin(SquishSkinSO newSkin)
    {
        if (newSkin == null || newSkin.sprite == null)
        {
            Debug.LogWarning("[SquishSimpleMesh] 스프라이트가 없는 스킨은 적용할 수 없습니다.", this);
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
        _visualGo = new GameObject("SquishSimpleVisual");
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

        UpdateVertices();

        _mesh = new Mesh { name = "SquishSimpleMesh", hideFlags = HideFlags.DontSave };
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

    private bool UpdateVertices()
    {
        if (_bones == null || _vertices == null) return false;

        int rings = _bones.Count + 1;
        for (int r = 0; r < rings; r++)
        {
            if (!RingPose(r, out Vector2 center, out Vector2 side))
                return false;

            Vector3 localCenter = transform.InverseTransformPoint(center);
            Vector3 localSide = transform.InverseTransformDirection(side.normalized) * _halfWidth;
            localCenter.z = 0f;
            localSide.z = 0f;

            _vertices[r * 2] = localCenter - localSide;
            _vertices[r * 2 + 1] = localCenter + localSide;
        }

        return true;
    }

    private bool RingPose(int ring, out Vector2 center, out Vector2 side)
    {
        center = Vector2.zero;
        side = Vector2.up;

        Rigidbody2D before = ring > 0 ? _bones[ring - 1] : null;
        Rigidbody2D after = ring < _bones.Count ? _bones[ring] : null;

        if (ring > 0 && before == null) return false;
        if (ring < _bones.Count && after == null) return false;

        if (before == null)
        {
            center = RingPoint(after, 0, -_halfLength);
            side = after.transform.up;
        }
        else if (after == null)
        {
            center = RingPoint(before, _bones.Count - 1, _halfLength);
            side = before.transform.up;
        }
        else
        {
            float x = RingLocalX(ring);
            Vector2 a = RingPoint(before, ring - 1, x);
            Vector2 b = RingPoint(after, ring, x);
            center = (a + b) * 0.5f;

            Vector2 averagedSide = (Vector2)before.transform.up + (Vector2)after.transform.up;
            side = averagedSide.sqrMagnitude > 0.0001f ? averagedSide.normalized : before.transform.up;
        }

        return true;
    }

    private Vector2 RingPoint(Rigidbody2D bone, int boneIndex, float localX)
    {
        SpineSample rest = body.BoneRestSamples[boneIndex];
        return bone.position + (Vector2)bone.transform.right * (localX - rest.position.x);
    }

    private float RingLocalX(int ring)
    {
        int rings = _bones.Count + 1;
        float t = rings > 1 ? ring / (float)(rings - 1) : 0f;
        return Mathf.Lerp(-_halfLength, _halfLength, t);
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
        if (body != null)
            body.OnSegmentCollision -= OnSegmentCollision;

        if (_visualGo != null)
            Destroy(_visualGo);

        if (_material != null)
            Destroy(_material);

        if (_mesh != null)
            Destroy(_mesh);
    }

    // 세그먼트 충돌 시 스킨의 타격음(여러 개면 랜덤) 재생. 충돌 세기(impact = 상대 속도)에 비례해 볼륨을 조절한다.
    private void OnSegmentCollision(Vector2 point, Vector2 normal, float impact)
    {
        if (skin == null) return;
        if (impact < minImpactVelocity) return;            // 미세 접촉은 무음
        if (Time.time - _lastHitTime < MinHitInterval) return;

        var clip = skin.GetRandomHitClip();
        if (clip == null) return;
        if (!SoundManager.TryGetInstance(out var sound)) return;

        float t = Mathf.InverseLerp(minImpactVelocity, maxImpactVelocity, impact);
        float volume = Mathf.Lerp(minVolume, maxVolume, t);

        _lastHitTime = Time.time;
        sound.PlaySfx(clip, volume);
    }
}
