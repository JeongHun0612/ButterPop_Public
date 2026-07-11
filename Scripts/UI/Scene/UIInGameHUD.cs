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
        Debug.Log("[UIInGameHUD] Custom 버튼 클릭");
    }

    private void OnStoreButtonClicked()
    {
        Debug.Log("[UIInGameHUD] Store 버튼 클릭");
    }

    private void OnTempButtonClicked()
    {
        Debug.Log("[UIInGameHUD] Temp 버튼 클릭");
    }
}
