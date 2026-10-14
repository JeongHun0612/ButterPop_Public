using System;
using Cysharp.Threading.Tasks;

public interface IPurchaseService
{
    bool RemoveBannerAds { get; }   // 배너 제거 (배너 광고 제거 구매)
    bool RemoveAllAds { get; }      // 전체 광고 제거 (전체 광고 제거 구매)
    bool AllSkinsUnlocked { get; }  // 올스킨 언락 (전체 광고 제거 구매)
    UniTask InitializeAsync();
    UniTask<PurchaseState> PurchaseAsync(string productId);
    UniTask RestoreAsync();                                 // 보유 상품 재조회(애플 심사 필수)
    bool TryGetPrice(string productId, out string price);   // 스토어 표기 가격
    event Action OnPurchasesUpdated;
}
