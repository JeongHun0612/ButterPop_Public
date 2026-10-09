using System;
using Cysharp.Threading.Tasks;

public interface IAdsService
{
    UniTask InitializeAsync();

    /// <summary>배너 표시. 광고 제거 구매 시 자동으로 무시(no-op)된다.</summary>
    void ShowBanner();
    void HideBanner();

    /// <summary>위치 변경을 지원하는 제공자에 배너 방향을 적용한다.</summary>
    void SetBannerSide(BannerSide side);

    /// <summary>
    /// 전면(인터스티셜) 광고를 재생한다. 광고 제거 구매 시 표시하지 않는다.
    /// 실제로 표시했으면 true, 게이팅/미로드로 건너뛰면 false를 반환한다.
    /// </summary>
    UniTask<bool> ShowInterstitialAsync();

    /// <summary>
    /// 리워드 광고를 재생한다(광고 제거와 무관하게 항상 허용).
    /// 시청을 완료하여 보상을 지급해야 하면 true를 반환한다.
    /// </summary>
    UniTask<bool> ShowRewardedAsync();

    /// <summary>리워드 광고가 재생 준비됐는지. (버튼 활성화 판단용)</summary>
    bool IsRewardedReady { get; }

    /// <summary>리워드 광고의 재생 준비 상태가 바뀔 때 통지한다.</summary>
    event Action<bool> OnRewardedAvailabilityChanged;

    /// <summary>배너가 차지하는 화면 높이(px). 광고 제거 구매·배너 미표시 시 0. (UI 여백 확보용)</summary>
    float BannerHeightPixels { get; }

    /// <summary>배너가 붙는 방향(상/하).</summary>
    BannerSide BannerSide { get; }
}
