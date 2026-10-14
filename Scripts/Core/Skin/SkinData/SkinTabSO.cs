using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkinTab", menuName = "ButterPop/Skin/Skin Tab")]
public class SkinTabSO : ScriptableObject
{
    public SkinCategory category;
    [Tooltip("이 카테고리의 기본 장착 스킨. isFree와 무관하게 항상 보유로 취급한다.")]
    public SkinDataSO defaultSkin;
    public List<SkinDataSO> skins = new();

    public int Count => skins.Count;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (defaultSkin == null)
            Debug.LogError($"[SkinTabSO] {category} 탭에 기본 스킨이 지정되지 않았습니다", this);
        else if (!skins.Contains(defaultSkin))
            Debug.LogError($"[SkinTabSO] {category} 탭의 기본 스킨이 목록에 없습니다: {defaultSkin.name}", this);
    }
#endif

    public bool IsDefault(SkinDataSO skin) => skin != null && skin == defaultSkin;
}
