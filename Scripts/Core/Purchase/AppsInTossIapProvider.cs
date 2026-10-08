#if APPS_IN_TOSS
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AppsInToss;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 앱인토스의 SKU 기반 일회성 결제를 기존 IIapProvider 계약에 연결한다.
// 콘솔 SKU는 AppsInTossServiceSettings로 게임 상품 ID와 변환하고, 이 클래스 밖으로는 게임 상품 ID만 내보낸다.
public sealed class AppsInTossIapProvider : IIapProvider
{
    private const float PurchaseTimeoutSeconds = 900f;
    private const int ApiTimeoutMs = 30000;

    private readonly List<string> _owned = new();
    private readonly List<string> _revoked = new();
    private readonly Dictionary<string, string> _prices = new();
    private bool _purchaseInProgress;
    private bool _ownedFetched;   // 이번 복원에서 결제 이력·미결 주문을 모두 조회했는지

    public async UniTask InitializeAsync()
    {
        var result = await AIT.IAPGetProductItemList(ApiTimeoutMs);
        _prices.Clear();

        if (result?.Products == null)
        {
            Debug.LogWarning("[AppsInTossIapProvider] 등록 상품을 조회하지 못했습니다.");
            return;
        }

        foreach (var product in result.Products)
        {
            if (product == null) continue;

            // 콘솔 SKU를 AppsInTossServiceSettings에 옮겨 적을 수 있도록 연결되지 않은 상품을 남긴다.
            if (!AppsInTossServiceSettings.TryGetProductId(product.Sku, out string productId))
            {
                Debug.LogWarning($"[AppsInTossIapProvider] 연결되지 않은 콘솔 상품: {product.Sku} ({product.DisplayName})");
                continue;
            }

            if (!string.IsNullOrEmpty(product.DisplayAmount)) _prices[productId] = product.DisplayAmount;
        }

        Debug.Log($"[AppsInTossIapProvider] 상품 조회 완료: 콘솔 {result.Products.Length}개 중 {_prices.Count}개 연결");
    }

    public async UniTask<PurchaseState> PurchaseAsync(string productId, Func<string, bool> processGrant)
    {
        if (_purchaseInProgress)
        {
            Debug.LogWarning($"[AppsInTossIapProvider] 이미 구매 진행 중: {productId}");
            return PurchaseState.Failed;
        }

        if (!AppsInTossServiceSettings.TryGetSku(productId, out string sku))
        {
            Debug.LogError($"[AppsInTossIapProvider] SKU가 설정되지 않은 상품: {productId}");
            return PurchaseState.Failed;
        }

        _purchaseInProgress = true;
        var audioPause = AudioPauseGate.Acquire();

        var source = new UniTaskCompletionSource<PurchaseState>();
        Action dispose = null;

        try
        {
            var options = new IapCreateOneTimePurchaseOrderOptionsOptions
            {
                Sku = sku,
                ProcessProductGrant = _ => processGrant?.Invoke(productId) == true
            };

            InstallErrorCodeHook();

            dispose = AIT.IAPCreateOneTimePurchaseOrder(
                onEvent: result =>
                {
                    if (result?.Type == "success")
                    {
                        AddOwned(_owned, _revoked, productId);
                        source.TrySetResult(PurchaseState.Success);
                    }
                    else
                    {
                        source.TrySetResult(PurchaseState.Failed);
                    }
                },
                options: options,
                onError: error =>
                {
                    if (AppsInTossPurchaseError.IsCancelled(error?.ErrorCode, error?.Message))
                    {
                        Debug.Log($"[AppsInTossIapProvider] 구매 취소({productId}): {error?.Message}");
                        source.TrySetResult(PurchaseState.Cancelled);
                        return;
                    }

                    // 승인되면 미결 주문으로 남아 다음 복원에서 지급된다.
                    if (AppsInTossPurchaseError.IsPending(error?.ErrorCode, error?.Message))
                    {
                        Debug.Log($"[AppsInTossIapProvider] 결제 승인 대기({productId}): {error?.Message}");
                        source.TrySetResult(PurchaseState.Pending);
                        return;
                    }

                    Debug.LogWarning($"[AppsInTossIapProvider] 구매 실패({productId}): {error?.ErrorCode} {error?.Message}");
                    source.TrySetResult(PurchaseState.Failed);
                });

            var (timedOut, state) = await source.Task
                .TimeoutWithoutException(TimeSpan.FromSeconds(PurchaseTimeoutSeconds));

            if (timedOut)
            {
                Debug.LogError($"[AppsInTossIapProvider] 구매 응답 시간 초과: {productId}");
                return PurchaseState.Failed;
            }

            return state;
        }
        finally
        {
            dispose?.Invoke();
            audioPause.Dispose();
            _purchaseInProgress = false;
        }
    }

    public async UniTask RestoreAsync(Func<string, UniTask<bool>> processGrant)
    {
        _ownedFetched = false;

        if (!AppsInTossServiceSettings.IsIapConfigured)
        {
            Debug.LogWarning("[AppsInTossIapProvider] 인앱결제 SKU가 설정되지 않아 복원을 건너뜁니다.");
            return;
        }

        // 복원이 겹쳐도 서로의 중간 결과를 지우지 않도록 지역 목록에 모은 뒤 마지막에 교체한다.
        var owned = new List<string>();
        var revoked = new List<string>();
        bool historyFetched = await TryReconcileCompletedAndRefundedAsync(owned, revoked);

        var pendingOrders = await TryGetPendingOrdersAsync();
        if (pendingOrders == null) return;

        // 결제가 끝난 주문이므로 지급·완료 통지 결과와 무관하게 보유로 본다.
        // 빠뜨리면 보유 동기화가 방금 복구한 권한을 회수한다.
        var pending = new List<(string OrderId, string ProductId)>();
        foreach (var order in pendingOrders)
        {
            if (order == null || !AppsInTossServiceSettings.TryGetProductId(order.Sku, out string productId)) continue;

            AddOwned(owned, revoked, productId);
            pending.Add((order.OrderId, productId));
        }

        _owned.Clear();
        _owned.AddRange(owned);
        _revoked.Clear();
        _revoked.AddRange(revoked);
        _ownedFetched = historyFetched;

        foreach (var (orderId, productId) in pending)
            await RecoverPendingOrderAsync(orderId, productId, processGrant);
    }

