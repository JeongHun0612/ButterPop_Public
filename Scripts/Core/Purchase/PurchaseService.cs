using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class PurchaseService : IPurchaseService
{
    private readonly ISaveService _save;
    private PurchaseSaveData Data => _save.Data.purchase;

    public event Action OnPurchasesUpdated;

    public PurchaseService(ISaveService save) => _save = save;

    public bool RemoveAds => Data.removeAds;

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
        if (!Data.purchasedProductIds.Contains(productId))
            Data.purchasedProductIds.Add(productId);

        if (productId == "remove_ads") Data.removeAds = true;

        _save.Save();
        OnPurchasesUpdated?.Invoke();
        Debug.Log($"[PurchaseService] 권한 적용: {productId}");
    }
}
