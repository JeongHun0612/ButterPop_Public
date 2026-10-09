#if APPS_IN_TOSS
using AppsInToss;
using UnityEngine;

// 기기 안전 영역의 상단 또는 하단에 배너를 고정하고 상단에서는 토스 내비게이션 캡슐을 피한다.
// 광고 전용 Canvas에는 Graphic/Raycaster를 두지 않아 게임 입력을 가로채지 않는다.
public sealed class AppsInTossBannerHost : MonoBehaviour
{
    private const float InitialHeightCss = 96f;
    private RectTransform _rect;
    private BannerSide _bannerSide = BannerSide.Bottom;

    public AITBannerAdView View { get; private set; }

    private void Awake()
    {
        _rect = (RectTransform)transform;
        View = gameObject.AddComponent<AITBannerAdView>();
        View.Placement = AITBannerAdPlacement.FollowRectTransform;
        View.AutoResizeHeight = true;
        RefreshPosition();
    }

    // SafeArea는 안전 영역을, SDK의 LateUpdate는 DOM 위치를 추적한다.
    private void Update() => RefreshPosition();

    public static AppsInTossBannerHost Create()
    {
        var root = new GameObject("AppsInTossBannerCanvas", typeof(RectTransform), typeof(Canvas));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // CanvasScaler 없이 화면 px와 RectTransform 단위를 일치시킨다.
        canvas.scaleFactor = 1f;
        DontDestroyOnLoad(root);

        var safeArea = new GameObject("SafeArea", typeof(RectTransform));
        var safeRect = (RectTransform)safeArea.transform;
        safeRect.SetParent(root.transform, false);
        safeRect.anchorMin = Vector2.zero;
        safeRect.anchorMax = Vector2.one;
        safeRect.offsetMin = Vector2.zero;
        safeRect.offsetMax = Vector2.zero;
        safeArea.AddComponent<SafeArea>();

        var banner = new GameObject("Banner", typeof(RectTransform));
        var bannerRect = (RectTransform)banner.transform;
        bannerRect.SetParent(safeRect, false);
        bannerRect.anchorMin = Vector2.zero;
        bannerRect.anchorMax = new Vector2(1f, 0f);
        bannerRect.pivot = new Vector2(0.5f, 0f);
        bannerRect.sizeDelta = new Vector2(0f, InitialHeightCss * AppsInTossSafeArea.DevicePixelRatio);
        return banner.AddComponent<AppsInTossBannerHost>();
    }

    public void Show(string adGroupId)
    {
        View.AdGroupId = adGroupId;
        // 높이가 0이면 SDK가 실제 광고 높이를 역산할 수 없어 표시할 때 초기화한다.
        _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            InitialHeightCss * AppsInTossSafeArea.DevicePixelRatio);
        RefreshPosition();
        // SDK가 초기 위치를 보내기 전에 Overlay Canvas의 화면 크기를 확정한다.
        Canvas.ForceUpdateCanvases();
        View.Show();
    }

    public void Hide() => View.Hide();

    public void SetBannerSide(BannerSide side)
    {
        _bannerSide = side;
        float y = side == BannerSide.Top ? 1f : 0f;
        _rect.anchorMin = new Vector2(0f, y);
        _rect.anchorMax = new Vector2(1f, y);
        _rect.pivot = new Vector2(0.5f, y);
        RefreshPosition();
    }

    private void RefreshPosition()
    {
        _rect.anchoredPosition = _bannerSide == BannerSide.Top
            ? new Vector2(0f, -BannerSafeArea.MinTopPixels)
            : Vector2.zero;
    }
}
#endif
