#if UNITY_IAP
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;

// Unity IAP 5.x를 IIapProvider로 감싼다. 콜백 기반 스토어 API를 UniTask로 변환한다.
public class UnityIapProvider : IIapProvider
{
    private const float ConnectTimeoutSeconds = 30f;
    private const float FetchTimeoutSeconds = 30f;
    // 결제창은 카드 등록·인증으로 길어질 수 있어 넉넉히 잡는다. 무한 대기만 막는 용도다.
    private const float PurchaseTimeoutSeconds = 900f;

    private StoreController _store;
    private readonly List<string> _owned = new();
    private UniTaskCompletionSource<PurchaseState> _purchaseSource;
    private bool _initialized;
    private bool _ownedFetched;   // 보유 조회에 한 번이라도 성공했는지

    public async UniTask InitializeAsync()
    {
        // 두 번 초기화하면 이벤트가 중복 구독되어 ConfirmPurchase가 두 번 호출된다.
        if (_initialized)
        {
            Debug.LogWarning("[UnityIapProvider] 이미 초기화됨 - 중복 호출 무시");
            return;
        }
        _initialized = true;

        _store = UnityIAPServices.StoreController();

        // 보유 조회 결과가 OnPurchasePending으로 흘러들면 구매 흐름과 뒤섞이므로 끈다.
        _store.ProcessPendingOrdersOnPurchasesFetched(false);

        _store.OnPurchasePending += OnPurchasePending;
        _store.OnPurchaseFailed += OnPurchaseFailed;
        _store.OnPurchaseDeferred += OnPurchaseDeferred;

        bool timeout = await _store.Connect().AsUniTask()
            .TimeoutWithoutException(TimeSpan.FromSeconds(ConnectTimeoutSeconds));
        if (timeout)
        {
            Debug.LogError("[UnityIapProvider] 스토어 연결 시간 초과");
            return;
        }

        await FetchProductsAsync();
    }

    public async UniTask<PurchaseState> PurchaseAsync(string productId, Func<string, bool> processGrant)
    {
        if (_store == null)
        {
            Debug.LogError("[UnityIapProvider] 스토어 미연결 - 구매 불가");
            return PurchaseState.Failed;
        }

        if (_purchaseSource != null)
        {
            Debug.LogWarning($"[UnityIapProvider] 이미 구매 진행 중 - 요청 무시: {productId}");
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

        (bool timeout, PurchaseState state) = await _purchaseSource.Task
            .TimeoutWithoutException(TimeSpan.FromSeconds(PurchaseTimeoutSeconds));

        if (timeout)
        {
            // 비우지 않으면 진행 중으로 남아 이후 구매가 전부 막힌다.
            _purchaseSource = null;
            Debug.LogError($"[UnityIapProvider] 구매 응답 시간 초과: {productId}");
            return PurchaseState.Failed;
        }

        return state;
    }

    public async UniTask RestoreAsync(Func<string, UniTask<bool>> processGrant)
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

        bool restoreTimeout = await restoreSource.Task
            .TimeoutWithoutException(TimeSpan.FromSeconds(FetchTimeoutSeconds));
        if (restoreTimeout) Debug.LogError("[UnityIapProvider] 복원 응답 시간 초과");
#endif

        await FetchPurchasesAsync();
    }

    public bool TryGetOwnedProductIds(out IReadOnlyList<string> productIds)
    {
        productIds = _owned;
        return _ownedFetched;
    }

    public IReadOnlyList<string> GetRevokedProductIds() => Array.Empty<string>();

    public bool TryGetLocalizedPrice(string productId, out string price)
    {
        price = FindProduct(productId)?.metadata?.localizedPriceString;
        return !string.IsNullOrEmpty(price);
    }

    private async UniTask FetchProductsAsync()
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

        bool timeout = await source.Task.TimeoutWithoutException(TimeSpan.FromSeconds(FetchTimeoutSeconds));
        if (!timeout) return;

        Unsubscribe();
        Debug.LogError("[UnityIapProvider] 상품 조회 시간 초과");
    }

    private async UniTask FetchPurchasesAsync()
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

        bool timeout = await source.Task.TimeoutWithoutException(TimeSpan.FromSeconds(FetchTimeoutSeconds));
        if (!timeout) return;

        Unsubscribe();
        Debug.LogError("[UnityIapProvider] 보유 상품 조회 시간 초과");
    }

    private void RecordOwned(Orders orders)
    {
        _owned.Clear();
        if (orders == null) return;

        _ownedFetched = true;

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

    // 처리 도중 예외가 나도 ResolvePurchase는 반드시 실행한다. 빠뜨리면 대기가 풀리지 않아 상점이 잠긴다.
    private void OnPurchasePending(PendingOrder order)
    {
        try
        {
            string productId = GetProductId(order);
            AddOwned(productId);
            _store.ConfirmPurchase(order);
            Debug.Log($"[UnityIapProvider] 구매 완료: {productId}");
        }
        catch (Exception e)
        {
            // 확정에 실패해도 결제는 이미 성사됐다. 확정은 다음 실행의 보유 조회가 다시 시도한다.
            Debug.LogError($"[UnityIapProvider] 구매 확정 실패: {e.Message}");
        }
        finally
        {
            ResolvePurchase(PurchaseState.Success);
        }
    }

    private void OnPurchaseFailed(FailedOrder order)
    {
        PurchaseState state = PurchaseState.Failed;
        try
        {
            if (order.FailureReason == PurchaseFailureReason.UserCancelled) state = PurchaseState.Cancelled;
            Debug.Log($"[UnityIapProvider] 구매 실패({order.FailureReason}): {GetProductId(order)}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[UnityIapProvider] 실패 처리 중 예외: {e.Message}");
        }
        finally
        {
            ResolvePurchase(state);
        }
    }

    private void OnPurchaseDeferred(DeferredOrder order)
    {
        Debug.Log($"[UnityIapProvider] 지연 결제 대기: {GetProductId(order)}");
        ResolvePurchase(PurchaseState.Pending);
    }
}
#endif
