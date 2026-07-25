using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class UIInGameHUD : UIScene
{
    [Header("Buttons")]
    [SerializeField] private Button customButton;
    [SerializeField] private Button storeButton;

    public override void Initialize()
    {
        base.Initialize();

        customButton.onClick.AddListener(OnCustomButtonClicked);
        storeButton.onClick.AddListener(OnStoreButtonClicked);
    }

    private void OnCustomButtonClicked()
    {
        UIManager.Instance.ShowPopup<UICustom>().Forget();
    }

    private void OnStoreButtonClicked()
    {
        UIManager.Instance.ShowPopup<UIStore>().Forget();
    }
}
