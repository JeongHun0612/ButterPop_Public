// Unity LevelPlay(구 ironSource) 어댑터. com.unity.services.levelplay 9.x 기준.
// LEVELPLAY_SDK 디파인 + 모바일 빌드 타겟일 때만 컴파일된다.
// (그 외 플랫폼에선 ServiceBootstrap이 같은 조건으로 StubAdProvider를 골라 광고 없이 진행)
#if LEVELPLAY_SDK && (UNITY_ANDROID || UNITY_IOS)
using System;
using Cysharp.Threading.Tasks;
using Unity.Services.LevelPlay;
using UnityEngine;

public class LevelPlayProvider : IAdProvider
{
#if UNITY_ANDROID
    public const string AppKey = "PUBLIC_SAMPLE_CONFIG";
    private const string BannerAdUnitId = "PUBLIC_SAMPLE_CONFIG";
    private const string InterstitialAdUnitId = "PUBLIC_SAMPLE_CONFIG";
    private const string RewardedAdUnitId = "PUBLIC_SAMPLE_CONFIG";
#else // UNITY_IOS
    public const string AppKey = "PUBLIC_SAMPLE_CONFIG";
    private const string BannerAdUnitId = "PUBLIC_SAMPLE_CONFIG";
    private const string InterstitialAdUnitId = "PUBLIC_SAMPLE_CONFIG";
    private const string RewardedAdUnitId = "PUBLIC_SAMPLE_CONFIG";
#endif

    // ad_impression의 ad_platform 값. 리포트에서 미디에이션을 구분하는 라벨이라 바꾸지 않는다.
    private const string AdPlatformName = "ironSource";

    // 성공/실패 콜백이 둘 다 오지 않는 엣지 케이스를 대비한 초기화 대기 상한(초).
    private const float InitTimeoutSeconds = 5f;

