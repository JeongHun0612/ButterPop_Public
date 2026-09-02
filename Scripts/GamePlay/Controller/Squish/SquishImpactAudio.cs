using UnityEngine;

// 메시/변형의 활성 상태와 독립적으로 SquishBody의 충돌음을 재생한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(SquishBody))]
public class SquishImpactAudio : MonoBehaviour
{
    // 여러 세그먼트가 동시에 충돌해도 소리는 한 번만 재생한다.
    private const float MinHitInterval = 0.3f;

    [Header("참조")]
    [SerializeField] private SquishBody body;
    [SerializeField] private SquishSkinSO skin;

    [Header("타격 사운드")]
    [Tooltip("이 충돌 속도 미만은 무음 처리한다(미세 접촉 클릭 방지).")]
    [SerializeField] private float minImpactVelocity = 5f;
    [Tooltip("이 충돌 속도에서 최대 볼륨에 도달한다.")]
    [SerializeField] private float maxImpactVelocity = 20f;
    [Tooltip("minImpactVelocity 지점의 볼륨.")]
    [SerializeField] private float minVolume = 0.2f;
    [Tooltip("maxImpactVelocity 이상에서의 볼륨.")]
    [SerializeField] private float maxVolume = 1f;

    private float _lastHitTime = -999f;
    private SquishBody _subscribedBody;

    private void Awake()
    {
        if (body == null) body = GetComponent<SquishBody>();
    }

    private void OnEnable()
    {
        if (body == null || _subscribedBody != null) return;
        body.OnSegmentCollision += OnSegmentCollision;
        _subscribedBody = body;
    }

    private void OnDisable() => UnsubscribeCollision();

    private void OnDestroy() => UnsubscribeCollision();

    private void UnsubscribeCollision()
    {
        if (_subscribedBody != null)
            _subscribedBody.OnSegmentCollision -= OnSegmentCollision;
        _subscribedBody = null;
    }

    // 비활성 상태에서도 스킨을 갱신하되 컴포넌트를 자동으로 켜지 않는다.
    public void ApplySkin(SquishSkinSO newSkin) => skin = newSkin;

    public void ResetCooldown() => _lastHitTime = -999f;

    // impact는 물리 임펄스가 아닌 상대 충돌 속도다.
    private void OnSegmentCollision(Vector2 point, Vector2 normal, float impact)
    {
        if (skin == null) return;
        if (impact < minImpactVelocity) return;
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
