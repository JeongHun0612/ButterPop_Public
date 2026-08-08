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

    public void Initialize()
    {
        if (bannerAdRemoveButton != null) bannerAdRemoveButton.OnClickWithSound(OnBannerAdRemoveClicked);
        if (allAdRemoveButton != null) allAdRemoveButton.OnClickWithSound(OnAllAdRemoveClicked);
    }

    // TODO: 배너 광고 제거 인앱결제 연동
    private void OnBannerAdRemoveClicked()
    {
        Debug.Log("[MenuStorePanel] 배너 광고 제거 버튼 클릭");
    }

    // TODO: 전체 광고 제거 인앱결제 연동
    private void OnAllAdRemoveClicked()
    {
        Debug.Log("[MenuStorePanel] 전체 광고 제거 버튼 클릭");
    }
}
