using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class StageManager : MonoSingleton<StageManager>
{
    [Header("스테이지 데이터")]
    [SerializeField] private StageListSO stageList;

    [Header("빌더")]
    [SerializeField] private StageBuilder builder;

    [Header("리스폰")]
    [SerializeField] private float respawnDelay = 0.5f;

    [Header("스킵")]
    [Tooltip("이 횟수만큼 실패하면 스킵 버튼이 활성화된다")]
    [Min(1)]
    [SerializeField] private int skipTryThreshold = 3;

    private StageContext _current;
    private int _currentTryCount;
    private int _failsSinceClear;   // 마지막 클리어·스킵 이후 누적 실패. 전면 광고(인터1) 판정용.
    private int _sessionTryCount;   // 세션 총 실패. 로그 확인용.

    // 현재 스테이지 수명에 묶인 비동기 작업의 취소원. 스테이지가 교체되면 끊긴다.
    private CancellationTokenSource _stageCts;

    public event Action<GameState> OnStateChanged;
    // 새 스테이지 로드 완료. 상태 전이가 아니라 로드마다 1회 발화한다.
    public event Action OnStageLoaded;
    public event Action<Bounds> OnStageCleared;
    public event Action<bool> OnSkipAvailabilityChanged;
    public event Action<int> OnTryCountChanged;   // 실패 시도 횟수 변경 (리워드 스킨 로테이션·전면 광고 판단용)
    public event Action<bool> OnSettlingChanged;  // 안착 임박(판정 타이머 누적) 여부 변경

    public GameState CurrentState { get; private set; } = GameState.None;
    public int CurrentTryCount => _currentTryCount;
    public int FailsSinceClear => _failsSinceClear;
    public int CurrentStageIndex { get; private set; }
    public int StageCount => stageList != null ? stageList.Count : 0;
    public bool HasNextStage => stageList != null && stageList.IsValidIndex(CurrentStageIndex + 1);
    public int SkipTryThreshold => skipTryThreshold;
    public bool IsSkipAvailable => _currentTryCount >= skipTryThreshold;
    public bool IsSettling => _current?.Squish != null && _current.Squish.IsSettling;
    public StageDataSO CurrentStageData => _current?.Data;
    public Squish Squish => _current?.Squish;
    public ArmController ArmController => _current?.ArmController;

    protected override void Awake()
    {
        base.Awake();

        if (builder != null)
            builder.OnActorsCreated += BindActors;
    }

    private void OnDestroy()
    {
        _stageCts?.Cancel();
        _stageCts?.Dispose();
        _stageCts = null;
    }

    public void LoadSavedStage()
    {
        int index = Mathf.Clamp(GetHighestClearedIndex() + 1, 0, StageCount - 1);
        LoadStage(index);
    }

    public void LoadNextStage()
    {
        if (HasNextStage)
        {
            LoadStage(CurrentStageIndex + 1);
        }
        else
        {
            Debug.Log("[StageManager] 마지막 스테이지입니다.");
            LoadStage(CurrentStageIndex);
        }
    }

    public void LoadStage(int index)
    {
        if (stageList == null || !stageList.IsValidIndex(index))
        {
            Debug.LogWarning($"[StageManager] 유효하지 않은 스테이지 인덱스: {index}");
            return;
        }

        CurrentStageIndex = index;
        LoadStageData(stageList.Get(index));
    }

    public void LoadStageData(StageDataSO data)
    {
        // 이전 스테이지에서 예약된 지연 작업(리스폰 등)이 새 스테이지를 건드리지 못하게 먼저 끊는다.
        ResetStageScope();

        _current = builder.Build(data, _stageCts.Token);
        RespawnSquish();
        ResetTryCount();
        SetState(GameState.StageReady);

        // SetState는 같은 상태면 조기 return하므로, 로드 통지는 상태와 분리해 항상 내보낸다.
        OnStageLoaded?.Invoke();

        Debug.Log($"[StageManager] 스테이지 셋업 완료: {data.stageId}");
    }

    public void SkipCurrentStage()
    {
        Debug.Log($"[StageManager] 스테이지 스킵: {CurrentStageIndex}");
        LogFirstClear(true);
        MarkCleared(CurrentStageIndex);
        LoadNextStage();
    }

    public void SetState(GameState newState)
    {
        if (CurrentState == newState) return;

        var prev = CurrentState;
        CurrentState = newState;

        Debug.Log($"[StageManager] {prev} → {newState}");

        OnStateChanged?.Invoke(newState);

        // Ready 진입(스테이지 시작 터치 / 재도전) 시 준비된 스퀴시를 화면 안으로 등장시킨다.
        if (newState == GameState.Ready && _current?.Squish != null)
            _current.Squish.Enter();
    }

    public void ResetSquish()
    {
        RespawnSquish();
        SetState(GameState.Ready);
    }

    // 팝업이 떠 있는 동안 안착·낙하 판정을 멈춘다(물리는 계속 돈다).
    public void SetJudgingPaused(bool paused) => _current?.Squish?.SetJudgingPaused(paused);

    // 스테이지 고유 클리어 연출을 모두 끝까지 기다린다. 스테이지가 교체되면 취소된다.
    public UniTask PlayClearEffectsAsync()
    {
        var effects = _current?.ClearEffects;
        if (effects == null || effects.Count == 0) return UniTask.CompletedTask;

        var token = _stageCts.Token;
        return UniTask.WhenAll(effects.Select(effect => effect.PlayClearEffectAsync(token)));
    }

    public StageDataSO GetStageData(int index) =>
        stageList != null && stageList.IsValidIndex(index) ? stageList.Get(index) : null;

    public bool IsCleared(int index) => index >= 0 && index <= GetHighestClearedIndex();

    public int GetHighestClearedIndex()
    {
        // 디버그 해금 빌드는 전 스테이지를 클리어한 것으로 취급한다(세이브 값은 건드리지 않는다).
        if (BuildFlags.DebugUnlock) return StageCount - 1;

        if (!Services.TryGet<ISaveService>(out var save)) return -1;
        if (save.Data?.stage == null) return -1;
        return save.Data.stage.highestClearedIndex;
    }

    private void MarkCleared(int index)
    {
        // 클리어와 스킵이 모두 여기를 지난다. 막힘 지표라 진행도 갱신 여부와 무관하게 리셋한다.
        _failsSinceClear = 0;

        if (!Services.TryGet<ISaveService>(out var save)) return;
        if (save.Data?.stage == null) return;
        if (index <= save.Data.stage.highestClearedIndex) return;

        save.Data.stage.highestClearedIndex = index;
        save.Save();
        Debug.Log($"[StageManager] 클리어 갱신: {index}");

        // 진행도가 오르는 유일한 지점이라 여기서만 랭킹에 올린다.
        if (Services.TryGet<ILeaderboardService>(out var leaderboard)) leaderboard.SubmitHighestStage(index);
    }

    // 진행도를 올리는 클리어·스킵만 기록하므로 MarkCleared보다 먼저 호출해야 한다. 재클리어는 제외된다.
    // 시도 횟수는 스테이지 로드·앱 재시작 때 0으로 돌아가는 현재 스테이지 기준이다.
    private void LogFirstClear(bool skipped)
    {
        if (!Services.TryGet<ISaveService>(out var save)) return;
        if (save.Data?.stage == null || CurrentStageIndex <= save.Data.stage.highestClearedIndex) return;
        if (!Services.TryGet<IAnalyticsService>(out var analytics)) return;

        // 클리어는 성공한 시도를 포함하고, 스킵은 실패한 시도만 센다.
        int attemptCount = skipped ? _currentTryCount : _currentTryCount + 1;
        bool rewardWatched = AdCoordinator.TryGetInstance(out var adCoordinator) &&
            adCoordinator.RewardWatchedThisStage;
        analytics.LogStageClear(CurrentStageIndex, attemptCount, rewardWatched, skipped);
    }

    private void IncrementTryCount()
    {
        bool wasAvailable = IsSkipAvailable;
        _currentTryCount++;
        _failsSinceClear++;
        _sessionTryCount++;
        Debug.Log($"[StageManager] 실패 트라이: 스테이지 {_currentTryCount}/{skipTryThreshold} · 클리어 이후 {_failsSinceClear} · 세션 누적 {_sessionTryCount}");

        OnTryCountChanged?.Invoke(_currentTryCount);

        if (!wasAvailable && IsSkipAvailable)
            OnSkipAvailabilityChanged?.Invoke(true);
    }

    private void ResetTryCount()
    {
        _currentTryCount = 0;
        OnTryCountChanged?.Invoke(_currentTryCount);
        OnSkipAvailabilityChanged?.Invoke(false);
    }

    // 스테이지 수명 취소원을 새로 연다. 이전 스코프에 걸린 대기는 여기서 취소된다.
    private void ResetStageScope()
    {
        _stageCts?.Cancel();
        _stageCts?.Dispose();
        _stageCts = new CancellationTokenSource();
    }

    // 현재 스퀴시를 스폰 위치로 되돌린다(화면 밖 준비). 화면 안 등장은 Ready 진입 시 SetState에서.
    private void RespawnSquish()
    {
        if (_current?.Squish != null)
            _current.Squish.RespawnAt();
    }

    private void BindActors(StageContext ctx)
    {
        ctx.Squish.OnDropped += OnSquishDropped;
        ctx.Squish.OnSettled += OnSquishSettled;
        ctx.Squish.OnFell += OnSquishFell;
        ctx.Squish.OnSettlingChanged += OnSquishSettlingChanged;
    }

    private void OnSquishSettlingChanged(bool settling) => OnSettlingChanged?.Invoke(settling);

    private void OnSquishDropped()
    {
        SetState(GameState.Falling);
        _current.Squish.BeginJudging();
    }

    private void OnSquishSettled(Bounds bounds)
    {
        _current.Squish.Freeze();
        SetState(GameState.Result);
        LogFirstClear(false);
        MarkCleared(CurrentStageIndex);

        if (SoundManager.TryGetInstance(out var sound))
            sound.PlaySfx(SfxType.StageClear);

        OnStageCleared?.Invoke(bounds);
    }

    private void OnSquishFell()
    {
        IncrementTryCount();
        RespawnAfterDelayAsync(_stageCts.Token).Forget();
    }

    private async UniTaskVoid RespawnAfterDelayAsync(CancellationToken token)
    {
        bool canceled = await UniTask.Delay((int)(respawnDelay * 1000), cancellationToken: token)
            .SuppressCancellationThrow();
        if (canceled) return;

        ResetSquish();
    }
}
