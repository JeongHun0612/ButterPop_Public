using UnityEngine;

// 스퀴시가 부딪히면 사운드만 재생하는 범용 컴포넌트. 흔들림 등 별도 연출이 없는 오브젝트에 붙인다.
public class SquishHitSound : MonoBehaviour, ISquishHittable
{
    [Header("사운드")]
    [Tooltip("충돌 시 재생할 클립. 여러 개를 넣으면 매번 무작위로 하나를 고른다")]
    [SerializeField] private AudioClip[] hitClips;

    [Header("재생 제한")]
    [Tooltip("연속 충돌로 소리가 겹치는 것을 막는 최소 재생 간격(초)")]
    [SerializeField] private float playCooldown = 0.5f;

    private float _lastPlayTime = float.NegativeInfinity;

    public void OnSquishHit(Collision2D collision) => PlayHitSound();

    // 충돌음을 재생한다. 쿨다운 중이거나 클립이 없으면 무시한다.
    public void PlayHitSound()
    {
        if (Time.time - _lastPlayTime < playCooldown) return;
        if (hitClips == null || hitClips.Length == 0) return;

        var clip = hitClips[Random.Range(0, hitClips.Length)];
        if (clip == null) return;

        if (!SoundManager.TryGetInstance(out var sound)) return;

        _lastPlayTime = Time.time;
        sound.PlaySfx(clip);
    }
}
