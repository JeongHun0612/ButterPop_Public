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
        // TODO(Unity IAP): 구매 플로우. 성공 시 ApplyEntitlement + 즉시 저장
        await UniTask.CompletedTask;
        return false;
    }

    public async UniTask RestoreAsync()
    {
        // TODO(Unity IAP): 스토어에서 보유 상품 조회 → ApplyEntitlement
        await UniTask.CompletedTask;
    }

    private void ApplyEntitlement(string productId)
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
                return;
        }

        _save.Save();
        OnPurchasesUpdated?.Invoke();
        Debug.Log($"[PurchaseService] 권한 적용: {productId}");
    }
}
