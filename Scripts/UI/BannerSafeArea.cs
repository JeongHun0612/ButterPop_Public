using UnityEngine;

// 배너 광고 높이만큼 이 RectTransform의 세로 offset을 자동으로 파낸다(높이·방향은 IAdsService에서 취득).
// 상단은 배너가 없어도 MinTopPixels만큼은 비운다.
// anchor는 건드리지 않으므로 SafeArea와 같은 오브젝트에 붙여도 된다.
[RequireComponent(typeof(RectTransform))]
public class BannerSafeArea : MonoBehaviour
{
    private RectTransform _rect;
    private Canvas _canvas;
    private float _appliedTop = float.NaN;
    private float _appliedBottom = float.NaN;

    // 안전 영역 안쪽 상단에 겹쳐 그려지는 플랫폼 버튼(앱인토스 X 캡슐)을 피할 최소 높이(px).
    // 상단 배너는 버튼 영역 아래에 배치하므로 두 높이를 합쳐 비운다.
    public static float MinTopPixels { get; set; }

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
    }

    private void OnEnable() => Apply();

    private void Update() => Apply();

    private void Apply()
    {
        if (_rect == null) return;

        // 서비스가 없거나 제거 구매 시 배너 높이는 0.
        bool hasAds = Services.TryGet<IAdsService>(out var ads);
        float banner = hasAds ? ToCanvasUnits(ads.BannerHeightPixels) : 0f;
        float minTop = ToCanvasUnits(MinTopPixels);
        bool top = !hasAds || ads.BannerSide == BannerSide.Top;
        SetPadding(top ? minTop + banner : minTop, top ? 0f : banner);
    }

    // 스크린 px → 캔버스 단위.
    private float ToCanvasUnits(float px)
    {
        if (px <= 0f) return 0f;

        float sf = _canvas != null ? _canvas.scaleFactor : 1f;
        return sf > 0f ? px / sf : 0f;
    }

    // anchor는 두고 세로 offset만 조정.
    private void SetPadding(float top, float bottom)
    {
        if (Mathf.Approximately(top, _appliedTop) && Mathf.Approximately(bottom, _appliedBottom)) return;
        _appliedTop = top;
        _appliedBottom = bottom;

        var max = _rect.offsetMax; max.y = -top; _rect.offsetMax = max;
        var min = _rect.offsetMin; min.y = bottom; _rect.offsetMin = min;
    }
}
