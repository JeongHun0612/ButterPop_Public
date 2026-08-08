using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class UIInGameHUD : UIScene
{
    [Header("Buttons")]
    [SerializeField] private Button menuButton;
    [SerializeField] private Button skipButton;

    [Header("Buttons")]
    [SerializeField] private Button customButton;
    [SerializeField] private Button storeButton;
    [SerializeField] private Button stageSelectButton;

    public override void Initialize()
    {
        base.Initialize();

        if (menuButton != null) menuButton.OnClickWithSound(OnMenuButtonClicked);
        if (skipButton != null) skipButton.OnClickWithSound(OnSkipButtonClicked);

        if (customButton != null) customButton.OnClickWithSound(OnCustomButtonClicked);
        if (storeButton != null) storeButton.OnClickWithSound(OnStoreButtonClicked);
        if (stageSelectButton != null) stageSelectButton.OnClickWithSound(OnStageSelectButtonClicked);
    }

    private void OnMenuButtonClicked()
    {
        UIManager.Instance.ShowPopup<UIMenu>().Forget();
    }

    private void OnSkipButtonClicked()
    {
        // TODO 스테이지 광고보고 건너뛰기
    }

    private void OnCustomButtonClicked()
    {
        UIManager.Instance.ShowPopup<UICustom>().Forget();
    }

    private void OnStoreButtonClicked()
    {
        UIManager.Instance.ShowPopup<UIStore>().Forget();
    }

    private void OnStageSelectButtonClicked()
    {
        UIManager.Instance.ShowPopup<UIStageSelect>().Forget();
    }
}
