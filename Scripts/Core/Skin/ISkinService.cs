public interface ISkinService
{
    bool IsOwned(SkinTabSO tab, SkinDataSO skin);
    bool IsEquipped(SkinCategory category, SkinDataSO skin);
    SkinDataSO ResolveEquipped(SkinTabSO tab);
    void Equip(SkinCategory category, SkinDataSO skin);
}