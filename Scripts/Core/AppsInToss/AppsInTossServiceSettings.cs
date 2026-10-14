using System.Collections.Generic;

// 앱인토스 콘솔에서 발급받는 광고 그룹 ID와 인앱결제 SKU. 콘솔 등록 후 빈 값을 채운다.
// 실광고 ID는 AIT_PRODUCTION_ADS 디파인이 있는 출시 후보 빌드에서만 쓴다.
// SDK의 Deploy for Online Test도 개발 빌드가 아니라서 Debug.isDebugBuild로 가르면 실기기 테스트에 실광고가 나간다(정책 위반).
public static class AppsInTossServiceSettings
{
    private const string ProductionBannerAdGroupId = "PUBLIC_SAMPLE_APPS_IN_TOSS_ID";
    private const string ProductionInterstitialAdGroupId = "PUBLIC_SAMPLE_APPS_IN_TOSS_ID";
    private const string ProductionRewardedAdGroupId = "PUBLIC_SAMPLE_APPS_IN_TOSS_ID";

    private const string TestBannerAdGroupId = "ait-ad-test-banner-id";
    private const string TestInterstitialAdGroupId = "ait-ad-test-interstitial-id";
    private const string TestRewardedAdGroupId = "ait-ad-test-rewarded-id";

    // 게임 상품 ID → 콘솔이 발급한 SKU(콘솔에서 발급받은 SKU).
    private static readonly Dictionary<string, string> Skus = new()
    {
        { ProductIds.RemoveBannerAds, "PUBLIC_SAMPLE_APPS_IN_TOSS_ID" },
        { ProductIds.RemoveAllAds, "PUBLIC_SAMPLE_APPS_IN_TOSS_ID" },
    };

#if AIT_PRODUCTION_ADS
    public static readonly bool UseProductionAds = true;
#else
    public static readonly bool UseProductionAds = false;
#endif

    public static string BannerAdGroupId => UseProductionAds ? ProductionBannerAdGroupId : TestBannerAdGroupId;
    public static string InterstitialAdGroupId => UseProductionAds ? ProductionInterstitialAdGroupId : TestInterstitialAdGroupId;
    public static string RewardedAdGroupId => UseProductionAds ? ProductionRewardedAdGroupId : TestRewardedAdGroupId;

    // SKU가 빠진 상품은 결제 이력에서 찾을 수 없어 보유 없음으로 보인다. 모두 채워졌을 때만 보유 동기화를 해야 한다.
    public static bool IsIapConfigured
    {
        get
        {
            foreach (var sku in Skus.Values)
            {
                if (string.IsNullOrWhiteSpace(sku)) return false;
            }
            return true;
        }
    }

    public static bool TryGetSku(string productId, out string sku)
    {
        sku = null;
        return productId != null && Skus.TryGetValue(productId, out sku) && !string.IsNullOrWhiteSpace(sku);
    }

    public static bool TryGetProductId(string sku, out string productId)
    {
        productId = null;
        if (string.IsNullOrWhiteSpace(sku)) return false;

        foreach (var pair in Skus)
        {
            if (pair.Value != sku) continue;
            productId = pair.Key;
            return true;
        }
        return false;
    }

    // 출시 후보 빌드에서 비어 있으면 광고·결제가 동작하지 않는 설정 이름들.
    public static List<string> GetMissingProductionSettings()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(ProductionBannerAdGroupId)) missing.Add(nameof(ProductionBannerAdGroupId));
        if (string.IsNullOrWhiteSpace(ProductionInterstitialAdGroupId)) missing.Add(nameof(ProductionInterstitialAdGroupId));
        if (string.IsNullOrWhiteSpace(ProductionRewardedAdGroupId)) missing.Add(nameof(ProductionRewardedAdGroupId));

        foreach (var pair in Skus)
        {
            if (string.IsNullOrWhiteSpace(pair.Value)) missing.Add($"SKU({pair.Key})");
        }
        return missing;
    }
}
