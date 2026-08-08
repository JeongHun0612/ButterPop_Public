public interface ISkinService
{
    bool IsOwned(SkinDataSO skin);
    bool IsEquipped(SkinCategory category, SkinDataSO skin);
    void Equip(SkinCategory category, SkinDataSO skin);
    void Grant(SkinDataSO skin);
}