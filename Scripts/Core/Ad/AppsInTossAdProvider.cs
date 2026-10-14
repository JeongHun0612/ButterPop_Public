#if APPS_IN_TOSS
using System;
using AppsInToss;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 앱인토스 통합 광고 SDK를 기존 광고 서비스 계약에 연결한다.
public sealed class AppsInTossAdProvider : IAdProvider, IBannerPositionProvider
{
    private const float ShowTimeoutSeconds = 120f;

    // 로드 응답이 끝내 오지 않으면 이후 전면·리워드 로드가 모두 막히므로 이 시간이 지나면 실패로 본다.
    private const float LoadTimeoutSeconds = 30f;

    // 실패가 이어져도 세션 안에서 복구되도록 간격을 늘려 가며 재시도한다(마지막 간격은 반복).
    private static readonly TimeSpan[] RetryDelays =
    {
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60),
    };

    private readonly AdRetryTimer _rewardedRetry = new AdRetryTimer(nameof(AppsInTossAdProvider), "리워드", RetryDelays);
    private readonly AdRetryTimer _bannerRetry = new AdRetryTimer(nameof(AppsInTossAdProvider), "배너", RetryDelays);

    private bool _interstitialReady;
    private bool _rewardedReady;
    private bool _fullScreenLoadInProgress;
    private bool _interstitialLoadQueued;
    private bool _rewardedLoadQueued;
    private int _fullScreenLoadId;
    private Action _loadDisposer;
    private bool _bannerRequested;   // 게임이 배너 노출을 원하는지. 실패해도 유지해 재시도 대상이 된다.
    private float _bannerHeightPixels;
    private AppsInTossBannerHost _bannerHost;
    private BannerSide _bannerSide = BannerSide.Bottom;

    public event Action<bool> OnRewardedAvailabilityChanged;

    // 앱인토스 광고 SDK는 노출 이벤트에 수익 정보를 주지 않아 발화하지 않는다.
    public event Action<AdImpression> OnAdImpression { add { } remove { } }

    public bool IsInterstitialReady => _interstitialReady;
    public bool IsRewardedReady => _rewardedReady;
    public float BannerHeightPixels => _bannerRequested ? _bannerHeightPixels : 0f;
    public BannerSide BannerSide => _bannerSide;

    public async UniTask InitializeAsync()
    {
        await UniTask.CompletedTask;
        Debug.Log($"[AppsInTossAdProvider] 초기화 완료 ({(AppsInTossServiceSettings.UseProductionAds ? "실광고" : "테스트 광고")} ID)");
    }

    public void ShowBanner()
    {
        string id = AppsInTossServiceSettings.BannerAdGroupId;
        if (!ValidateId(id, "배너")) return;

        _bannerRequested = true;
        _bannerRetry.Cancel();
        if (_bannerHost == null)
        {
            _bannerHost = AppsInTossBannerHost.Create();
            _bannerHost.View.OnAdEvent += OnBannerEvent;
        }
        _bannerHeightPixels = 0f;
        _bannerHost.SetBannerSide(_bannerSide);
        _bannerHost.Show(id);
    }

    public void HideBanner()
    {
        _bannerRequested = false;
        _bannerRetry.Cancel();
        if (_bannerHost != null) _bannerHost.Hide();
        _bannerHeightPixels = 0f;
    }

    public void SetBannerSide(BannerSide side)
    {
        _bannerSide = side;
        if (_bannerHost != null) _bannerHost.SetBannerSide(side);
    }

    public void LoadInterstitial()
    {
        if (_interstitialReady) return;

        // 구버전 토스 앱에서도 두 전면 광고 그룹을 동시에 로드하지 않도록 직렬화한다.
        if (_fullScreenLoadInProgress)
        {
            _interstitialLoadQueued = true;
            return;
        }

        StartFullScreenLoad(AppsInTossServiceSettings.InterstitialAdGroupId, rewarded: false);
    }

    public void LoadRewarded()
    {
        if (_rewardedReady) return;

        // 예약된 재시도를 기다리지 않고 지금 로드한다.
        _rewardedRetry.Cancel();

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
        OnRewardedAvailabilityChanged?.Invoke(false);
        return await ShowFullScreenAsync(AppsInTossServiceSettings.RewardedAdGroupId, rewarded: true);
    }

    private void StartFullScreenLoad(string adGroupId, bool rewarded)
    {
        if (_fullScreenLoadInProgress || !ValidateId(adGroupId, rewarded ? "리워드" : "전면")) return;

        _fullScreenLoadInProgress = true;
        int loadId = ++_fullScreenLoadId;
        _loadDisposer?.Invoke();
        _loadDisposer = AIT.LoadFullScreenAd(
            adGroupId,
            onEvent: evt =>
            {
                if (!IsCurrentLoad(loadId) || evt?.Type != "loaded") return;

                if (rewarded)
                {
                    _rewardedReady = true;
                    _rewardedRetry.Reset();
                    OnRewardedAvailabilityChanged?.Invoke(true);
                }
                else
                {
                    _interstitialReady = true;
                }

                FinishFullScreenLoad();
            },
            onError: error =>
            {
                if (!IsCurrentLoad(loadId)) return;
                FailFullScreenLoad(rewarded, error.Message);
            });

        WatchLoadTimeoutAsync(loadId, rewarded).Forget();
    }

    // 타임아웃 뒤 늦게 도착한 응답이 다음 로드 상태를 건드리지 않게 한다.
    private bool IsCurrentLoad(int loadId) => _fullScreenLoadInProgress && loadId == _fullScreenLoadId;

    private async UniTaskVoid WatchLoadTimeoutAsync(int loadId, bool rewarded)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(LoadTimeoutSeconds));
        if (!IsCurrentLoad(loadId)) return;

        FailFullScreenLoad(rewarded, $"{LoadTimeoutSeconds:0}초 동안 응답 없음");
    }

    // 전면은 다음 표시 기회에 AdsService가 다시 로드하므로 리워드만 재시도한다.
    private void FailFullScreenLoad(bool rewarded, string reason)
    {
        Debug.LogWarning($"[AppsInTossAdProvider] {(rewarded ? "리워드" : "전면")} 로드 실패: {reason}");

        if (rewarded)
        {
            // 로드 중에 쌓인 리워드 요청은 재시도가 대신하므로 즉시 재로드하지 않는다.
            _rewardedLoadQueued = false;
            OnRewardedAvailabilityChanged?.Invoke(false);
            _rewardedRetry.Schedule(LoadRewarded);
        }

        FinishFullScreenLoad();
    }

    private void FinishFullScreenLoad()
    {
        _loadDisposer?.Invoke();
        _loadDisposer = null;
        _fullScreenLoadInProgress = false;

        // 대기 중인 요청은 버튼 노출에 직결되는 리워드부터 처리한다.
        if (_rewardedLoadQueued)
        {
            _rewardedLoadQueued = false;
            LoadRewarded();
        }

        if (_interstitialLoadQueued && !_fullScreenLoadInProgress)
        {
            _interstitialLoadQueued = false;
            LoadInterstitial();
        }
    }

    private async UniTask<bool> ShowFullScreenAsync(string adGroupId, bool rewarded)
    {
        bool rewardEarned = false;
        var audioPause = AudioPauseGate.Acquire();

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
            audioPause.Dispose();
        }
    }

    private static bool ValidateId(string id, string type)
    {
        if (!string.IsNullOrWhiteSpace(id)) return true;
        Debug.LogWarning($"[AppsInTossAdProvider] {type} 광고 그룹 ID가 비어 있어 광고를 사용하지 않습니다.");
        return false;
    }

    private void OnBannerEvent(AITBannerAdEvent evt)
    {
        if (evt == null || !_bannerRequested) return;

        switch (evt.Kind)
        {
            case AITBannerAdEventKind.Rendered:
                _bannerRetry.Reset();
                break;
            // Height는 CSS px라 비율이 없을 때는 캔버스 배율을 곱해 스크린 px로 바꾼다.
            case AITBannerAdEventKind.Resized:
                _bannerHeightPixels = evt.HeightFraction > 0f
                    ? evt.HeightFraction * Screen.height
                    : evt.Height * AppsInTossSafeArea.DevicePixelRatio;
                break;
            // 재시도의 Show가 슬롯을 새로 만들어 교체한다. 초기화 실패도 다음 Show에서 다시 초기화된다.
            case AITBannerAdEventKind.FailedToRender:
            case AITBannerAdEventKind.InitializationFailed:
            case AITBannerAdEventKind.NoFill:
                _bannerHeightPixels = 0f;
                Debug.LogWarning($"[AppsInTossAdProvider] 배너 표시 실패: {evt}");
                if (_bannerRequested) _bannerRetry.Schedule(ShowBanner);
                break;
        }
    }
}
#endif
