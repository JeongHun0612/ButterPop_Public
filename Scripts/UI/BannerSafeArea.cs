using UnityEngine;

// 배너 광고 높이만큼 이 RectTransform의 세로 offset을 자동으로 파낸다(높이·방향은 IAdsService에서 취득).
// anchor는 건드리지 않으므로 SafeArea와 같은 오브젝트에 붙여도 된다.
[RequireComponent(typeof(RectTransform))]
public class BannerSafeArea : MonoBehaviour
{
    private RectTransform _rect;
    private Canvas _canvas;
    private float _appliedTop = float.NaN;
    private float _appliedBottom = float.NaN;

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

        float pad = ResolvePadding();
        bool top = !Services.TryGet<IAdsService>(out var ads) || ads.BannerSide == BannerSide.Top;
        SetPadding(top ? pad : 0f, top ? 0f : pad);
    }

    // 배너 실제 높이(px) → 캔버스 단위. 서비스/배너 없거나 제거 구매 시 0.
    private float ResolvePadding()
    {
        if (!Services.TryGet<IAdsService>(out var ads)) return 0f;

        float px = ads.BannerHeightPixels;
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
