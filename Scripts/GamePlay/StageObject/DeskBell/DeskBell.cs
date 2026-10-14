using DG.Tweening;
using UnityEngine;

public class DeskBell : MonoBehaviour, ISquishHittable
{
    [Header("흔들림 대상")]
    [Tooltip("좌우로 흔들릴 가운데 벨 Transform")]
    [SerializeField] private Transform bellBody;

    [Header("흔들림")]
    [Tooltip("좌우로 기우는 최대 각도(도)")]
    [SerializeField] private float swayAngle = 10f;
    [Tooltip("한 번 울렸을 때 흔들림이 잦아들기까지 걸리는 시간(초)")]
    [SerializeField] private float swayDuration = 0.5f;
    [Tooltip("감쇠 진동 횟수. 클수록 여러 번 잘게 흔들린다")]
    [SerializeField] private int swayVibrato = 8;

    [Header("사운드")]
    [Tooltip("종을 칠 때 재생할 종소리 클립")]
    [SerializeField] private AudioClip ringClip;

    [Header("재생 제한")]
    [Tooltip("연속 충돌로 인한 연타를 막는 최소 재생 간격(초)")]
    [SerializeField] private float ringCooldown = 1f;

    private Vector3 _bodyOriginRotation;
    private float _lastRingTime = float.NegativeInfinity;

    private void Awake()
    {
        if (bellBody != null)
            _bodyOriginRotation = bellBody.localEulerAngles;
    }

    public void OnSquishHit(Collision2D collision) => RingBell();

    // 종을 울린다: 가운데 벨을 좌우로 흔들고 사운드를 재생한다. 쿨다운 중이면 무시한다.
    public void RingBell()
    {
        if (Time.time - _lastRingTime < ringCooldown)
            return;

        _lastRingTime = Time.time;

        PlaySwayAnimation();
        PlayRingSound();
    }

    private void PlaySwayAnimation()
    {
        if (bellBody == null) return;

        // 이전 흔들림이 남아 있으면 정리하고 원점부터 다시 친다.
        bellBody.DOKill();
        bellBody.localEulerAngles = _bodyOriginRotation;

        // Z축으로 좌우로 감쇠하며 흔들려 종이 쳐진 느낌을 준다.
        bellBody.DOPunchRotation(new Vector3(0f, 0f, swayAngle), swayDuration, swayVibrato)
            .SetLink(bellBody.gameObject);
    }

    private void PlayRingSound()
    {
        if (ringClip == null) return;

        if (SoundManager.TryGetInstance(out var sound))
            sound.PlaySfx(ringClip);
    }
}
