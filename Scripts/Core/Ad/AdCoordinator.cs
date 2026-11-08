using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 광고 트리거·정책 중재자.
public class AdCoordinator : MonoSingleton<AdCoordinator>
{
    [Header("데이터")]
    [SerializeField] private SkinDatabaseSO skinDatabase;

    [Header("전면 - 공통")]
    [Tooltip("세션 시작 후 이 시간(초)이 지나야 전면 노출")]
    [SerializeField] private float minSessionPlaySeconds = 30f;

    [Header("전면 - 실패")]
    [Tooltip("마지막 클리어·스킵·전면 이후 이 횟수 이상 실패하면 전면 노출. 스테이지를 옮겨도 이어서 센다")]
    [Min(1)]
    [SerializeField] private int interstitialFailThreshold = 8;

    [Header("전면 - 클리어")]
    [Tooltip("마지막 전면 이후 이 개수 이상 클리어하면 클리어 연출 뒤 전면 노출")]
    [Min(1)]
    [SerializeField] private int interstitialClearThreshold = 2;

    [Header("리워드 - 스킨 체험(리워드1)")]
    [Tooltip("실패 시도가 이 횟수에 이르면 체험 버튼이 처음 나타난다")]
    [Min(1)]
    [SerializeField] private int skinTrialFirstTry = 3;

    [Tooltip("첫 노출 이후 실패 시도 몇 회마다 체험 후보 스킨이 로테이션되는지")]
    [Min(1)]
    [SerializeField] private int skinTrialRotationPeriod = 3;

    [Header("광고 제거 유도")]
    [Tooltip("전면 노출 후 광고 제거 버튼이 떠 있는 실패 횟수")]
    [Min(1)]
    [SerializeField] private int removeAdsPromptFailCount = 3;

    private StageManager _stage;
    private IAdsService _ads;
    private IPurchaseService _purchase;
    private ISkinService _skin;
    private IRemoteConfigService _remoteConfig;

    // 인스펙터 값을 기본값으로 원격 설정을 덮어쓴 적용 값
    private float _minSessionPlaySeconds;
    private int _interstitialFailThreshold;
    private int _interstitialClearThreshold;
    private int _skinTrialFirstTry;
    private int _skinTrialRotationPeriod;
    private int _removeAdsPromptFailCount;

    private readonly List<SquishSkinSO> _trialPool = new();

    private bool _rewardWatchedThisStage;
    private int _clearsSinceInterstitial;
    private int _failsAtLastInterstitial;   // 전면 노출 시점의 FailsSinceClear. 클리어 이후 실패 중 전면 이후분만 세기 위한 기준값
    private float _sessionStartTime;

    private int _currentTrialStep = -1;      // 현재 로테이션 주기 인덱스(경계에서만 새로 뽑기 위한 캐시 키)
    private SquishSkinSO _currentTrialSkin;  // 현재 주기의 체험 후보(직전 제외 랜덤으로 뽑힘)

    private int _removeAdsPromptFailsLeft;   // 광고 제거 버튼이 사라지기까지 남은 실패 횟수
    private bool _removeAdsPromptPending;    // 클리어 전면 이후 다음 스테이지 로드 때 노출을 시작할지

    public event System.Action OnRemoveAdsPromptChanged;

    private float SessionPlaySeconds => Time.realtimeSinceStartup - _sessionStartTime;
    private int FailsSinceInterstitial => _stage.FailsSinceClear - _failsAtLastInterstitial;

    public bool RewardWatchedThisStage => _rewardWatchedThisStage;

    // 올스킨 보유(전체 광고 제거)면 체험이 불필요. 후보가 있어야 노출.
    public bool IsSkinTrialAvailable =>
        _initialized && skinDatabase != null && !_purchase.AllSkinsUnlocked && GetTrialPool().Count > 0;

    // 전면 직후 남은 실패 횟수 동안 노출한다. 이미 구매했거나 스토어에서 상품이 조회되지 않으면 띄우지 않는다.
    public bool IsRemoveAdsPromptActive =>
        _initialized
        && _removeAdsPromptFailsLeft > 0
        && !_purchase.RemoveAllAds
        && _purchase.TryGetPrice(ProductIds.RemoveAllAds, out _);

    // 건너뛰기는 전면 주기에서 광고 제거 노출을 뺀 실패 동안 연속으로 보이므로, 그 구간의 중간쯤에 한 번 리마인드한다.
    // 교체가 없는 스테이지에서는 이 주기로 반복되므로 매 실패마다 흔들리지 않게 최소 2로 둔다.
    public int SkipReminderInterval =>
        Mathf.Max(2, Mathf.CeilToInt((_interstitialFailThreshold - _removeAdsPromptFailCount) / 2f));

