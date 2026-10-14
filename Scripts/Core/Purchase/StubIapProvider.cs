using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

// SDK 미연동 개발용 스텁 프로바이더. 실제 결제 없이 권한 지급 흐름을 테스트한다.
public class StubIapProvider : IIapProvider
{
    // 개발용 스위치. true로 바꾸면 구매가 항상 실패해 실패 안내 UI를 확인할 수 있다.
    private static readonly bool ForceFailure = false;

    public async UniTask InitializeAsync()
    {
        Debug.Log("[StubIapProvider] 초기화(스텁)");
        await UniTask.CompletedTask;
    }

    public async UniTask<PurchaseState> PurchaseAsync(string productId, Func<string, bool> processGrant)
    {
        await UniTask.CompletedTask;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (ForceFailure)
        {
            Debug.Log($"[StubIapProvider] 강제 실패(개발용): {productId}");
            return PurchaseState.Failed;
        }

        // 즉시승인: 스토어 연동 전 개발빌드 한정으로 결제 성공을 흉내낸다.
        // 릴리즈 빌드에는 이 분기가 컴파일되지 않아 공짜 지급이 새지 않는다.
        Debug.Log($"[StubIapProvider] 개발빌드 즉시승인: {productId}");
        return PurchaseState.Success;
#else
        Debug.LogError($"[StubIapProvider] IAP 미연동 릴리즈 빌드 - 구매 거부: {productId}");
        return PurchaseState.Failed;
#endif
    }

    public async UniTask RestoreAsync(Func<string, UniTask<bool>> processGrant)
    {
        Debug.Log("[StubIapProvider] 복원(스텁)");
        await UniTask.CompletedTask;
    }

    // 스토어가 없어 항상 미확인으로 답한다(세이브 권한 유지).
    public bool TryGetOwnedProductIds(out IReadOnlyList<string> productIds)
    {
        productIds = null;
        return false;
    }

    public IReadOnlyList<string> GetRevokedProductIds() => System.Array.Empty<string>();

    // 스토어가 없어 더미 가격을 돌려준다.
    public bool TryGetLocalizedPrice(string productId, out string price)
    {
        price = "₩0";
        return true;
    }
}
