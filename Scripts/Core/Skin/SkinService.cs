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

    // 전체 광고 제거(올스킨) 구매 시 모든 스킨을 보유한 것으로 취급한다.
    public bool IsOwned(SkinDataSO skin) => _purchase.AllSkinsUnlocked || Data.ownedSkinIds.Contains(skin.id);

    public bool IsEquipped(SkinCategory category, SkinDataSO skin) =>
        Data.equipped.Exists(e => e.category == category && e.skinId == skin.id);

    public void Equip(SkinCategory category, SkinDataSO skin)
    {
        var entry = Data.equipped.Find(e => e.category == category);
        if (entry != null)
            entry.skinId = skin.id;
        else
            Data.equipped.Add(new EquippedEntry { category = category, skinId = skin.id });
        _save.Save();
    }

    public void Grant(SkinDataSO skin)
    {
        if (Data.ownedSkinIds.Contains(skin.id)) return;
        Data.ownedSkinIds.Add(skin.id);
        _save.Save();
    }
}