    // 이번 클리어에서 전면을 노출할 조건이 되는지 판정한다
    public bool ShouldShowClearInterstitial =>
        _initialized
        && _clearsSinceInterstitial >= _interstitialClearThreshold
        && CanShowInterstitial();

#if UNITY_EDITOR
    public bool DebugInitialized => _initialized;
    public bool DebugCanShowInterstitial => _initialized && CanShowInterstitial();
    public float DebugSessionPlaySeconds => SessionPlaySeconds;
    public float DebugMinSessionPlaySeconds => _minSessionPlaySeconds;
    public int DebugFailsSinceInterstitial => _initialized ? FailsSinceInterstitial : 0;
    public int DebugInterstitialFailThreshold => _interstitialFailThreshold;
    public int DebugClearsSinceInterstitial => _clearsSinceInterstitial;
    public int DebugInterstitialClearThreshold => _interstitialClearThreshold;
    public int DebugRemoveAdsPromptFailsLeft => _removeAdsPromptFailsLeft;
    public int DebugRemoveAdsPromptFailCount => _removeAdsPromptFailCount;
    public bool DebugRemoveAdsPromptPending => _removeAdsPromptPending;
#endif

    private bool _initialized;

    protected override void Awake()
    {
        base.Awake();
        _sessionStartTime = Time.realtimeSinceStartup;
    }

    private void OnDestroy()
    {
        if (_stage != null)
        {
            _stage.OnStageLoaded -= OnStageLoaded;
            _stage.OnTryCountChanged -= OnTryCountChanged;
            _stage.OnStageCleared -= OnStageCleared;
        }

        if (_initialized) _ads.SetBannerSide(BannerSide.Bottom);
    }

    public void Initialize()
    {
        if (_initialized) return;
        _ads = Services.Get<IAdsService>();
        _purchase = Services.Get<IPurchaseService>();
        _skin = Services.Get<ISkinService>();
        _remoteConfig = Services.Get<IRemoteConfigService>();

        if (!StageManager.TryGetInstance(out _stage))
        {
            Debug.LogError("[AdCoordinator] StageManager가 없어 스테이지별 광고 정책을 초기화할 수 없습니다.");
            enabled = false;
            return;
        }

        ApplyRemoteConfig();

        _stage.OnStageLoaded += OnStageLoaded;
        _stage.OnTryCountChanged += OnTryCountChanged;
        _stage.OnStageCleared += OnStageCleared;
        _initialized = true;

        // 첫 스테이지는 구독 전에 로드되므로 배너 표시 전에 현재 설정을 적용한다.
        ApplyStageBannerSide();

        // 배너는 게임 진입과 함께 상시 노출(배너 제거 구매 시 서비스가 무시).
        _ads.ShowBanner();
    }

    // 리워드 광고 시청 시 호출. 해당 스테이지의 전면을 면제하고 광고 제거 버튼을 띄운다.
    public void MarkRewardWatched()
    {
        _rewardWatchedThisStage = true;
        StartRemoveAdsPrompt();
    }

    // 현재 시도 횟수에 대응하는 체험 후보 스킨 반환.
    public SquishSkinSO GetTrialSkin(int tryCount)
    {
        if (!_initialized || tryCount < _skinTrialFirstTry) return null;

        var pool = GetTrialPool();
        if (pool.Count == 0) return null;

        int step = (tryCount - _skinTrialFirstTry) / _skinTrialRotationPeriod;
        if (step != _currentTrialStep)
        {
            _currentTrialStep = step;
            _currentTrialSkin = PickRandomExcluding(pool, _currentTrialSkin);
        }
        return _currentTrialSkin;
    }

#if UNITY_EDITOR
    // 세션 시작 후 전면 유예 시간이 이미 지난 것으로 만든다.
    public void DebugSkipSessionGrace() =>
        _sessionStartTime = Mathf.Min(_sessionStartTime, Time.realtimeSinceStartup - _minSessionPlaySeconds);

    // 실패 누적 없이 실패 전면을 시도한다. 공통 게이트(리워드 면제·광고 제거·세션 유예)는 그대로 적용된다.
    public void DebugTryShowInterstitial()
    {
        if (!_initialized) return;
        if (!CanShowInterstitial())
            Debug.Log("[AdCoordinator] 전면 게이트에 막혀 표시하지 않습니다");

        TryShowInterstitialAsync().Forget();
    }

    public void DebugStartRemoveAdsPrompt() => StartRemoveAdsPrompt();
#endif

    // 클리어 연출이 끝난 뒤 조건 통과 시 전면을 자동 노출한다
    public void NotifyStageClearPresented()
    {
        if (!ShouldShowClearInterstitial) return;

        TryShowInterstitialAsync(true).Forget();
    }

    // Squish 탭 스킨 중 미보유·미장착인 것들(체험 대상).
    private List<SquishSkinSO> GetTrialPool()
    {
        _trialPool.Clear();
        if (!_initialized || skinDatabase == null) return _trialPool;

        var tab = skinDatabase.GetTab(SkinCategory.Squish);
        if (tab == null) return _trialPool;

        foreach (var skin in tab.skins)
            if (skin is SquishSkinSO squishSkin
                && !_skin.IsOwned(tab, skin)
                && !_skin.IsEquipped(SkinCategory.Squish, skin))
                _trialPool.Add(squishSkin);

        return _trialPool;
    }

