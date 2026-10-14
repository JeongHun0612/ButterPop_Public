using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 광고 트리거·정책 중재자.
public class AdCoordinator : MonoSingleton<AdCoordinator>
{
    [Header("데이터")]
    [SerializeField] private SkinDatabaseSO skinDatabase;

    [Header("전면 - 실패(인터1)")]
    [Tooltip("마지막 클리어 이후 이 횟수만큼 실패할 때마다 강제 전면(리워드 시청 시 면제). 스테이지를 옮겨도 리셋되지 않고, 클리어·스킵 시 초기화된다")]
    [Min(1)]
    [SerializeField] private int interstitialFailThreshold = 10;

    [Header("전면 - 클리어(인터2)")]
    [Tooltip("이번 세션에서 이 개수 이상 스테이지를 클리어한 뒤부터 클리어 전면 노출")]
    [Min(0)]
    [SerializeField] private int minSessionClearsForAd = 3;
    [Tooltip("세션 누적 플레이가 이 시간(초) 이상이어야 클리어 전면 노출")]
    [SerializeField] private float minSessionPlaySeconds = 90f;

    [Header("전면 - 공통 빈도")]
    [Tooltip("직전 전면 이후 이 시간(초)이 지나야 다음 전면 노출")]
    [SerializeField] private float interstitialCooldownSeconds = 75f;
    [Tooltip("한 세션에서 노출할 전면 최대 횟수")]
    [Min(0)]
    [SerializeField] private int sessionInterstitialCap = 5;

    [Header("리워드 - 스킨 체험(리워드1)")]
    [Tooltip("실패 시도 몇 회마다 체험 후보 스킨이 로테이션되는지")]
    [Min(1)]
    [SerializeField] private int skinTrialRotationPeriod = 3;

    private StageManager _stage;
    private IAdsService _ads;
    private IPurchaseService _purchase;
    private ISkinService _skin;

    private readonly List<SquishSkinSO> _trialPool = new();

    private bool _rewardWatchedThisStage;
    private int _sessionClearedCount;
    private float _lastInterstitialTime = -999f;
    private int _sessionInterstitialCount;
    private float _sessionStartTime;

    private int _currentTrialStep = -1;      // 현재 로테이션 주기 인덱스(경계에서만 새로 뽑기 위한 캐시 키)
    private SquishSkinSO _currentTrialSkin;  // 현재 주기의 체험 후보(직전 제외 랜덤으로 뽑힘)

    private float SessionPlaySeconds => Time.realtimeSinceStartup - _sessionStartTime;

    public bool RewardWatchedThisStage => _rewardWatchedThisStage;

    // 올스킨 보유(전체 광고 제거)면 체험이 불필요. 후보가 있어야 노출.
    public bool IsSkinTrialAvailable =>
        _initialized && skinDatabase != null && !_purchase.AllSkinsUnlocked && GetTrialPool().Count > 0;

    // 이번 클리어에서 전면을 노출할 조건이 되는지 판정한다
    public bool ShouldShowClearInterstitial =>
        _initialized
        && _sessionClearedCount >= minSessionClearsForAd
        && SessionPlaySeconds >= minSessionPlaySeconds
        && CanShowInterstitial();

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

        if (!StageManager.TryGetInstance(out _stage))
        {
            Debug.LogError("[AdCoordinator] StageManager가 없어 스테이지별 광고 정책을 초기화할 수 없습니다.");
            enabled = false;
            return;
        }

        _stage.OnStageLoaded += OnStageLoaded;
        _stage.OnTryCountChanged += OnTryCountChanged;
        _stage.OnStageCleared += OnStageCleared;
        _initialized = true;

        // 첫 스테이지는 구독 전에 로드되므로 배너 표시 전에 현재 설정을 적용한다.
        ApplyStageBannerSide();

        // 배너는 게임 진입과 함께 상시 노출(배너 제거 구매 시 서비스가 무시).
        _ads.ShowBanner();
    }

    // 리워드 광고 시청 시 호출. 해당 스테이지의 전면을 면제한다.
    public void MarkRewardWatched() => _rewardWatchedThisStage = true;

    // 현재 시도 횟수에 대응하는 체험 후보 스킨 반환.
    public SquishSkinSO GetTrialSkin(int tryCount)
    {
        if (tryCount < skinTrialRotationPeriod) return null;

        var pool = GetTrialPool();
        if (pool.Count == 0) return null;

        int step = tryCount / skinTrialRotationPeriod - 1;
        if (step != _currentTrialStep)
        {
            _currentTrialStep = step;
            _currentTrialSkin = PickRandomExcluding(pool, _currentTrialSkin);
        }
        return _currentTrialSkin;
    }

    // 클리어 연출이 끝난 뒤 조건 통과 시 전면을 자동 노출한다
    public void NotifyStageClearPresented()
    {
        if (!ShouldShowClearInterstitial) return;

        TryShowInterstitialAsync().Forget();
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

    private async UniTaskVoid TryShowInterstitialAsync()
    {
        if (!CanShowInterstitial()) return;

        bool shown = await _ads.ShowInterstitialAsync();
        if (!shown) return;

        _lastInterstitialTime = Time.realtimeSinceStartup;
        _sessionInterstitialCount++;
    }

    // 전면 공통 게이트(리워드 면제·광고 제거·세션 캡·쿨다운)
    private bool CanShowInterstitial() =>
        !_rewardWatchedThisStage
        && !_purchase.RemoveAllAds
        && _sessionInterstitialCount < sessionInterstitialCap
        && Time.realtimeSinceStartup - _lastInterstitialTime >= interstitialCooldownSeconds;

    private void ApplyStageBannerSide()
    {
        var data = _stage.CurrentStageData;
        _ads.SetBannerSide(data != null ? data.AppsInTossBannerSide : BannerSide.Bottom);
    }

    // 새 스테이지 시작 시 per-stage 상태 초기화.
    private void OnStageLoaded()
    {
        ApplyStageBannerSide();
        _rewardWatchedThisStage = false;
        _currentTrialStep = -1; // 새 스테이지 첫 주기에서 다시 뽑도록(직전 제외는 _currentTrialSkin으로 유지)
    }

    private void OnStageCleared(Bounds bounds) => _sessionClearedCount++;

    // 인터1: 마지막 클리어 이후 N회 실패마다 강제 전면(= 한 지점에 막혀 있을 때).
    // 스테이지 카운터는 스테이지를 옮길 때마다 0이 되어 전면을 계속 피할 수 있으므로 쓰지 않는다.
    private void OnTryCountChanged(int tryCount)
    {
        if (tryCount <= 0) return;   // 스테이지 리셋 통지는 실패가 아니다

        var stage = StageManager.Instance;
        Debug.Assert(stage != null, "[AdCoordinator] StageManager가 없습니다");

        if (stage.FailsSinceClear % interstitialFailThreshold != 0) return;

        TryShowInterstitialAsync().Forget();
    }
}
