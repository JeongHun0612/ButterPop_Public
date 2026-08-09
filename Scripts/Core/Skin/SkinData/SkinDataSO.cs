using UnityEngine;

[CreateAssetMenu(fileName = "SkinData", menuName = "BananaGallery/Skin/Skin Data")]
public class SkinDataSO : ScriptableObject
{
    [Header("식별")]
    public string id;

    [Header("표시 정보")]
    public string displayName;
    public Sprite icon;
    [TextArea] public string description;

    [Header("가격")]
    public int price;
}
