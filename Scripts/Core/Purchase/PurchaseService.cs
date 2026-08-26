using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class PurchaseService : IPurchaseService
{
    private readonly ISaveService _save;
    private readonly IIapProvider _provider;
    private PurchaseSaveData Data => _save.Data.purchase;

    public event Action OnPurchasesUpdated;

    public PurchaseService(ISaveService save, IIapProvider provider)
    {
        _save = save;
        _provider = provider;
    }

    public bool RemoveBannerAds => Data.bannerRemoveAds || Data.allRemoveAds;
    public bool RemoveAllAds => Data.allRemoveAds;
    public bool AllSkinsUnlocked => Data.allRemoveAds;

    public async UniTask InitializeAsync()
    {
        await _provider.InitializeAsync();
        await RestoreAsync();
    }

    public async UniTask<PurchaseState> PurchaseAsync(string productId)
    {
        PurchaseState state = await _provider.PurchaseAsync(productId);

        if (state == PurchaseState.Success)
        {
            if (ApplyEntitlement(productId)) CommitEntitlements();
        }
        else
        {
            Debug.Log($"[PurchaseService] 구매 미완료({state}): {productId}");
        }

        return state;
    }

    // 스토어 보유 목록을 세이브에 반영한다(재설치·기기 변경 복원).
    public async UniTask RestoreAsync()
    {
        await _provider.RestoreAsync();

        var owned = _provider.GetOwnedProductIds();
        if (owned == null) return;

        bool changed = false;
        for (int i = 0; i < owned.Count; i++)
            changed |= ApplyEntitlement(owned[i]);

        if (changed) CommitEntitlements();
    }

    public bool TryGetPrice(string productId, out string price)
        => _provider.TryGetLocalizedPrice(productId, out price);

    // 상품 ID에 해당하는 권한을 켠다. 이미 보유 중이면 false를 반환해 저장·통지 반복을 막는다.
    private bool ApplyEntitlement(string productId)
    {
        switch (productId)
        {
            case ProductIds.RemoveBannerAds:
                if (Data.bannerRemoveAds) return false;
                Data.bannerRemoveAds = true;
                break;
            case ProductIds.RemoveAllAds:
                if (Data.allRemoveAds) return false;
                Data.allRemoveAds = true;
                break;
            default:
                Debug.LogWarning($"[PurchaseService] 알 수 없는 상품 ID: {productId}");
                return false;
        }

        Debug.Log($"[PurchaseService] 권한 적용: {productId}");
        return true;
    }

    // 권한 변경을 저장하고 구독자에게 알린다.
    private void CommitEntitlements()
    {
        _save.Save();
        OnPurchasesUpdated?.Invoke();
    }
}
