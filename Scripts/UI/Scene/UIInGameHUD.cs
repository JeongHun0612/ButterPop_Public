using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class UIInGameHUD : UIScene
{
    [Header("Buttons")]
    [SerializeField] private Button customButton;
    [SerializeField] private Button storeButton;
    [SerializeField] private Button tempButton;

    public override void Initialize()
    {
        base.Initialize();

        customButton.onClick.AddListener(OnCustomButtonClicked);
        storeButton.onClick.AddListener(OnStoreButtonClicked);
        tempButton.onClick.AddListener(OnTempButtonClicked);
    }

    private void OnCustomButtonClicked()
    {
        UIManager.Instance.ShowPopup<UICustom>().Forget();
    }

    private void OnStoreButtonClicked()
    {
        UIManager.Instance.ShowPopup<UIStore>().Forget();
    }

    private void OnTempButtonClicked()
    {
        Debug.Log("[UIInGameHUD] Temp 버튼 클릭");
    }
}
