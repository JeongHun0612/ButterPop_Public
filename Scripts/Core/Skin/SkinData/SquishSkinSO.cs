using UnityEngine;

[CreateAssetMenu(fileName = "SquishSkin", menuName = "BananaGallery/Squish/Squish Skin")]
public class SquishSkinSO : SkinDataSO
{
    [Header("비주얼")]
    public Sprite sprite;

    [Header("타격 사운드 (ASMR)")]
    [Tooltip("충돌 시 SoundManager로 재생할 클립.")]
    public AudioClip hitClip;
}
