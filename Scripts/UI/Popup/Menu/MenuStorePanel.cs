using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

/// <summary>
/// 통합 메뉴의 상점 패널. 광고 제거 IAP 버튼을 담당한다.
/// </summary>
public class MenuStorePanel : MonoBehaviour
{
    [Header("상점")]
    [SerializeField] private Button bannerAdRemoveButton;   // 배너 광고 제거
    [SerializeField] private TMP_Text bannerAdRemovePriceText;

    [SerializeField] private Button allAdRemoveButton;       // 전체 광고 제거
    [SerializeField] private TMP_Text allAdRemovePriceText;

    [SerializeField] private Button restoreButton;           // 구매 복원

    [Header("안내 문구")]
    [SerializeField] private StatusMessage statusMessage;
    [SerializeField] private LocalizedString purchaseFailedMessage;
    [SerializeField] private LocalizedString purchasePendingMessage;
    [SerializeField] private LocalizedString purchaseRestoredMessage;
    [SerializeField] private LocalizedString nothingToRestoreMessage;

    private IPurchaseService _purchase;
    private bool _purchasing;   // 결제·복원 진행 중 중복 요청(연타) 차단

    private void OnDestroy()
    {
        if (_purchase != null) _purchase.OnPurchasesUpdated -= RefreshButtons;
    }

    public void Initialize()
    {
        if (bannerAdRemoveButton != null) bannerAdRemoveButton.OnClickWithSound(OnBannerAdRemoveClicked);
        if (allAdRemoveButton != null) allAdRemoveButton.OnClickWithSound(OnAllAdRemoveClicked);
        if (restoreButton != null) restoreButton.OnClickWithSound(OnRestoreClicked);

        if (Services.TryGet<IPurchaseService>(out _purchase))
            _purchase.OnPurchasesUpdated += RefreshButtons;

        Refresh();
    }

    // 스토어 상품 조회는 비동기라 패널을 열 때마다 가격을 다시 채운다.
    public void Refresh()
    {
        // 지난번 안내가 남아 지금 발생한 일로 오해되지 않게 한다.
        if (statusMessage != null) statusMessage.Clear();

        RefreshPrices();
        RefreshButtons();
    }

    private void RefreshPrices()
    {
        RefreshPrice(bannerAdRemovePriceText, ProductIds.RemoveBannerAds);
        RefreshPrice(allAdRemovePriceText, ProductIds.RemoveAllAds);
    }

    // 조회 실패 시 인스펙터에 넣어둔 기본 문구를 그대로 둔다.
    private void RefreshPrice(TMP_Text text, string productId)
    {
        if (text == null || _purchase == null) return;
        if (_purchase.TryGetPrice(productId, out string price)) text.text = price;
    }

    // 이미 보유한 상품, 스토어에서 조회되지 않은 상품, 결제 진행 중인 버튼은 비활성화한다.
    private void RefreshButtons()
    {
        if (_purchase == null) return;

        if (bannerAdRemoveButton != null)
            bannerAdRemoveButton.interactable = CanPurchase(ProductIds.RemoveBannerAds, _purchase.RemoveBannerAds);
        if (allAdRemoveButton != null)
            allAdRemoveButton.interactable = CanPurchase(ProductIds.RemoveAllAds, _purchase.RemoveAllAds);
        if (restoreButton != null)
            restoreButton.interactable = !_purchasing;
    }

    // 가격이 조회되지 않으면 스토어에 없는 상품으로 본다.
    private bool CanPurchase(string productId, bool owned)
        => !_purchasing && !owned && _purchase.TryGetPrice(productId, out _);

    private void ShowStatus(string message, bool isError = false)
    {
        if (statusMessage != null) statusMessage.Show(message, isError);
    }

    private async UniTask ShowLocalizedStatusAsync(LocalizedString localizedMessage, bool isError = false)
    {
        if (localizedMessage == null) return;

        string message = await localizedMessage.GetLocalizedStringAsync();
        ShowStatus(message, isError);
    }

    private async UniTaskVoid PurchaseAsync(string productId)
    {
        if (_purchase == null || _purchasing) return;

        _purchasing = true;
        RefreshButtons();

        PurchaseState state = await _purchase.PurchaseAsync(productId);

        _purchasing = false;
        RefreshButtons();

        // 취소는 유저가 스스로 닫은 것이라 안내하지 않는다.
        switch (state)
        {
            case PurchaseState.Failed:
                await ShowLocalizedStatusAsync(purchaseFailedMessage, true);
                Debug.LogWarning($"[MenuStorePanel] 구매 실패: {productId}");
                break;
            case PurchaseState.Pending:
                await ShowLocalizedStatusAsync(purchasePendingMessage);
                Debug.Log($"[MenuStorePanel] 결제 대기: {productId}");
                break;
        }
        // 성공 시 OnPurchasesUpdated → RefreshButtons가 자동 호출된다.
    }

    private async UniTaskVoid RestoreAsync()
    {
        if (_purchase == null || _purchasing) return;

        _purchasing = true;
        RefreshButtons();

        bool hadBanner = _purchase.RemoveBannerAds;
        bool hadAll = _purchase.RemoveAllAds;

        await _purchase.RestoreAsync();

        _purchasing = false;
        RefreshButtons();

        bool restored = (!hadBanner && _purchase.RemoveBannerAds) || (!hadAll && _purchase.RemoveAllAds);

        LocalizedString selectedMessage = restored
            ? purchaseRestoredMessage
            : nothingToRestoreMessage;
        await ShowLocalizedStatusAsync(selectedMessage);
        Debug.Log($"[MenuStorePanel] 구매 복원 완료 (복원된 권한 있음: {restored})");
    }

    private void OnBannerAdRemoveClicked() => PurchaseAsync(ProductIds.RemoveBannerAds).Forget();

    private void OnAllAdRemoveClicked() => PurchaseAsync(ProductIds.RemoveAllAds).Forget();

    private void OnRestoreClicked() => RestoreAsync().Forget();
}
