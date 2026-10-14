using UnityEngine;

public class SkinService : ISkinService
{
    private readonly ISaveService _save;
    private readonly IPurchaseService _purchase;

    private SkinSaveData Data => _save.Data.skin;

    public SkinService(ISaveService save, IPurchaseService purchase)
    {
        _save = save;
        _purchase = purchase;
    }

    // 전체 광고 제거(올스킨) 구매, 무료 스킨, 카테고리 기본 스킨이면 보유.
    public bool IsOwned(SkinTabSO tab, SkinDataSO skin) =>
        _purchase.AllSkinsUnlocked || skin.isFree || (tab != null && tab.IsDefault(skin));

    public bool IsEquipped(SkinCategory category, SkinDataSO skin) =>
        Data.equipped.Exists(e => e.category == category && e.skinId == skin.id);

    // 장착 스킨을 반환한다. 장착 정보가 없거나 탭에 없거나 미보유 스킨이면 기본 스킨으로 확정해 저장한다.
    public SkinDataSO ResolveEquipped(SkinTabSO tab)
    {
        if (tab == null) return null;

        var entry = Data.equipped.Find(e => e.category == tab.category);
        if (entry != null)
        {
            var equipped = tab.skins.Find(s => s != null && s.id == entry.skinId);
            if (equipped != null && IsOwned(tab, equipped)) return equipped;
        }

        if (tab.defaultSkin == null) return null;

        Equip(tab.category, tab.defaultSkin);
        return tab.defaultSkin;
    }

    public void Equip(SkinCategory category, SkinDataSO skin)
    {
        var entry = Data.equipped.Find(e => e.category == category);
        if (entry != null)
            entry.skinId = skin.id;
        else
            Data.equipped.Add(new EquippedEntry { category = category, skinId = skin.id });
        _save.Save();
    }
}
