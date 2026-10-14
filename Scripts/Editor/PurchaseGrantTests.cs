#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 결제 지급 응답이 구독자·저장 예외로 뒤집히지 않고, 복원이 경합·초기화 실패로 권한을 잃지 않는지 검증한다.
// 가짜 스토어는 AITCore.OnNestedCallback처럼 지급 콜백의 예외를 지급 실패(false)로 바꿔 응답한다.
public class PurchaseGrantTests
{
    [UnityTest]
    public IEnumerator GrantReportsSuccessWhenSubscriberThrows() => UniTask.ToCoroutine(async () =>
    {
        var save = new MemorySaveService();
        var store = new GrantingIapProvider();
        var purchase = new PurchaseService(save, store);
        purchase.OnPurchasesUpdated += ThrowFromSubscriber;
        ExpectSubscriberError();

        var state = await purchase.PurchaseAsync(ProductIds.RemoveBannerAds);

        Assert.AreEqual(true, store.GrantResponse);
        Assert.AreEqual(PurchaseState.Success, state);
        Assert.IsTrue(save.Data.purchase.bannerRemoveAds);
        Assert.AreEqual(1, save.SaveCalls);
    });

    [UnityTest]
    public IEnumerator OtherSubscribersAreNotifiedWhenOneThrows() => UniTask.ToCoroutine(async () =>
    {
        var store = new GrantingIapProvider();
        var purchase = new PurchaseService(new MemorySaveService(), store);
        int notified = 0;
        purchase.OnPurchasesUpdated += ThrowFromSubscriber;
        purchase.OnPurchasesUpdated += () => notified++;
        ExpectSubscriberError();

        await purchase.PurchaseAsync(ProductIds.RemoveAllAds);

        Assert.AreEqual(true, store.GrantResponse);
        Assert.AreEqual(1, notified);
    });

    [UnityTest]
    public IEnumerator GrantReportsSuccessWhenSaveThrows() => UniTask.ToCoroutine(async () =>
    {
        var save = new MemorySaveService { SaveError = new IOException("저장 실패") };
        var store = new GrantingIapProvider();
        var purchase = new PurchaseService(save, store);
        int notified = 0;
        purchase.OnPurchasesUpdated += () => notified++;
        LogAssert.Expect(LogType.Error, new Regex(@"\[PurchaseService\] 권한 저장 예약 실패"));

        var state = await purchase.PurchaseAsync(ProductIds.RemoveBannerAds);

        Assert.AreEqual(true, store.GrantResponse);
        Assert.AreEqual(PurchaseState.Success, state);
        Assert.IsTrue(save.Data.purchase.bannerRemoveAds);
        Assert.AreEqual(1, notified);
    });

    [UnityTest]
    public IEnumerator RecoveredGrantCompletesWhenSubscriberThrows() => UniTask.ToCoroutine(async () =>
    {
        var save = new MemorySaveService();
        var store = new GrantingIapProvider { PendingProductId = ProductIds.RemoveAllAds };
        var purchase = new PurchaseService(save, store);
        purchase.OnPurchasesUpdated += ThrowFromSubscriber;
        ExpectSubscriberError();

        await purchase.RestoreAsync();

        Assert.AreEqual(true, store.RecoveredGrantResponse);
        Assert.IsTrue(save.Data.purchase.allRemoveAds);
    });

    [UnityTest]
    public IEnumerator OverlappingRestoresShareOneStoreQuery() => UniTask.ToCoroutine(async () =>
    {
        var store = new GatedIapProvider { RestoreGate = new UniTaskCompletionSource() };
        var purchase = new PurchaseService(new MemorySaveService(), store);

        var first = purchase.RestoreAsync();
        var second = purchase.RestoreAsync();
        store.RestoreGate.TrySetResult();
        await UniTask.WhenAll(first, second);

        Assert.AreEqual(1, store.RestoreCalls);

        // 끝난 복원에 묶이지 않고 새로 조회한다(동기 완료 경로 포함).
        await purchase.RestoreAsync();
        await purchase.RestoreAsync();
        Assert.AreEqual(3, store.RestoreCalls);
    });

    [UnityTest]
    public IEnumerator PurchaseDuringRestoreIsNotRevoked() => UniTask.ToCoroutine(async () =>
    {
        var save = new MemorySaveService();
        var store = new GatedIapProvider { RestoreGate = new UniTaskCompletionSource() };
        var purchase = new PurchaseService(save, store);

        // 구매 전 보유 목록(빈 목록)을 조회해 둔 채로 복원이 대기한다.
        var restore = purchase.RestoreAsync();
        await purchase.PurchaseAsync(ProductIds.RemoveBannerAds);
        store.RestoreGate.TrySetResult();
        await restore;

        Assert.IsTrue(save.Data.purchase.bannerRemoveAds);
    });

