#if APPS_IN_TOSS
using System;
using AppsInToss;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 앱인토스 SDK의 safe area inset을 UI 안전 영역(SafeArea·BannerSafeArea)에 연결한다.
// Unity WebGL의 Screen.safeArea는 항상 전체 화면이라 그대로 두면 노치·Dynamic Island를 피하지 못한다.
// SDK 3.2.0의 SafeAreaInsetsSubscribe는 C# 콜백이 JS로 전달되지 않아(OnEvent가 JsonIgnore) 쓰지 않고,
// 화면 크기가 바뀔 때 SafeAreaInsetsGet으로 다시 조회한다.
public static class AppsInTossSafeArea
{
    private const int QueryTimeoutMs = 2000;
    private const int Attempts = 3;
    private const int RetryDelayMs = 250;

    // 게임 X 버튼 캡슐은 inset 아래(iOS top+5, Android top+10)에 놓인다.
    // 캡슐 높이는 문서에 없어 추정값(36)에 여유(6)를 더했다. 실기기에서 조정한다.
    private const float NavigationReserveCss = 52f;

    private static SafeAreaInsets _insets;
    private static float _dpr = 1f;
    private static Vector2Int _queriedScreen;
    private static bool _querying;

    // CSS px → 스크린 px 배율. Initialize 전에는 1.
    public static float DevicePixelRatio => _dpr;

    public static void Initialize()
    {
        UpdateDevicePixelRatio();
        SafeArea.SafeAreaProvider = GetSafeArea;
        QueryAsync().Forget();
    }

    // SafeArea가 매 프레임 호출한다. 조회 전이거나 끝내 실패하면 Screen.safeArea(전체 화면)를 쓴다.
    private static Rect GetSafeArea()
    {
        int width = Screen.width;
        int height = Screen.height;
        if (!_querying && (width != _queriedScreen.x || height != _queriedScreen.y))
            QueryAsync().Forget();

        return _insets != null ? ToScreenRect(_insets, _dpr, width, height) : Screen.safeArea;
    }

    private static async UniTaskVoid QueryAsync()
    {
        _querying = true;
        _queriedScreen = new Vector2Int(Screen.width, Screen.height);
        try
        {
            UpdateDevicePixelRatio();
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                var insets = await TryGetInsetsAsync();
                if (insets != null)
                {
                    _insets = insets;
                    Debug.Log($"[AppsInTossSafeArea] inset 적용(CSS px) top={insets.Top} bottom={insets.Bottom} left={insets.Left} right={insets.Right}, dpr={_dpr}, screen={Screen.width}x{Screen.height}");
                    return;
                }

                if (attempt < Attempts - 1)
                    await UniTask.Delay(RetryDelayMs * (attempt + 1), DelayType.Realtime);
            }

            Debug.LogWarning("[AppsInTossSafeArea] inset 조회 실패 — 직전 값(없으면 전체 화면) 기준으로 UI를 배치합니다");
        }
        finally
        {
            _querying = false;
        }
    }

    // 조회 실패·무효 응답이면 null.
    private static async UniTask<SafeAreaInsets> TryGetInsetsAsync()
    {
        try
        {
            // JS가 null·undefined를 주면 역직렬화 결과가 C# null이 된다. 플랫폼 미지원은 error 필드로 온다.
            var insets = await AIT.SafeAreaInsetsGet(QueryTimeoutMs);
            if (insets == null || !string.IsNullOrEmpty(insets.error) || !IsValid(insets))
            {
                Debug.LogWarning($"[AppsInTossSafeArea] 무효한 inset 응답: {(insets == null ? "null" : insets.error)}");
                return null;
            }
            return insets;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AppsInTossSafeArea] inset 조회 오류: {e.Message}");
            return null;
        }
    }

    // GetDevicePixelRatio는 캔버스 해상도(Screen.width/height)에 쓰인 배율을 준다.
    // 템플릿이 저사양 기기에서 window.devicePixelRatio보다 낮춰 두므로 이 값으로 변환해야 맞는다.
    private static void UpdateDevicePixelRatio()
    {
        double dpr = AIT.GetDevicePixelRatio();
        _dpr = !double.IsNaN(dpr) && !double.IsInfinity(dpr) && dpr > 0 ? (float)dpr : 1f;
        BannerSafeArea.MinTopPixels = NavigationReserveCss * _dpr;
    }

    // CSS px inset → 스크린 px 안전 영역. Unity Rect는 좌하단이 원점이다.
    private static Rect ToScreenRect(SafeAreaInsets insets, float dpr, int width, int height)
    {
        float left = ToScreenPixels(insets.Left, dpr, width);
        float right = ToScreenPixels(insets.Right, dpr, width);
        float top = ToScreenPixels(insets.Top, dpr, height);
        float bottom = ToScreenPixels(insets.Bottom, dpr, height);
        return new Rect(left, bottom, width - left - right, height - top - bottom);
    }

    // 한 변이 화면 절반을 넘지 않게 막아 영역이 뒤집히지 않도록 한다.
    private static float ToScreenPixels(double css, float dpr, int extent) =>
        Mathf.Clamp(Mathf.Round((float)css * dpr), 0f, extent * 0.5f);

    private static bool IsValid(SafeAreaInsets insets) =>
        IsValidInset(insets.Top) && IsValidInset(insets.Bottom) && IsValidInset(insets.Left) && IsValidInset(insets.Right);

    private static bool IsValidInset(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
}
#endif
