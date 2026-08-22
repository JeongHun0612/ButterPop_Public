using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class PurchaseService : IPurchaseService
{
    private readonly ISaveService _save;
    private PurchaseSaveData Data => _save.Data.purchase;

    public event Action OnPurchasesUpdated;

    public PurchaseService(ISaveService save) => _save = save;

    public bool RemoveBannerAds => Data.bannerRemoveAds || Data.allRemoveAds;
    public bool RemoveAllAds => Data.allRemoveAds;
    public bool AllSkinsUnlocked => Data.allRemoveAds;

    public async UniTask InitializeAsync()
    {
        // TODO(Unity IAP): 초기화 후 스토어에서 보유 상품 복원
        await RestoreAsync();
    }

    public async UniTask<bool> PurchaseAsync(string productId)
    {
        await UniTask.CompletedTask;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 임시 즉시승인: 스토어 연동 전 개발빌드 한정으로 즉시 권한을 지급한다.
        // 릴리즈 빌드에는 이 분기가 컴파일되지 않아 공짜 지급이 새지 않는다.
        bool applied = ApplyEntitlement(productId);
        if (applied) Debug.Log($"[PurchaseService] 개발빌드 즉시승인: {productId}");
        return applied;
#else
        // TODO(Unity IAP): 실 구매 플로우. 성공 시 ApplyEntitlement + 즉시 저장.
        Debug.LogWarning($"[PurchaseService] IAP 미연동 - 구매 무시: {productId}");
        return false;
#endif
    }

    public async UniTask RestoreAsync()
    {
        // TODO(Unity IAP): 스토어에서 보유 상품 조회 → ApplyEntitlement
        await UniTask.CompletedTask;
    }

    // 권한을 적용했으면 true, 알 수 없는 상품이면 false.
    private bool ApplyEntitlement(string productId)
    {
        switch (productId)
        {
            case ProductIds.RemoveBannerAds:
                Data.bannerRemoveAds = true;
                break;
            case ProductIds.RemoveAllAds:
                Data.allRemoveAds = true;
                break;
            default:
                Debug.LogWarning($"[PurchaseService] 알 수 없는 상품 ID: {productId}");
                return false;
        }

        _save.Save();
        OnPurchasesUpdated?.Invoke();
        Debug.Log($"[PurchaseService] 권한 적용: {productId}");
        return true;
    }
}
