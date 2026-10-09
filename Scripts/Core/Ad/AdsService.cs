using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 게임이 사용하는 광고 계약 구현. SDK는 IAdProvider가 감추고,
// 여기서는 광고 제거 게이팅 · preload · (추후)빈도 제한 같은 정책을 담당한다.
public class AdsService : IAdsService
{
    private readonly IAdProvider _provider;
    private readonly IPurchaseService _purchase;
    private readonly IAnalyticsService _analytics;

    private bool _initialized;
    private bool _bannerRequested;   // 게임이 배너 노출을 요청했는지.

    public event Action<bool> OnRewardedAvailabilityChanged;

    public AdsService(IAdProvider provider, IPurchaseService purchase, IAnalyticsService analytics)
    {
        _provider = provider;
        _purchase = purchase;
        _analytics = analytics;
        _purchase.OnPurchasesUpdated += OnPurchasesUpdated;
        _provider.OnRewardedAvailabilityChanged += OnProviderRewardedAvailabilityChanged;
        _provider.OnAdImpression += _analytics.LogAdImpression;
    }

    public bool IsRewardedReady => _provider.IsRewardedReady;

    public float BannerHeightPixels => _purchase.RemoveBannerAds ? 0f : _provider.BannerHeightPixels;
    public BannerSide BannerSide => _provider.BannerSide;

    public async UniTask InitializeAsync()
    {
        await _provider.InitializeAsync();
        _initialized = true;

        // 초기화 완료 전에 배너 노출이 요청됐다면 이제 실제로 띄운다(요청 유실 방지).
        if (_bannerRequested && !_purchase.RemoveBannerAds)
            _provider.ShowBanner();

        // 전면·리워드는 표시 전에 미리 로드해 둔다.
        _provider.LoadInterstitial();
        _provider.LoadRewarded();
    }

    public void ShowBanner()
    {
        // 권한이 회수되면 띄울 수 있도록 요청은 항상 기억한다.
        _bannerRequested = true;

        if (_purchase.RemoveBannerAds) return;

        // 초기화 전이면 요청만 기억하고, 완료 시 InitializeAsync가 자동으로 노출한다.
        if (!_initialized)
            return;

        _provider.ShowBanner();
    }

    public void HideBanner()
    {
        _bannerRequested = false;
        _provider.HideBanner();
    }

    public void SetBannerSide(BannerSide side)
    {
        if (_provider is IBannerPositionProvider positionProvider)
            positionProvider.SetBannerSide(side);
    }

    public async UniTask<bool> ShowInterstitialAsync()
    {
        if (_purchase.RemoveAllAds) return false;

        // 로드가 안 됐으면 다음을 위해 로드만 걸고 흐름은 막지 않는다(게임 진행 우선).
        if (!_provider.IsInterstitialReady)
        {
            _provider.LoadInterstitial();
            return false;
        }

        bool shown = await _provider.ShowInterstitialAsync();
        _provider.LoadInterstitial(); // 다음 표시를 위해 재로드
        return shown;
    }

    public async UniTask<bool> ShowRewardedAsync()
    {
        // 리워드는 광고 제거와 무관하게 항상 허용한다(유저가 보상을 위해 자발적으로 시청).
        if (!_provider.IsRewardedReady)
        {
            _provider.LoadRewarded();
            return false;
        }

        bool rewarded = await _provider.ShowRewardedAsync();
        _provider.LoadRewarded(); // 다음 시청을 위해 재로드
        return rewarded;
    }

    // 배너 제거 권한 변경에 맞춰 배너를 내리거나 다시 띄운다.
    private void OnPurchasesUpdated()
    {
        if (_purchase.RemoveBannerAds) _provider.HideBanner();
        else if (_initialized && _bannerRequested) _provider.ShowBanner();
    }

    private void OnProviderRewardedAvailabilityChanged(bool available) =>
        OnRewardedAvailabilityChanged?.Invoke(available);
}
