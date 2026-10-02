using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class PurchaseService : IPurchaseService
{
    private readonly ISaveService _save;
    private readonly IIapProvider _provider;

    private UniTask _restoreTask = UniTask.CompletedTask;   // 진행 중이거나 마지막으로 끝난 복원
    private int _purchaseGrantCount;                         // 결제로 권한을 지급한 횟수. 복원 도중 결제를 감지한다.

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
    // 상품 조회가 실패해도 미결 주문 복구·환불 회수는 해야 하므로 복원은 계속한다.
    public async UniTask InitializeAsync()
    {
        try
        {
            await _provider.InitializeAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"[PurchaseService] 스토어 초기화 실패: {e.Message}");
        }

        await RestoreAsync();
    }

    public async UniTask<PurchaseState> PurchaseAsync(string productId)
    {
        PurchaseState state;
        try
        {
            state = await _provider.PurchaseAsync(productId, GrantImmediately);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PurchaseService] 구매 처리 중 예외: {e.Message}");
            state = PurchaseState.Failed;
        }

        if (state == PurchaseState.Success)
        {
            _purchaseGrantCount++;
            if (ApplyEntitlement(productId)) CommitEntitlements();
        }
        else
        {
            Debug.Log($"[PurchaseService] 구매 미완료({state}): {productId}");
        }

        return state;
    }

    // 스토어 보유 목록으로 세이브 권한을 복원·회수한다. 조회 실패 시 세이브를 유지한다.
    // 부팅 복원과 복원 버튼이 겹치면 스토어 보유 목록을 서로 덮어써 권한을 잘못 회수하므로, 진행 중인 복원을 함께 기다린다.
    public UniTask RestoreAsync()
    {
        if (_restoreTask.Status == UniTaskStatus.Pending) return _restoreTask;

        _restoreTask = RestoreCoreAsync().Preserve();
        return _restoreTask;
    }

    public bool TryGetPrice(string productId, out string price)
        => _provider.TryGetLocalizedPrice(productId, out price);

    private async UniTask RestoreCoreAsync()
    {
        int grantCountAtStart = _purchaseGrantCount;
        try
        {
            await _provider.RestoreAsync(GrantRecoveredAsync);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PurchaseService] 복원 실패: {e.Message}");
            return;
        }

        if (!_provider.TryGetOwnedProductIds(out var owned) || owned == null)
        {
            Debug.Log("[PurchaseService] 보유 조회 미확인 - 세이브 권한 유지");
            return;
        }

        // 복원 도중 결제가 끝났으면 그 상품이 조회 결과에 아직 없을 수 있어 이번에는 회수하지 않는다.
        bool allowRevoke = grantCountAtStart == _purchaseGrantCount;
        if (!allowRevoke) Debug.Log("[PurchaseService] 복원 중 결제가 완료되어 이번 동기화에서는 권한을 회수하지 않습니다");

        bool changed = false;
        changed |= SyncEntitlement(ref Data.bannerRemoveAds, ProductIds.RemoveBannerAds, owned, allowRevoke);
        changed |= SyncEntitlement(ref Data.allRemoveAds, ProductIds.RemoveAllAds, owned, allowRevoke);

        var revoked = _provider.GetRevokedProductIds();
        if (allowRevoke && revoked != null)
        {
            for (int i = 0; i < revoked.Count; i++)
                changed |= RevokeEntitlement(revoked[i]);
        }

        if (changed) CommitEntitlements();
    }

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

    // Apps in Toss의 ProcessProductGrant는 결제 오버레이가 player loop를 멈춘 상태에서
    // 동기로 호출된다. 권한은 즉시 메모리에 반영하고 저장은 큐에 예약한다.
    // 반환값이 토스에 지급 결과로 전달되므로 통지보다 먼저 확정한다. 여기서 예외가 새면
    // SDK가 false로 응답해 결제됐는데 환불 안내가 뜬다.
    private bool GrantImmediately(string productId)
    {
        _purchaseGrantCount++;
        bool changed = ApplyEntitlement(productId);
        bool granted = HasEntitlement(productId);
        if (changed) CommitEntitlements();
        return granted;
    }

    // 미결 주문 복구는 오버레이 밖에서 실행되므로 영속 저장 성공까지 기다린 뒤
    // 프로바이더가 토스에 지급 완료를 통지할 수 있게 한다.
    private async UniTask<bool> GrantRecoveredAsync(string productId)
    {
        bool changed = ApplyEntitlement(productId);
        if (!HasEntitlement(productId)) return false;

        bool saved = await _save.SaveAsync();
        if (changed && saved) NotifyPurchasesUpdated();
        return saved;
    }

    private bool HasEntitlement(string productId)
    {
        return productId switch
        {
            ProductIds.RemoveBannerAds => Data.bannerRemoveAds || Data.allRemoveAds,
            ProductIds.RemoveAllAds => Data.allRemoveAds,
            _ => false
        };
    }

    private bool RevokeEntitlement(string productId)
    {
        switch (productId)
        {
            case ProductIds.RemoveBannerAds:
                if (!Data.bannerRemoveAds) return false;
                Data.bannerRemoveAds = false;
                break;
            case ProductIds.RemoveAllAds:
                if (!Data.allRemoveAds) return false;
                Data.allRemoveAds = false;
                break;
            default:
                Debug.LogWarning($"[PurchaseService] 알 수 없는 회수 상품 ID: {productId}");
                return false;
        }

        Debug.Log($"[PurchaseService] 환불 권한 회수: {productId}");
        return true;
    }

    // 플래그를 스토어 보유 여부에 맞춘다. allowRevoke가 false면 켜기만 한다. 바뀌었으면 true.
    private static bool SyncEntitlement(ref bool flag, string productId, IReadOnlyList<string> owned, bool allowRevoke)
    {
        bool isOwned = false;
        for (int i = 0; i < owned.Count; i++)
        {
            if (owned[i] != productId) continue;
            isOwned = true;
            break;
        }

        if (flag == isOwned) return false;
        if (!isOwned && !allowRevoke) return false;

        flag = isOwned;
        if (isOwned) Debug.Log($"[PurchaseService] 권한 복원: {productId}");
        else Debug.LogWarning($"[PurchaseService] 스토어 미보유 - 권한 회수: {productId}");
        return true;
    }

    // 권한 변경을 저장하고 구독자에게 알린다. 지급 응답·복원 흐름이 끊기지 않도록 예외를 밖으로 내지 않는다.
    private void CommitEntitlements()
    {
        try
        {
            _save.Save();
        }
        catch (Exception e)
        {
            Debug.LogError($"[PurchaseService] 권한 저장 예약 실패: {e}");
        }

        NotifyPurchasesUpdated();
    }

    // 구독자 하나의 예외가 다른 구독자(배너 숨김 등)의 통지를 막지 않도록 하나씩 호출한다.
    private void NotifyPurchasesUpdated()
    {
        var handlers = OnPurchasesUpdated;
        if (handlers == null) return;

        foreach (Action handler in handlers.GetInvocationList())
        {
            try
            {
                handler();
            }
            catch (Exception e)
            {
                Debug.LogError($"[PurchaseService] 구매 상태 구독자 예외: {e}");
            }
        }
    }
}
