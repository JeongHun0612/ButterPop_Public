using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class UIInGameHUD : UIScene
{
    [Header("Buttons")]
    [SerializeField] private Button customButton;
    [SerializeField] private Button storeButton;
    [SerializeField] private Button stageSelectButton;

    public override void Initialize()
    {
        base.Initialize();

        customButton.OnClickWithSound(OnCustomButtonClicked);
        storeButton.OnClickWithSound(OnStoreButtonClicked);
        stageSelectButton.OnClickWithSound(OnStageSelectButtonClicked);
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