    // 조회 실패로 빈 목록을 넘기면 보유 동기화가 구매 권한을 회수하므로, 조회가 끝났을 때만 true.
    public bool TryGetOwnedProductIds(out IReadOnlyList<string> productIds)
    {
        productIds = _owned;
        return _ownedFetched;
    }

    public IReadOnlyList<string> GetRevokedProductIds() => _revoked;

    public bool TryGetLocalizedPrice(string productId, out string price)
        => _prices.TryGetValue(productId, out price);

    private static void AddOwned(List<string> owned, List<string> revoked, string productId)
    {
        if (!owned.Contains(productId)) owned.Add(productId);
        revoked.Remove(productId);
    }

    // 결제 이력을 끝까지 조회해 보유·회수 목록을 채웠으면 true.
    private static async UniTask<bool> TryReconcileCompletedAndRefundedAsync(List<string> owned, List<string> revoked)
    {
        CompletedOrRefundedOrdersResult history;
        try
        {
            history = await AIT.IAPGetCompletedOrRefundedOrders(ApiTimeoutMs);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AppsInTossIapProvider] 결제 이력 조회 실패: {e.Message}");
            return false;
        }

        if (history?.Orders == null)
        {
            Debug.LogWarning($"[AppsInTossIapProvider] 결제 이력 응답 없음: {history?.error}");
            return false;
        }

        // SDK가 다음 페이지 키를 받지 않아 첫 페이지(최대 50건)만 볼 수 있다. 나머지에 보유 주문이 있을 수 있어 미확인으로 둔다.
        if (history.HasNext)
        {
            Debug.LogWarning("[AppsInTossIapProvider] 결제 이력이 한 페이지를 넘어 보유 동기화를 건너뜁니다.");
            return false;
        }

        // 같은 상품에 여러 이력이 있으면 ISO 날짜가 가장 최신인 주문 상태를 사용한다.
        var latest = new Dictionary<string, CompletedOrRefundedOrdersResultOrder>();
        foreach (var order in history.Orders)
        {
            if (order == null || !AppsInTossServiceSettings.TryGetProductId(order.Sku, out string productId)) continue;
            if (!latest.TryGetValue(productId, out var previous)
                || string.CompareOrdinal(order.Date, previous.Date) > 0)
                latest[productId] = order;
        }

        foreach (var pair in latest)
        {
            if (pair.Value.Status == CompletedOrRefundedOrdersResultOrderStatus.COMPLETED)
                AddOwned(owned, revoked, pair.Key);
            else if (!revoked.Contains(pair.Key))
                revoked.Add(pair.Key);
        }

        return true;
    }

    // 조회에 실패하면 null.
    private static async UniTask<IAPGetPendingOrdersResultOrder[]> TryGetPendingOrdersAsync()
    {
        try
        {
            var result = await AIT.IAPGetPendingOrders(ApiTimeoutMs);
            if (result?.Orders == null)
                Debug.LogWarning($"[AppsInTossIapProvider] 미결 주문 응답 없음: {result?.error}");
            return result?.Orders;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AppsInTossIapProvider] 미결 주문 조회 실패: {e.Message}");
            return null;
        }
    }

    // 지급 저장이 끝난 주문만 완료 통지한다. 한 주문의 실패가 다른 주문 복구를 막지 않도록 예외를 여기서 끊는다.
    private static async UniTask RecoverPendingOrderAsync(string orderId, string productId, Func<string, UniTask<bool>> processGrant)
    {
        try
        {
            bool granted = processGrant != null && await processGrant(productId);
            if (!granted)
            {
                Debug.LogError($"[AppsInTossIapProvider] 미결 주문 지급 저장 실패: {orderId}");
                return;
            }

            bool completed = await AIT.IAPCompleteProductGrant(
                new IAPCompleteProductGrantArgs_0
                {
                    Params = new IAPCompleteProductGrantArgs_0Params { OrderId = orderId }
                },
                ApiTimeoutMs);

            if (completed)
                Debug.Log($"[AppsInTossIapProvider] 미결 주문 복구 완료: {orderId}");
            else
                Debug.LogError($"[AppsInTossIapProvider] 미결 주문 완료 통지 실패: {orderId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[AppsInTossIapProvider] 미결 주문 복구 오류({orderId}): {e.Message}");
        }
    }

    // SDK 브릿지가 버리는 결제 오류 코드를 메시지 앞 "[CODE] "로 보존하는 JS 훅(AppsInTossIapErrorCode.jslib)을 건다.
    // 주문 생성 전에 걸려 있어야 하고, 여러 번 불러도 한 번만 걸린다.
    private static void InstallErrorCodeHook()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (ButterPopInstallIapErrorCodeHook() == 0)
            Debug.LogWarning("[AppsInTossIapProvider] 결제 오류 코드 훅을 걸지 못했습니다. 취소 여부를 메시지로만 판정합니다.");
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int ButterPopInstallIapErrorCodeHook();
#endif
}
#endif
