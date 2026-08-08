using UnityEngine;

public class SkinService : ISkinService
{
    private readonly ISaveService _save;

    private SkinSaveData Data => _save.Data.skin;

    public SkinService(ISaveService save) => _save = save;

    public bool IsOwned(SkinDataSO skin) => Data.ownedSkinIds.Contains(skin.id);

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
