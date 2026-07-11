using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkinTab", menuName = "BananaGallery/Skin/Skin Tab")]
public class SkinTabSO : ScriptableObject
{
    public SkinCategory category;
    public List<SkinDataSO> skins = new();

    public int Count => skins.Count;
}
