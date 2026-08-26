using System.Collections.Generic;
using Cysharp.Threading.Tasks;

// 인앱 결제 SDK를 얇게 래핑하는 어댑터 계약. 권한 지급 정책은 PurchaseService가 담당한다.
public interface IIapProvider
{
    UniTask InitializeAsync();

    UniTask<PurchaseState> PurchaseAsync(string productId);

    // 보유 상품 재조회. 애플은 명시적 복원이 필수다.
    UniTask RestoreAsync();

    // 스토어가 보유로 확인한 상품 ID들.
    IReadOnlyList<string> GetOwnedProductIds();

    // 스토어에 등록된 현지화 가격("₩3,300"). 조회 실패 시 false.
    bool TryGetLocalizedPrice(string productId, out string price);
}
