// Unity LevelPlay(구 ironSource) 어댑터. com.unity.services.levelplay 9.x 기준.
// LEVELPLAY_SDK 디파인이 있을 때만 컴파일된다(없으면 ServiceBootstrap이 StubAdProvider 사용)
#if LEVELPLAY_SDK
using System;
using Cysharp.Threading.Tasks;
using Unity.Services.LevelPlay;
using UnityEngine;

public class LevelPlayProvider : IAdProvider
{
#if UNITY_ANDROID
    private const string AppKey = "PUBLIC_SAMPLE_CONFIG";
    private const string BannerAdUnitId = "PUBLIC_SAMPLE_CONFIG";
    private const string InterstitialAdUnitId = "PUBLIC_SAMPLE_CONFIG";
    private const string RewardedAdUnitId = "PUBLIC_SAMPLE_CONFIG";
#elif UNITY_IOS
    private const string AppKey = "PUBLIC_SAMPLE_CONFIG";
    private const string BannerAdUnitId = "PUBLIC_SAMPLE_CONFIG";
    private const string InterstitialAdUnitId = "PUBLIC_SAMPLE_CONFIG";
    private const string RewardedAdUnitId = "PUBLIC_SAMPLE_CONFIG";
#else
#error LevelPlayProvider는 Android/iOS 전용입니다. 빌드 타겟을 모바일로 바꾸거나 LEVELPLAY_SDK 디파인을 끄세요.
#endif

    // 성공/실패 콜백이 둘 다 오지 않는 엣지 케이스를 대비한 초기화 대기 상한(초).
    private const float InitTimeoutSeconds = 5f;

    // 배너 위치 단일 소스.
    private static readonly LevelPlayBannerPosition BannerPos = LevelPlayBannerPosition.TopCenter;

    private LevelPlayBannerAd _banner;
    private LevelPlayAdSize _bannerSize;
    private int _bannerHeightDp;   // 배너 dp 높이 캐시(Android에선 Height getter가 JNI 호출).
    private LevelPlayInterstitialAd _interstitial;
    private LevelPlayRewardedAd _rewarded;

    private bool _bannerShown;

    // 표시 중인 전면/리워드의 완료를 await로 받기 위한 완료 소스.
    private UniTaskCompletionSource<bool> _interstitialShowTcs;
    private UniTaskCompletionSource<bool> _rewardedShowTcs;
    private bool _rewardGranted;

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
            if (_banner == null) return 0f;
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
        if (_banner == null) return;
        if (!_bannerShown)
        {
            _bannerShown = true;
            _banner.LoadAd(); // displayOnLoad=true 이므로 로드 완료 시 자동 표시된다.
            return;
        }
        _banner.ShowAd();
    }

    public void HideBanner() => _banner?.HideAd();

    public void LoadInterstitial() => _interstitial?.LoadAd();

    public async UniTask<bool> ShowInterstitialAsync()
    {
        if (!IsInterstitialReady) return false;

        _interstitialShowTcs = new UniTaskCompletionSource<bool>();
        _interstitial.ShowAd();
        return await _interstitialShowTcs.Task;
    }

    public void LoadRewarded() => _rewarded?.LoadAd();

    public async UniTask<bool> ShowRewardedAsync()
    {
        if (!IsRewardedReady) return false;

        _rewardGranted = false;
        _rewardedShowTcs = new UniTaskCompletionSource<bool>();
        _rewarded.ShowAd();
        return await _rewardedShowTcs.Task;
    }

    private void CreateBanner()
    {
        // 상단 중앙 + 세이프에어리어 준수(노치/상태바 회피).
        _bannerSize = LevelPlayAdSize.BANNER;
        _bannerHeightDp = _bannerSize.Height; // dp 높이 1회 캐시
        var config = new LevelPlayBannerAd.Config.Builder()
            .SetSize(_bannerSize)
            .SetPosition(BannerPos)
            .SetRespectSafeArea(true)
            .Build();
        _banner = new LevelPlayBannerAd(BannerAdUnitId, config);
    }

    private void CreateInterstitial()
    {
        _interstitial = new LevelPlayInterstitialAd(InterstitialAdUnitId);
        _interstitial.OnAdClosed += _ => _interstitialShowTcs?.TrySetResult(true);
        _interstitial.OnAdDisplayFailed += (_, __) => _interstitialShowTcs?.TrySetResult(false);
        // 최초 로드는 AdsService.InitializeAsync가 LoadInterstitial()로 건다.
    }

    private void CreateRewarded()
    {
        _rewarded = new LevelPlayRewardedAd(RewardedAdUnitId);
        _rewarded.OnAdRewarded += (_, __) => _rewardGranted = true;
        _rewarded.OnAdClosed += _ => ResolveRewardedNextFrame().Forget();
        _rewarded.OnAdDisplayFailed += (_, __) => _rewardedShowTcs?.TrySetResult(false);
    }

    // OnAdRewarded/OnAdClosed 호출 순서가 플랫폼마다 달라, 닫힘 후 한 프레임 뒤 보상 여부를 확정한다.
    private async UniTaskVoid ResolveRewardedNextFrame()
    {
        await UniTask.NextFrame();
        _rewardedShowTcs?.TrySetResult(_rewardGranted);
    }
}
#endif
