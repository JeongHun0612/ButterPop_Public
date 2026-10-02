#if APPS_IN_TOSS
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// 앱인토스 WebGL 빌드 직전에 광고·결제 설정을 점검한다.
// 출시 후보(AIT_PRODUCTION_ADS)에 콘솔 값이 비어 있으면 광고·결제가 동작하지 않는 빌드가 올라가므로 빌드를 중단한다.
public class AppsInTossBuildValidator : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL) return;

        if (!AppsInTossServiceSettings.UseProductionAds)
        {
            Debug.LogWarning("[AppsInTossBuildValidator] 테스트 광고 ID로 빌드합니다. 출시 후보는 AIT_PRODUCTION_ADS 디파인이 있는 빌드 프로필로 빌드하세요.");
            if (!AppsInTossServiceSettings.IsIapConfigured)
                Debug.LogWarning("[AppsInTossBuildValidator] 인앱결제 SKU가 비어 있어 이 빌드에서는 결제·복원이 동작하지 않습니다.");
            return;
        }

        var missing = AppsInTossServiceSettings.GetMissingProductionSettings();
        if (missing.Count > 0)
            throw new BuildFailedException(
                $"[AppsInTossBuildValidator] 실광고 빌드(AIT_PRODUCTION_ADS)에 필요한 콘솔 값이 비어 있습니다(AppsInTossServiceSettings): {string.Join(", ", missing)}. " +
                "실기기 테스트(Deploy for Online Test)는 'AppsInToss - Mobile - Development' 프로필로 빌드하세요.");

        Debug.Log("[AppsInTossBuildValidator] 실광고 ID로 빌드합니다.");
    }
}
#endif
