using UnityEngine;

// 스퀴시 오브젝트의 타격 사운드(ASMR). SquishBody.OnSegmentCollision을 구독해
// SquishSkinSO.hitClip을 SoundManager로 재생한다. 실제 출력은 SoundManager가 담당.
// - 물리는 SquishBody 공유, 스킨(SquishSkinSO)만 바꾸면 타격음이 교체된다.
public class SquishSound : MonoBehaviour
{
    // 한 번의 착지에서 여러 세그먼트가 동시에 충돌해도 소리는 한 번만 (중복 재생 방지).
    private const float MinInterval = 0.05f;

    [Header("참조")]
    [SerializeField] private SquishBody body;
    [Tooltip("타격음/비주얼 스킨. SquishMesh와 같은 SO를 물린다.")]
    [SerializeField] private SquishSkinSO skin;

    private float _lastPlayTime = -999f;

    public SquishSkinSO Skin
    {
        get => skin;
        set => skin = value;
    }

    private void Awake()
    {
        if (body == null) body = GetComponent<SquishBody>();
    }

    private void Start()
    {
        if (body == null)
        {
            Debug.LogError("[SquishSound] SquishBody가 없습니다.");
            enabled = false;
            return;
        }

        body.OnSegmentCollision += OnSegmentCollision;
    }

    private void OnDestroy()
    {
        if (body != null)
            body.OnSegmentCollision -= OnSegmentCollision;
    }

    // SquishBody 세그먼트 충돌 → 스킨 클립을 SoundManager로 재생.
    private void OnSegmentCollision(Vector2 point, Vector2 normal, float impact)
    {
        if (skin == null || skin.hitClip == null) return;
        if (Time.time - _lastPlayTime < MinInterval) return;
        if (!SoundManager.TryGetInstance(out var sound)) return;

        _lastPlayTime = Time.time;
        sound.PlaySfx(skin.hitClip);
    }
}
