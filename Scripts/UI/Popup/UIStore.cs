using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class UIStore : UIPopup
{
    [Header("Store")]
    [SerializeField] private Button advButton;
    [SerializeField] private Button goldBananaButton;
    [SerializeField] private Button dailyRewardButton;

    [Header("하단")]
    [SerializeField] private Button customButton;
    [SerializeField] private Button closeButton;

    public override void Initialize()
    {
        base.Initialize();

        if (advButton != null) advButton.OnClickWithSound(OnAdvButtonClicked);
        if (goldBananaButton != null) goldBananaButton.OnClickWithSound(OnGoldBananaButtonClicked);
        if (dailyRewardButton != null) dailyRewardButton.OnClickWithSound(OnDailyRewardButtonClicked);

        if (customButton != null) customButton.OnClickWithSound(OnCustomButtonClicked);
        if (closeButton != null) closeButton.OnClickWithSound(OnClickBack);
    }

    // TODO: 광고 제거 인앱결제 연동
    private void OnAdvButtonClicked()
    {
        Debug.Log("[UIStore] 광고 제거 버튼 클릭");
    }

    // TODO: 골드 바나나 인앱결제 연동
    private void OnGoldBananaButtonClicked()
    {
        Debug.Log("[UIStore] 골드 바나나 구매 버튼 클릭");
    }

    // TODO: 리워드 광고 시청 후 1GB 지급
    private void OnDailyRewardButtonClicked()
    {
        Debug.Log("[UIStore] 데일리 리워드 버튼 클릭");
    }

    private async void OnCustomButtonClicked()
    {
        var uiManager = UIManager.Instance;
        Debug.Assert(uiManager != null, "[UIStore] UIManager가 없습니다");

        await uiManager.HidePopup();
        uiManager.ShowPopup<UICustom>().Forget();
    }
}
