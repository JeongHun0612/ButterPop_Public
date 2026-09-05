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

    // 디버그 해금 빌드는 구매 세이브를 바꾸지 않고 실행 중 권한만 열어 준다.
    public bool RemoveBannerAds => BuildFlags.DebugUnlock || Data.bannerRemoveAds || Data.allRemoveAds;
    public bool RemoveAllAds => BuildFlags.DebugUnlock || Data.allRemoveAds;
    public bool AllSkinsUnlocked => BuildFlags.DebugUnlock || Data.allRemoveAds;

    // 스토어 실패가 게임 진행을 막지 않도록 예외를 삼킨다.
    public async UniTask InitializeAsync()
    {
        try
        {
            await _provider.InitializeAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"[PurchaseService] 스토어 초기화 실패: {e.Message}");
            return;
        }

        await RestoreAsync();
    }

    public async UniTask<PurchaseState> PurchaseAsync(string productId)
    {
        PurchaseState state;
        try
        {
            state = await _provider.PurchaseAsync(productId);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PurchaseService] 구매 처리 중 예외: {e.Message}");
            state = PurchaseState.Failed;
        }

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
        try
        {
            await _provider.RestoreAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"[PurchaseService] 복원 실패: {e.Message}");
            return;
        }

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
