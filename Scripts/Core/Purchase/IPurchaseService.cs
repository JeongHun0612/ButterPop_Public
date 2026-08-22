using System;
using Cysharp.Threading.Tasks;

public interface IPurchaseService
{
    bool RemoveBannerAds { get; }   // 배너 제거 (배너 또는 전체 광고 제거 구매)
    bool RemoveAllAds { get; }      // 전면 포함 전체 광고 제거 (전체 구매)
    bool AllSkinsUnlocked { get; }  // 올스킨 언락 (전체 구매)
    UniTask InitializeAsync();
    UniTask<bool> PurchaseAsync(string productId);
    UniTask RestoreAsync();
    event Action OnPurchasesUpdated;
}
