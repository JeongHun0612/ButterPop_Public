using System;
using Cysharp.Threading.Tasks;

// 광고 SDK를 얇게 래핑하는 어댑터 계약. AdMob·LevelPlay 등 SDK 교체는 이 구현만 바꾼다.
// 정책(광고 제거 게이팅·빈도 제한·preload)은 상위 AdsService가 담당하고,
// 여기서는 순수 로드/표시만 다룬다(SDK는 광고 제거 여부·과금 상태를 모른다).
public interface IAdProvider
{
    UniTask InitializeAsync();

    // 배너
    void ShowBanner();
    void HideBanner();
    float BannerHeightPixels { get; }   // 배너가 차지하는 화면 높이(px). 실제 노출 중이 아니면 0.
    BannerSide BannerSide { get; }       // 배너가 붙는 방향(상/하)

    // 전면(인터스티셜)
    bool IsInterstitialReady { get; }
    void LoadInterstitial();
    UniTask<bool> ShowInterstitialAsync();   // 표시 완료 시 true

    // 리워드
    bool IsRewardedReady { get; }
    event Action<bool> OnRewardedAvailabilityChanged;
    void LoadRewarded();
    UniTask<bool> ShowRewardedAsync();        // 시청 완료(보상 지급) 시 true

    // 광고가 노출될 때마다 수익 정보와 함께 메인 스레드에서 발화한다.
    event Action<AdImpression> OnAdImpression;
}
