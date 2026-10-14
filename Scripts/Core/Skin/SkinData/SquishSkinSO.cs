using UnityEngine;

[CreateAssetMenu(fileName = "SquishSkin", menuName = "ButterPop/Squish/Squish Skin")]
public class SquishSkinSO : SkinDataSO
{
    [Header("비주얼")]
    public Sprite sprite;

    [Header("타격 사운드 (ASMR)")]
    [Tooltip("충돌 시 SoundManager로 재생할 클립. 여러 개면 매 타격마다 랜덤으로 하나 재생한다.")]
    public AudioClip[] hitClips;

    // 직전에 고른 인덱스(연속 중복 재생 방지용 런타임 상태).
    private int _lastHitIndex = -1;

    // 등록된 타격음 중 하나를 랜덤으로 반환한다(가능하면 직전과 다른 클립). 없으면 null.
    public AudioClip GetRandomHitClip()
    {
        if (hitClips == null || hitClips.Length == 0) return null;
        if (hitClips.Length == 1) return hitClips[0];

        int index = Random.Range(0, hitClips.Length);
        if (index == _lastHitIndex)
            index = (index + 1) % hitClips.Length; // 직전과 겹치면 다음 것으로

        _lastHitIndex = index;
        return hitClips[index];
    }
}
