#if APPS_IN_TOSS
using System;
using System.Collections.Generic;
using AppsInToss;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 앱인토스의 SKU 기반 일회성 결제를 기존 IIapProvider 계약에 연결한다.
public sealed class AppsInTossIapProvider : IIapProvider
{
    private const float PurchaseTimeoutSeconds = 900f;
    private const int ApiTimeoutMs = 30000;

    private readonly List<string> _owned = new();
    private readonly List<string> _revoked = new();
    private readonly Dictionary<string, string> _prices = new();
    private bool _purchaseInProgress;

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
            if (!string.IsNullOrEmpty(product.Sku) && !string.IsNullOrEmpty(product.DisplayAmount))
                _prices[product.Sku] = product.DisplayAmount;
        }

        Debug.Log($"[AppsInTossIapProvider] 상품 조회 완료: {_prices.Count}개");
    }

    public async UniTask<PurchaseState> PurchaseAsync(string productId, Func<string, bool> processGrant)
    {
        if (_purchaseInProgress)
        {
            Debug.LogWarning($"[AppsInTossIapProvider] 이미 구매 진행 중: {productId}");
            return PurchaseState.Failed;
        }

        if (!IsKnownProduct(productId))
        {
            Debug.LogError($"[AppsInTossIapProvider] 알 수 없는 SKU: {productId}");
            return PurchaseState.Failed;
        }

        _purchaseInProgress = true;
        bool previousAudioPause = AudioListener.pause;
        AudioListener.pause = true;

        var source = new UniTaskCompletionSource<PurchaseState>();
        Action dispose = null;

        try
        {
            var options = new IapCreateOneTimePurchaseOrderOptionsOptions
            {
                Sku = productId,
                ProcessProductGrant = _ => processGrant?.Invoke(productId) == true
            };

            dispose = AIT.IAPCreateOneTimePurchaseOrder(
                onEvent: result =>
                {
                    if (result?.Type == "success")
                    {
                        if (!_owned.Contains(productId)) _owned.Add(productId);
                        _revoked.Remove(productId);
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
                    Debug.LogWarning($"[AppsInTossIapProvider] 구매 실패({productId}): {error.ErrorCode} {error.Message}");
                    source.TrySetResult(IsCancelled(error) ? PurchaseState.Cancelled : PurchaseState.Failed);
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
            AudioListener.pause = previousAudioPause;
            _purchaseInProgress = false;
        }
    }

    public async UniTask RestoreAsync(Func<string, UniTask<bool>> processGrant)
    {
        _owned.Clear();
        _revoked.Clear();

        await ReconcileCompletedAndRefundedAsync();

        var pending = await AIT.IAPGetPendingOrders(ApiTimeoutMs);
        if (pending?.Orders == null) return;

        foreach (var order in pending.Orders)
        {
            if (order == null || !IsKnownProduct(order.Sku)) continue;

            bool granted = processGrant != null && await processGrant(order.Sku);
            if (!granted)
            {
                Debug.LogError($"[AppsInTossIapProvider] 미결 주문 지급 저장 실패: {order.OrderId}");
                continue;
            }

            bool completed = await AIT.IAPCompleteProductGrant(
                new IAPCompleteProductGrantArgs_0
                {
                    Params = new IAPCompleteProductGrantArgs_0Params { OrderId = order.OrderId }
                },
                ApiTimeoutMs);

            if (completed)
            {
                if (!_owned.Contains(order.Sku)) _owned.Add(order.Sku);
                _revoked.Remove(order.Sku);
                Debug.Log($"[AppsInTossIapProvider] 미결 주문 복구 완료: {order.OrderId}");
            }
            else
            {
                Debug.LogError($"[AppsInTossIapProvider] 미결 주문 완료 통지 실패: {order.OrderId}");
            }
        }
    }

    public IReadOnlyList<string> GetOwnedProductIds() => _owned;
    public IReadOnlyList<string> GetRevokedProductIds() => _revoked;

    public bool TryGetLocalizedPrice(string productId, out string price)
        => _prices.TryGetValue(productId, out price);

    private async UniTask ReconcileCompletedAndRefundedAsync()
    {
        var history = await AIT.IAPGetCompletedOrRefundedOrders(ApiTimeoutMs);
        if (history?.Orders == null) return;

        // 같은 SKU에 여러 이력이 있으면 ISO 날짜가 가장 최신인 주문 상태를 사용한다.
        var latest = new Dictionary<string, CompletedOrRefundedOrdersResultOrder>();
        foreach (var order in history.Orders)
        {
            if (order == null || !IsKnownProduct(order.Sku)) continue;
            if (!latest.TryGetValue(order.Sku, out var previous)
                || string.CompareOrdinal(order.Date, previous.Date) > 0)
                latest[order.Sku] = order;
        }

        foreach (var pair in latest)
        {
            if (pair.Value.Status == CompletedOrRefundedOrdersResultOrderStatus.COMPLETED)
                _owned.Add(pair.Key);
            else
                _revoked.Add(pair.Key);
        }
    }

    private static bool IsKnownProduct(string productId)
        => productId == ProductIds.RemoveBannerAds || productId == ProductIds.RemoveAllAds;

    private static bool IsCancelled(AITException error)
    {
        string value = $"{error?.ErrorCode} {error?.Message}";
        return value.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
#endif
