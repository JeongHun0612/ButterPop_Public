using UnityEngine;

public class EmergencyButton : MonoBehaviour, IBananaHittable
{
    [Header("사운드")]
    [Tooltip("버튼을 누를 때 재생할 딸깍 클립")]
    [SerializeField] private AudioClip pressClip;

    [Header("재생 제한")]
    [Tooltip("연속 충돌로 인한 연타를 막는 최소 재생 간격(초)")]
    [SerializeField] private float pressCooldown = 0.5f;

    private float _lastPressTime = float.NegativeInfinity;

    public void OnBananaHit(Collision2D collision) => PressButton();

    // 버튼을 누른다: 딸깍 소리를 한 번 재생한다. 쿨다운 중이면 무시한다.
    public void PressButton()
    {
        if (Time.time - _lastPressTime < pressCooldown)
            return;

        _lastPressTime = Time.time;

        PlayPressSound();
    }

    private void PlayPressSound()
    {
        if (pressClip == null) return;

        if (SoundManager.TryGetInstance(out var sound))
            sound.PlaySfx(pressClip);
    }
}
