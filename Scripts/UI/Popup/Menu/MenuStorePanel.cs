using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 통합 메뉴의 상점 패널. 광고 제거 IAP 버튼을 담당한다.
/// </summary>
public class MenuStorePanel : MonoBehaviour
{
    [Header("상점")]
    [SerializeField] private Button bannerAdRemoveButton;   // 배너 광고 제거
    [SerializeField] private Button allAdRemoveButton;       // 전체 광고 제거

    private IPurchaseService _purchase;

    private void OnDestroy()
    {
        if (_purchase != null) _purchase.OnPurchasesUpdated -= RefreshButtons;
    }

    public void Initialize()
    {
        if (bannerAdRemoveButton != null) bannerAdRemoveButton.OnClickWithSound(OnBannerAdRemoveClicked);
        if (allAdRemoveButton != null) allAdRemoveButton.OnClickWithSound(OnAllAdRemoveClicked);

        if (Services.TryGet<IPurchaseService>(out _purchase))
            _purchase.OnPurchasesUpdated += RefreshButtons;

        RefreshButtons();
    }

    // 이미 보유한 상품의 버튼은 비활성화한다.
    private void RefreshButtons()
    {
        if (_purchase == null) return;
        if (bannerAdRemoveButton != null) bannerAdRemoveButton.interactable = !_purchase.RemoveBannerAds;
        if (allAdRemoveButton != null) allAdRemoveButton.interactable = !_purchase.RemoveAllAds;
    }

    private async UniTaskVoid PurchaseAsync(string productId)
    {
        if (_purchase == null) return;
        bool ok = await _purchase.PurchaseAsync(productId);
        if (!ok) Debug.LogWarning($"[MenuStorePanel] 구매 실패 또는 미연동: {productId}");
        // 성공 시 OnPurchasesUpdated → RefreshButtons가 자동 호출된다.
    }

    private void OnBannerAdRemoveClicked() => PurchaseAsync(ProductIds.RemoveBannerAds).Forget();

    private void OnAllAdRemoveClicked() => PurchaseAsync(ProductIds.RemoveAllAds).Forget();
}