    [UnityTest]
    public IEnumerator RestoreRunsWhenStoreInitializationFails() => UniTask.ToCoroutine(async () =>
    {
        var save = new MemorySaveService();
        var store = new GatedIapProvider
        {
            InitializeError = new InvalidOperationException("상품 조회 실패"),
            PendingProductId = ProductIds.RemoveAllAds
        };
        var purchase = new PurchaseService(save, store);
        LogAssert.Expect(LogType.Error, new Regex(@"\[PurchaseService\] 스토어 초기화 실패"));

        await purchase.InitializeAsync();

        Assert.AreEqual(1, store.RestoreCalls);
        Assert.IsTrue(save.Data.purchase.allRemoveAds);
    });

    private static void ThrowFromSubscriber() => throw new InvalidOperationException("구독자 오류");

    private static void ExpectSubscriberError() =>
        LogAssert.Expect(LogType.Error, new Regex(@"\[PurchaseService\] 구매 상태 구독자 예외"));

    private sealed class MemorySaveService : ISaveService
    {
        public Exception SaveError;
        public int SaveCalls;

        public SaveData Data { get; } = new SaveData();
        public bool IsReadOnly => false;

        public void Save()
        {
            SaveCalls++;
            if (SaveError != null) throw SaveError;
        }

        public UniTask<bool> SaveAsync()
        {
            SaveCalls++;
            return UniTask.FromResult(true);
        }

        public void DeleteAll() { }
    }

    // 앱인토스처럼 결제 도중 지급 콜백을 동기로 부르고, 미결 주문 복구에서는 비동기 콜백을 부른다.
    private sealed class GrantingIapProvider : IIapProvider
    {
        public string PendingProductId;
        public bool? GrantResponse;
        public bool? RecoveredGrantResponse;

        public UniTask InitializeAsync() => UniTask.CompletedTask;

        public UniTask<PurchaseState> PurchaseAsync(string productId, Func<string, bool> processGrant)
        {
            try
            {
                GrantResponse = processGrant(productId);
            }
            catch (Exception)
            {
                GrantResponse = false;
            }

            return UniTask.FromResult(GrantResponse == true ? PurchaseState.Success : PurchaseState.Failed);
        }

        public async UniTask RestoreAsync(Func<string, UniTask<bool>> processGrant)
        {
            if (PendingProductId == null) return;
            RecoveredGrantResponse = await processGrant(PendingProductId);
        }

        public bool TryGetOwnedProductIds(out IReadOnlyList<string> productIds)
        {
            productIds = null;
            return false;
        }

        public IReadOnlyList<string> GetRevokedProductIds() => Array.Empty<string>();

        public bool TryGetLocalizedPrice(string productId, out string price)
        {
            price = null;
            return false;
        }
    }

    // 복원 응답 시점을 테스트가 정하는 가짜 스토어.
    // 복원을 시작할 때 조회한 보유 목록(미결 주문만)을 끝날 때 확정해, 그 사이의 구매는 목록에 없다.
    private sealed class GatedIapProvider : IIapProvider
    {
        public Exception InitializeError;
        public string PendingProductId;
        public UniTaskCompletionSource RestoreGate;
        public int RestoreCalls;

        private readonly List<string> _owned = new();
        private bool _ownedFetched;

        public UniTask InitializeAsync() =>
            InitializeError != null ? UniTask.FromException(InitializeError) : UniTask.CompletedTask;

        public UniTask<PurchaseState> PurchaseAsync(string productId, Func<string, bool> processGrant) =>
            UniTask.FromResult(processGrant(productId) ? PurchaseState.Success : PurchaseState.Failed);

        public async UniTask RestoreAsync(Func<string, UniTask<bool>> processGrant)
        {
            RestoreCalls++;
            _ownedFetched = false;
            var fetched = new List<string>();

            if (RestoreGate != null) await RestoreGate.Task;

            if (PendingProductId != null)
            {
                fetched.Add(PendingProductId);
                await processGrant(PendingProductId);
            }

            _owned.Clear();
            _owned.AddRange(fetched);
            _ownedFetched = true;
        }

        public bool TryGetOwnedProductIds(out IReadOnlyList<string> productIds)
        {
            productIds = _owned;
            return _ownedFetched;
        }

        public IReadOnlyList<string> GetRevokedProductIds() => Array.Empty<string>();

        public bool TryGetLocalizedPrice(string productId, out string price)
        {
            price = null;
            return false;
        }
    }
}
#endif