    // 최초 배너 로드가 일시적으로 실패해도 세션 내에서 다시 복구할 수 있도록 점진적으로 재시도한다.
    private static readonly TimeSpan[] BannerRetryDelays =
    {
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60),
    };

    private static readonly TimeSpan[] RewardedRetryDelays =
    {
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60),
    };

    // 배너 위치 단일 소스.
    private static readonly LevelPlayBannerPosition BannerPos = LevelPlayBannerPosition.TopCenter;

    private LevelPlayBannerAd _banner;
    private LevelPlayAdSize _bannerSize;
    private int _bannerHeightDp;   // 배너 dp 높이 캐시(Android에선 Height getter가 JNI 호출).
    private LevelPlayInterstitialAd _interstitial;
    private LevelPlayRewardedAd _rewarded;

    private readonly AdRetryTimer _rewardedRetry = new AdRetryTimer(nameof(LevelPlayProvider), "리워드", RewardedRetryDelays);
    private readonly AdRetryTimer _bannerRetry = new AdRetryTimer(nameof(LevelPlayProvider), "배너", BannerRetryDelays);

    private bool _rewardedLoadInProgress;

    private bool _bannerRequested;       // 게임이 현재 배너 노출을 원하는지.
    private bool _bannerLoadInProgress;  // LoadAd 중복 호출 방지.
    private bool _bannerHasLoaded;       // 최초 로드 성공 여부(자동 갱신 실패와 구분).
    private bool _bannerVisible;         // 배너가 실제로 화면에 노출 중인지.

    // 표시 중인 전면/리워드의 완료를 await로 받기 위한 완료 소스.
    private UniTaskCompletionSource<bool> _interstitialShowTcs;
    private UniTaskCompletionSource<bool> _rewardedShowTcs;
    private bool _rewardGranted;

    public event Action<bool> OnRewardedAvailabilityChanged;
    public event Action<AdImpression> OnAdImpression;

    public bool IsInterstitialReady => _interstitial != null && _interstitial.IsAdReady();
    public bool IsRewardedReady => _rewarded != null && _rewarded.IsAdReady();

    public BannerSide BannerSide =>
        BannerPos == LevelPlayBannerPosition.BottomLeft
        || BannerPos == LevelPlayBannerPosition.BottomCenter
        || BannerPos == LevelPlayBannerPosition.BottomRight
            ? BannerSide.Bottom
            : BannerSide.Top;

    // dp 높이 → 화면 px. dpi 불명 시 폴백 밀도(2.75).
    public float BannerHeightPixels
    {
        get
        {
            if (!_bannerVisible) return 0f;
            float density = Screen.dpi > 0f ? Screen.dpi / 160f : 2.75f;
            return _bannerHeightDp * density;
        }
    }

    public async UniTask InitializeAsync()
    {
        var initTcs = new UniTaskCompletionSource<bool>();

        LevelPlay.OnInitSuccess += OnInitSuccess;
        LevelPlay.OnInitFailed += OnInitFailed;

        void OnInitSuccess(LevelPlayConfiguration _)
        {
            LevelPlay.OnInitSuccess -= OnInitSuccess;
            LevelPlay.OnInitFailed -= OnInitFailed;
            Debug.Log("[LevelPlayProvider] 초기화 성공");
            initTcs.TrySetResult(true);
        }

        void OnInitFailed(LevelPlayInitError error)
        {
            LevelPlay.OnInitSuccess -= OnInitSuccess;
            LevelPlay.OnInitFailed -= OnInitFailed;
            Debug.LogError($"[LevelPlayProvider] 초기화 실패: {error}");
            initTcs.TrySetResult(false);
        }

        LevelPlay.Init(AppKey);

        // 콜백이 어느 쪽도 오지 않는 경우 무한 대기하지 않도록 상한을 둔다. 초과 시 실패로 간주하고 정리한다.
        var (isTimeout, ok) = await initTcs.Task.TimeoutWithoutException(TimeSpan.FromSeconds(InitTimeoutSeconds));
        if (isTimeout)
        {
            LevelPlay.OnInitSuccess -= OnInitSuccess;
            LevelPlay.OnInitFailed -= OnInitFailed;
            Debug.LogError($"[LevelPlayProvider] 초기화 타임아웃({InitTimeoutSeconds}s) — 광고 없이 진행");
            return;
        }
        if (!ok) return;

        // 광고 표시 동안 게임 활동을 자동 일시정지(광고 콜백은 계속 동작, 닫히면 자동 재개).
        LevelPlay.SetPauseGame(true);

        CreateBanner();
        CreateInterstitial();
        CreateRewarded();
    }

    public void ShowBanner()
    {
        _bannerRequested = true;
        if (_banner == null) return;

        if (_bannerHasLoaded)
        {
            _banner.ShowAd();
            _bannerVisible = true;
            return;
        }

        RequestBannerLoad();
    }

    public void HideBanner()
    {
        _bannerRequested = false;
        _bannerRetry.Cancel();
        if (_banner == null) return;
        _banner.HideAd();
        _bannerVisible = false;
    }

    public void LoadInterstitial() => _interstitial?.LoadAd();

    public async UniTask<bool> ShowInterstitialAsync()
    {
        if (!IsInterstitialReady) return false;

        _interstitialShowTcs = new UniTaskCompletionSource<bool>();
        _interstitial.ShowAd();
        return await _interstitialShowTcs.Task;
    }

    public void LoadRewarded() => RequestRewardedLoad();

    public async UniTask<bool> ShowRewardedAsync()
    {
        if (!IsRewardedReady) return false;

        OnRewardedAvailabilityChanged?.Invoke(false);
        _rewardGranted = false;
        _rewardedShowTcs = new UniTaskCompletionSource<bool>();
        _rewarded.ShowAd();
        return await _rewardedShowTcs.Task;
    }

    private void CreateBanner()
    {
        // 상단 중앙 + 세이프에어리어 준수(노치/상태바 회피).
        _bannerSize = LevelPlayAdSize.BANNER;
        _bannerHeightDp = _bannerSize.Height; // 로드 전 폴백 dp 높이
        var config = new LevelPlayBannerAd.Config.Builder()
            .SetSize(_bannerSize)
            .SetPosition(BannerPos)
            .SetDisplayOnLoad(true)
            .SetRespectSafeArea(true)
            .Build();
        _banner = new LevelPlayBannerAd(BannerAdUnitId, config);

        _banner.OnAdLoaded += OnBannerLoaded;
        _banner.OnAdLoadFailed += OnBannerLoadFailed;
        _banner.OnAdDisplayFailed += OnBannerDisplayFailed;
        _banner.OnAdImpressionDataReady += OnImpressionDataReady;
    }

    private void CreateInterstitial()
    {
        _interstitial = new LevelPlayInterstitialAd(InterstitialAdUnitId);
        _interstitial.OnAdClosed += _ => _interstitialShowTcs?.TrySetResult(true);
        _interstitial.OnAdDisplayFailed += (_, __) => _interstitialShowTcs?.TrySetResult(false);
        _interstitial.OnAdImpressionDataReady += OnImpressionDataReady;
        // 최초 로드는 AdsService.InitializeAsync가 LoadInterstitial()로 건다.
    }

    private void CreateRewarded()
    {
        _rewarded = new LevelPlayRewardedAd(RewardedAdUnitId);
        _rewarded.OnAdLoaded += OnRewardedLoaded;
        _rewarded.OnAdLoadFailed += OnRewardedLoadFailed;
        _rewarded.OnAdRewarded += (_, __) => _rewardGranted = true;
        _rewarded.OnAdImpressionDataReady += OnImpressionDataReady;
        _rewarded.OnAdClosed += _ =>
        {
            OnRewardedAvailabilityChanged?.Invoke(false);
            ResolveRewardedNextFrame().Forget();
        };
        _rewarded.OnAdDisplayFailed += (_, error) =>
        {
            Debug.LogWarning($"[LevelPlayProvider] 리워드 표시 실패: {error}");
            OnRewardedAvailabilityChanged?.Invoke(false);
            _rewardedShowTcs?.TrySetResult(false);
        };
    }

    // SDK가 백그라운드 스레드에서 호출하므로 값만 꺼내고 발화는 메인 스레드로 넘긴다.
    private void OnImpressionDataReady(LevelPlayImpressionData data)
    {
        if (data?.Revenue == null) return;

        var impression = new AdImpression(
            AdPlatformName,
            data.AdNetwork,
            data.AdFormat,
            data.MediationAdUnitName,
            data.Revenue.Value);

        UniTask.Post(() => OnAdImpression?.Invoke(impression));
    }

    private void OnRewardedLoaded(LevelPlayAdInfo info)
    {
        _rewardedLoadInProgress = false;
        _rewardedRetry.Reset();
        Debug.Log($"[LevelPlayProvider] 리워드 로드 성공: {info}");
        OnRewardedAvailabilityChanged?.Invoke(true);
    }

    private void OnRewardedLoadFailed(LevelPlayAdError error)
    {
        _rewardedLoadInProgress = false;
        Debug.LogWarning($"[LevelPlayProvider] 리워드 로드 실패: {error}");
        OnRewardedAvailabilityChanged?.Invoke(false);
        if (_rewarded != null) _rewardedRetry.Schedule(RequestRewardedLoad);
    }

    private void RequestRewardedLoad()
    {
        if (_rewarded == null || IsRewardedReady || _rewardedLoadInProgress) return;

        _rewardedLoadInProgress = true;
        _rewarded.LoadAd();
    }

    // OnAdRewarded/OnAdClosed 호출 순서가 플랫폼마다 달라, 닫힘 후 한 프레임 뒤 보상 여부를 확정한다.
    private async UniTaskVoid ResolveRewardedNextFrame()
    {
        await UniTask.NextFrame();
        _rewardedShowTcs?.TrySetResult(_rewardGranted);
    }

    // 실제 로드된 광고 높이를 우선 사용한다(어댑티브·커스텀 크기 대응).
    private void OnBannerLoaded(LevelPlayAdInfo info)
    {
        _bannerLoadInProgress = false;
        _bannerHasLoaded = true;
        _bannerRetry.Reset();

        int height = info?.AdSize?.Height ?? 0;
        if (height > 0) _bannerHeightDp = height;

        if (_bannerRequested)
        {
            // displayOnLoad=true가 기본값이지만 의도를 명확히 하고 숨김 도중 완료된 로드도 처리한다.
            _banner.ShowAd();
            _bannerVisible = true;
        }
        else
        {
            _banner.HideAd();
            _bannerVisible = false;
        }
    }

    private void OnBannerLoadFailed(LevelPlayAdError error)
    {
        _bannerLoadInProgress = false;
        Debug.LogWarning($"[LevelPlayProvider] 배너 로드 실패: {error}");

        // 한 번이라도 로드된 뒤의 실패는 SDK 자동 갱신 실패일 수 있다. 직전 배너는 유지하고
        // SDK의 다음 자동 갱신에 맡긴다. 최초 로드 실패일 때만 직접 재시도한다.
        if (!_bannerHasLoaded)
        {
            _bannerVisible = false;
            ScheduleBannerRetry();
        }
    }

    private void OnBannerDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
    {
        Debug.LogWarning($"[LevelPlayProvider] 배너 표시 실패: {error}");
        _bannerHasLoaded = false;
        _bannerVisible = false;
        ScheduleBannerRetry();
    }

    private void RequestBannerLoad()
    {
        if (!_bannerRequested || _banner == null || _bannerLoadInProgress) return;

        _bannerLoadInProgress = true;
        _banner.LoadAd(); // displayOnLoad=true이므로 성공하면 자동 표시된다.
    }

    private void ScheduleBannerRetry()
    {
        if (!_bannerRequested || _banner == null) return;
        _bannerRetry.Schedule(RequestBannerLoad);
    }
}
#endif
