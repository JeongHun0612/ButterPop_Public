using UnityEngine;

// 앱인토스 콘솔 등록 후 Production 값만 교체한다.
// 개발 빌드는 공식 테스트 광고 ID를 강제 사용해 실광고 테스트 정책 위반을 막는다.
public static class AppsInTossServiceSettings
{
    private const string ProductionBannerAdGroupId = "";
    private const string ProductionInterstitialAdGroupId = "";
    private const string ProductionRewardedAdGroupId = "";

    public static string BannerAdGroupId => Debug.isDebugBuild
        ? "ait-ad-test-banner-id"
        : ProductionBannerAdGroupId;

    public static string InterstitialAdGroupId => Debug.isDebugBuild
        ? "ait-ad-test-interstitial-id"
        : ProductionInterstitialAdGroupId;

    public static string RewardedAdGroupId => Debug.isDebugBuild
        ? "ait-ad-test-rewarded-id"
        : ProductionRewardedAdGroupId;
}
