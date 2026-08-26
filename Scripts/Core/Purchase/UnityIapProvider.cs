#if UNITY_IAP
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;

// Unity IAP 5.x를 IIapProvider로 감싼다. 콜백 기반 스토어 API를 UniTask로 변환한다.
public class UnityIapProvider : IIapProvider
{
    private StoreController _store;
    private readonly List<string> _owned = new();
    private UniTaskCompletionSource<PurchaseState> _purchaseSource;

    public async UniTask InitializeAsync()
    {
        _store = UnityIAPServices.StoreController();

        // 보유 조회 결과가 OnPurchasePending으로 흘러들면 구매 흐름과 뒤섞이므로 끈다.
        _store.ProcessPendingOrdersOnPurchasesFetched(false);

        _store.OnPurchasePending += OnPurchasePending;
        _store.OnPurchaseFailed += OnPurchaseFailed;
        _store.OnPurchaseDeferred += OnPurchaseDeferred;

        await _store.Connect();
        await FetchProductsAsync();
    }

    public async UniTask<PurchaseState> PurchaseAsync(string productId)
    {
        if (_store == null)
        {
            Debug.LogError("[UnityIapProvider] 스토어 미연결 - 구매 불가");
            return PurchaseState.Failed;
        }

        Product product = FindProduct(productId);
        if (product == null)
        {
            Debug.LogError($"[UnityIapProvider] 스토어에 없는 상품: {productId}");
            return PurchaseState.Failed;
        }

        _purchaseSource = new UniTaskCompletionSource<PurchaseState>();
        _store.PurchaseProduct(product);
        return await _purchaseSource.Task;
    }

    public async UniTask RestoreAsync()
    {
        if (_store == null) return;

#if UNITY_IOS
        // 애플은 명시적 복원이 필수다. 구글은 FetchPurchases만으로 보유가 확인된다.
        var restoreSource = new UniTaskCompletionSource();
        _store.RestoreTransactions((success, error) =>
        {
            if (!success) Debug.LogWarning($"[UnityIapProvider] 복원 실패: {error}");
            restoreSource.TrySetResult();
        });
        await restoreSource.Task;
#endif

        await FetchPurchasesAsync();
    }

    public IReadOnlyList<string> GetOwnedProductIds() => _owned;

    public bool TryGetLocalizedPrice(string productId, out string price)
    {
        price = FindProduct(productId)?.metadata?.localizedPriceString;
        return !string.IsNullOrEmpty(price);
    }

    private UniTask FetchProductsAsync()
    {
        var source = new UniTaskCompletionSource();
        var catalog = new List<ProductDefinition>
        {
            new(ProductIds.RemoveBannerAds, ProductType.NonConsumable),
            new(ProductIds.RemoveAllAds, ProductType.NonConsumable)
        };

        void Unsubscribe()
        {
            _store.OnProductsFetched -= OnFetched;
            _store.OnProductsFetchFailed -= OnFailed;
        }

        void OnFetched(List<Product> products)
        {
            Unsubscribe();
            Debug.Log($"[UnityIapProvider] 상품 조회 완료: {products.Count}개");
            source.TrySetResult();
        }

        void OnFailed(ProductFetchFailed failure)
        {
            Unsubscribe();
            Debug.LogError($"[UnityIapProvider] 상품 조회 실패: {failure}");
            source.TrySetResult();
        }

        _store.OnProductsFetched += OnFetched;
        _store.OnProductsFetchFailed += OnFailed;
        _store.FetchProducts(catalog);
        return source.Task;
    }

    private UniTask FetchPurchasesAsync()
    {
        var source = new UniTaskCompletionSource();

        void Unsubscribe()
        {
            _store.OnPurchasesFetched -= OnFetched;
            _store.OnPurchasesFetchFailed -= OnFailed;
        }

        void OnFetched(Orders orders)
        {
            Unsubscribe();
            RecordOwned(orders);
            source.TrySetResult();
        }

        void OnFailed(PurchasesFetchFailureDescription failure)
        {
            Unsubscribe();
            Debug.LogWarning($"[UnityIapProvider] 보유 상품 조회 실패: {failure}");
            source.TrySetResult();
        }

        _store.OnPurchasesFetched += OnFetched;
        _store.OnPurchasesFetchFailed += OnFailed;
        _store.FetchPurchases();
        return source.Task;
    }

    private void RecordOwned(Orders orders)
    {
        _owned.Clear();
        if (orders == null) return;

        foreach (Order order in orders.ConfirmedOrders)
            AddOwned(GetProductId(order));

        // 미확정 주문은 지난 실행에서 확정 전에 앱이 종료된 것이다.
        // 구글은 3일 내 미확정 구매를 자동 환불하므로 여기서 확정해 둔다.
        foreach (PendingOrder order in orders.PendingOrders)
        {
            AddOwned(GetProductId(order));
            _store.ConfirmPurchase(order);
        }
    }

    private void AddOwned(string productId)
    {
        if (string.IsNullOrEmpty(productId) || _owned.Contains(productId)) return;
        _owned.Add(productId);
    }

    private Product FindProduct(string productId)
        => _store?.GetProducts()?.FirstOrDefault(p => p.definition.id == productId);

    private void ResolvePurchase(PurchaseState state)
    {
        UniTaskCompletionSource<PurchaseState> source = _purchaseSource;
        _purchaseSource = null;
        source?.TrySetResult(state);
    }

    private static string GetProductId(Order order)
        => order?.CartOrdered?.Items()?.FirstOrDefault()?.Product?.definition?.id;

    private void OnPurchasePending(PendingOrder order)
    {
        string productId = GetProductId(order);
        AddOwned(productId);
        _store.ConfirmPurchase(order);
        Debug.Log($"[UnityIapProvider] 구매 완료: {productId}");
        ResolvePurchase(PurchaseState.Success);
    }

    private void OnPurchaseFailed(FailedOrder order)
    {
        bool cancelled = order.FailureReason == PurchaseFailureReason.UserCancelled;
        Debug.Log($"[UnityIapProvider] 구매 실패({order.FailureReason}): {GetProductId(order)}");
        ResolvePurchase(cancelled ? PurchaseState.Cancelled : PurchaseState.Failed);
    }

    private void OnPurchaseDeferred(DeferredOrder order)
    {
        Debug.Log($"[UnityIapProvider] 지연 결제 대기: {GetProductId(order)}");
        ResolvePurchase(PurchaseState.Pending);
    }
}
#endif
