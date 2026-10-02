using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

// 인앱 결제 SDK를 얇게 래핑하는 어댑터 계약. 권한 지급 정책은 PurchaseService가 담당한다.
public interface IIapProvider
{
    UniTask InitializeAsync();

    // Apps in Toss는 결제 오버레이가 열린 동안 상품 지급 성공 여부를 동기로 요구한다.
    // 다른 스토어 구현은 processGrant를 무시하고 기존 구매 완료 콜백을 사용한다.
    UniTask<PurchaseState> PurchaseAsync(string productId, Func<string, bool> processGrant);

    // 보유 상품 재조회. 애플은 명시적 복원이 필수다.
    // Apps in Toss의 미결 주문은 지급 저장이 끝난 뒤 완료 처리해야 하므로 비동기 콜백을 받는다.
    UniTask RestoreAsync(Func<string, UniTask<bool>> processGrant);

    // 스토어가 보유로 확인한 상품 ID들. 보유 조회에 성공한 적이 없으면 false(보유 없음과 구분).
    bool TryGetOwnedProductIds(out IReadOnlyList<string> productIds);

    // 환불 등으로 회수해야 할 상품 ID들. 회수 정보를 제공하지 않는 스토어는 빈 목록을 반환한다.
    IReadOnlyList<string> GetRevokedProductIds();

    // 스토어에 등록된 현지화 가격("₩3,300"). 조회 실패 시 false.
    bool TryGetLocalizedPrice(string productId, out string price);
}
