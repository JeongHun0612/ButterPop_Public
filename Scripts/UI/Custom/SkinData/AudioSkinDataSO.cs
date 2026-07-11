using UnityEngine;

[CreateAssetMenu(fileName = "AudioSkinData", menuName = "BananaGallery/Skin/Audio Skin Data")]
public class AudioSkinDataSO : SkinDataSO
{
    [Header("미리듣기")]
    [SerializeField] private AudioClip previewClip;

    public override AudioClip PreviewClip => previewClip;
}
