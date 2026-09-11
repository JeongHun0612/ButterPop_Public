#if APPS_IN_TOSS
using System;
using AppsInToss;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 앱인토스 통합 광고 SDK를 기존 광고 서비스 계약에 연결한다.
public sealed class AppsInTossAdProvider : IAdProvider
{
    private const float ShowTimeoutSeconds = 120f;

    private bool _interstitialReady;
    private bool _rewardedReady;
    private bool _fullScreenLoadInProgress;
    private bool _rewardedLoadQueued;
    private bool _bannerVisible;
    private float _bannerHeightPixels;
    private Action _loadDisposer;

    public bool IsInterstitialReady => _interstitialReady;
    public bool IsRewardedReady => _rewardedReady;
    public float BannerHeightPixels => _bannerVisible ? _bannerHeightPixels : 0f;
    public BannerSide BannerSide => BannerSide.Top;

    public async UniTask InitializeAsync()
    {
        AITBannerAd.OnAdEvent -= OnBannerEvent;
        AITBannerAd.OnAdEvent += OnBannerEvent;
        await UniTask.CompletedTask;
        Debug.Log("[AppsInTossAdProvider] 초기화 완료");
    }

    public void ShowBanner()
    {
        string id = AppsInTossServiceSettings.BannerAdGroupId;
        if (!ValidateId(id, "배너")) return;

        _bannerVisible = true;
        AITBannerAd.Show(id, AITBannerPosition.Top);
    }

    public void HideBanner()
    {
        AITBannerAd.Hide();
        _bannerVisible = false;
        _bannerHeightPixels = 0f;
    }

    public void LoadInterstitial()
    {
        if (_interstitialReady) return;
        StartFullScreenLoad(AppsInTossServiceSettings.InterstitialAdGroupId, rewarded: false);
    }

    public void LoadRewarded()
    {
        if (_rewardedReady) return;

        // 구버전 토스 앱에서도 두 전면 광고 그룹을 동시에 로드하지 않도록 직렬화한다.
        if (_fullScreenLoadInProgress)
        {
            _rewardedLoadQueued = true;
            return;
        }

        StartFullScreenLoad(AppsInTossServiceSettings.RewardedAdGroupId, rewarded: true);
    }

    public async UniTask<bool> ShowInterstitialAsync()
    {
        if (!_interstitialReady) return false;
        _interstitialReady = false;
        return await ShowFullScreenAsync(AppsInTossServiceSettings.InterstitialAdGroupId, rewarded: false);
    }

    public async UniTask<bool> ShowRewardedAsync()
    {
        if (!_rewardedReady) return false;
        _rewardedReady = false;
        return await ShowFullScreenAsync(AppsInTossServiceSettings.RewardedAdGroupId, rewarded: true);
    }

    private void StartFullScreenLoad(string adGroupId, bool rewarded)
    {
        if (_fullScreenLoadInProgress || !ValidateId(adGroupId, rewarded ? "리워드" : "전면")) return;

        _fullScreenLoadInProgress = true;
        _loadDisposer?.Invoke();
        _loadDisposer = AIT.LoadFullScreenAd(
            adGroupId,
            onEvent: evt =>
            {
                if (evt?.Type != "loaded") return;

                if (rewarded) _rewardedReady = true;
                else _interstitialReady = true;
                FinishFullScreenLoad();
            },
            onError: error =>
            {
                Debug.LogWarning($"[AppsInTossAdProvider] {(rewarded ? "리워드" : "전면")} 로드 실패: {error.Message}");
                FinishFullScreenLoad();
            });
    }

    private void FinishFullScreenLoad()
    {
        _loadDisposer?.Invoke();
        _loadDisposer = null;
        _fullScreenLoadInProgress = false;

        if (_rewardedLoadQueued)
        {
            _rewardedLoadQueued = false;
            LoadRewarded();
        }
    }

    private async UniTask<bool> ShowFullScreenAsync(string adGroupId, bool rewarded)
    {
        bool previousAudioPause = AudioListener.pause;
        bool rewardEarned = false;
        AudioListener.pause = true;

        var source = new UniTaskCompletionSource<bool>();
        Action dispose = null;

        try
        {
            dispose = AIT.ShowFullScreenAd(
                adGroupId,
                onEvent: evt =>
                {
                    switch (evt?.Type)
                    {
                        case "userEarnedReward":
                            rewardEarned = true;
                            break;
                        case "dismissed":
                            source.TrySetResult(rewarded ? rewardEarned : true);
                            break;
                        case "failedToShow":
                            source.TrySetResult(false);
                            break;
                    }
                },
                onError: error =>
                {
                    Debug.LogWarning($"[AppsInTossAdProvider] 광고 표시 실패: {error.Message}");
                    source.TrySetResult(false);
                });

            var (timedOut, result) = await source.Task
                .TimeoutWithoutException(TimeSpan.FromSeconds(ShowTimeoutSeconds));

            if (timedOut)
            {
                Debug.LogError("[AppsInTossAdProvider] 광고 표시 응답 시간 초과");
                return false;
            }

            return result;
        }
        finally
        {
            dispose?.Invoke();
            AudioListener.pause = previousAudioPause;
        }
    }

    private void OnBannerEvent(AITBannerAdEvent evt)
    {
        if (evt == null) return;

        switch (evt.Kind)
        {
            case AITBannerAdEventKind.Resized:
                _bannerHeightPixels = evt.HeightFraction > 0f
                    ? evt.HeightFraction * Screen.height
                    : evt.Height;
                break;
            case AITBannerAdEventKind.FailedToRender:
            case AITBannerAdEventKind.InitializationFailed:
            case AITBannerAdEventKind.NoFill:
                _bannerVisible = false;
                _bannerHeightPixels = 0f;
                Debug.LogWarning($"[AppsInTossAdProvider] 배너 표시 실패: {evt}");
                break;
        }
    }

    private static bool ValidateId(string id, string type)
    {
        if (!string.IsNullOrWhiteSpace(id)) return true;
        Debug.LogWarning($"[AppsInTossAdProvider] {type} 광고 그룹 ID가 비어 있어 광고를 사용하지 않습니다.");
        return false;
    }
}
#endif