    // 직전 후보를 제외하고 랜덤으로 하나 뽑는다. 후보가 1개뿐이면 불가피하게 동일한 후보를 반환한다.
    private SquishSkinSO PickRandomExcluding(List<SquishSkinSO> pool, SquishSkinSO exclude)
    {
        if (pool.Count == 1) return pool[0];

        SquishSkinSO pick;
        do { pick = pool[Random.Range(0, pool.Count)]; }
        while (pick == exclude);
        return pick;
    }

    // 클리어 전면은 결과 화면 위에서 나오므로, 광고 제거 버튼은 다음 스테이지가 시작될 때 띄운다.
    private async UniTaskVoid TryShowInterstitialAsync(bool deferPromptToNextStage = false)
    {
        if (!CanShowInterstitial()) return;

        bool shown = await _ads.ShowInterstitialAsync();
        if (!shown) return;

        _clearsSinceInterstitial = 0;
        _failsAtLastInterstitial = _stage.FailsSinceClear;

        if (deferPromptToNextStage) _removeAdsPromptPending = true;
        else StartRemoveAdsPrompt();
    }

    // 전면 공통 게이트(리워드 면제·광고 제거·세션 시작 유예)
    private bool CanShowInterstitial() =>
        !_rewardWatchedThisStage
        && !_purchase.RemoveAllAds
        && SessionPlaySeconds >= _minSessionPlaySeconds;

    // 스테이지 도중 기준이 바뀌지 않도록 초기화와 스테이지 로드 때만 원격 값을 다시 읽는다.
    private void ApplyRemoteConfig()
    {
        _minSessionPlaySeconds = _remoteConfig.GetFloat(RemoteConfigKeys.AdMinSessionSeconds, minSessionPlaySeconds, 0f);
        _interstitialFailThreshold = _remoteConfig.GetInt(RemoteConfigKeys.AdInterstitialFailThreshold, interstitialFailThreshold, 1);
        _interstitialClearThreshold = _remoteConfig.GetInt(RemoteConfigKeys.AdInterstitialClearThreshold, interstitialClearThreshold, 1);
        _skinTrialFirstTry = _remoteConfig.GetInt(RemoteConfigKeys.SkinTrialFirstTry, skinTrialFirstTry, 1);
        _skinTrialRotationPeriod = _remoteConfig.GetInt(RemoteConfigKeys.SkinTrialRotationPeriod, skinTrialRotationPeriod, 1);
        _removeAdsPromptFailCount = _remoteConfig.GetInt(RemoteConfigKeys.RemoveAdsPromptFailCount, removeAdsPromptFailCount, 1);
    }

    private void ApplyStageBannerSide()
    {
        var data = _stage.CurrentStageData;
        _ads.SetBannerSide(data != null ? data.AppsInTossBannerSide : BannerSide.Bottom);
    }

    // 새 스테이지 시작 시 per-stage 상태 초기화.
    private void OnStageLoaded()
    {
        ApplyRemoteConfig();
        ApplyStageBannerSide();
        _rewardWatchedThisStage = false;
        _currentTrialStep = -1; // 새 스테이지 첫 주기에서 다시 뽑도록(직전 제외는 _currentTrialSkin으로 유지)

        // 스킵으로 FailsSinceClear가 0이 되면 기준값도 함께 맞춘다.
        _failsAtLastInterstitial = Mathf.Min(_failsAtLastInterstitial, _stage.FailsSinceClear);

        if (_removeAdsPromptPending)
        {
            _removeAdsPromptPending = false;
            StartRemoveAdsPrompt();
        }
    }

    // 클리어하면 FailsSinceClear가 0이 되므로 기준값도 함께 초기화한다.
    private void OnStageCleared(Bounds bounds)
    {
        _clearsSinceInterstitial++;
        _failsAtLastInterstitial = 0;
    }

    private void StartRemoveAdsPrompt()
    {
        _removeAdsPromptFailsLeft = _removeAdsPromptFailCount;
        OnRemoveAdsPromptChanged?.Invoke();
    }

    private void CountDownRemoveAdsPrompt()
    {
        if (_removeAdsPromptFailsLeft <= 0) return;

        _removeAdsPromptFailsLeft--;
        if (_removeAdsPromptFailsLeft == 0) OnRemoveAdsPromptChanged?.Invoke();
    }

    // 한 지점에 막혀 있을 때 강제 전면.
    // 스테이지별 시도 횟수는 스테이지를 옮길 때마다 0이 되어 전면을 계속 피할 수 있으므로 쓰지 않는다.
    private void OnTryCountChanged(int tryCount)
    {
        if (tryCount <= 0) return;   // 스테이지 리셋 통지는 실패가 아니다

        CountDownRemoveAdsPrompt();

        if (FailsSinceInterstitial < _interstitialFailThreshold) return;

        TryShowInterstitialAsync().Forget();
    }
}
