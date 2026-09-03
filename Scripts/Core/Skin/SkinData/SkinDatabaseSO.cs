using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkinDatabase", menuName = "ButterPop/Skin/Skin Database")]
public class SkinDatabaseSO : ScriptableObject
{
    public List<SkinTabSO> tabs = new();

    public SkinTabSO GetTab(SkinCategory category) => tabs.Find(t => t.category == category);
}
