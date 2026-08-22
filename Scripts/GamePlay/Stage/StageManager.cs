using System;
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

    public event Action<GameState> OnStateChanged;
    public event Action<Bounds> OnStageCleared;
    public event Action<bool> OnSkipAvailabilityChanged;
    public event Action<int> OnTryCountChanged;   // 실패 시도 횟수 변경 (리워드 스킨 로테이션·전면 광고 판단용)

    public GameState CurrentState { get; private set; } = GameState.None;
    public int CurrentTryCount => _currentTryCount;
    public int CurrentStageIndex { get; private set; }
    public int StageCount => stageList != null ? stageList.Count : 0;
    public bool HasNextStage => stageList != null && stageList.IsValidIndex(CurrentStageIndex + 1);
    public bool IsSkipAvailable => _currentTryCount >= skipTryThreshold;
    public StageDataSO CurrentStageData => _current?.Data;
    public Squish Squish => _current?.Squish;
    public ArmController ArmController => _current?.ArmController;

    protected override void Awake()
    {
        base.Awake();

        if (builder != null)
            builder.OnActorsCreated += BindActors;
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
        _current = builder.Build(data);
        RespawnSquish();
        ResetTryCount();
        SetState(GameState.StageReady);
        Debug.Log($"[StageManager] 스테이지 셋업 완료: {data.stageId}");
    }

    public void SkipCurrentStage()
    {
        Debug.Log($"[StageManager] 스테이지 스킵: {CurrentStageIndex}");
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

    public StageDataSO GetStageData(int index) =>
        stageList != null && stageList.IsValidIndex(index) ? stageList.Get(index) : null;

    public bool IsCleared(int index) => index >= 0 && index <= GetHighestClearedIndex();

    public int GetHighestClearedIndex()
    {
        if (!Services.TryGet<ISaveService>(out var save)) return -1;
        if (save.Data?.stage == null) return -1;
        return save.Data.stage.highestClearedIndex;
    }

    private void MarkCleared(int index)
    {
        if (!Services.TryGet<ISaveService>(out var save)) return;
        if (save.Data?.stage == null) return;
        if (index <= save.Data.stage.highestClearedIndex) return;

        save.Data.stage.highestClearedIndex = index;
        save.Save();
        Debug.Log($"[StageManager] 클리어 갱신: {index}");
    }

    private void IncrementTryCount()
    {
        bool wasAvailable = IsSkipAvailable;
        _currentTryCount++;
        Debug.Log($"[StageManager] 실패 트라이: {_currentTryCount}/{skipTryThreshold}");

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
    }

    private void OnSquishDropped()
    {
        SetState(GameState.Falling);
        _current.Squish.BeginJudging();
    }

    private void OnSquishSettled(Bounds bounds)
    {
        _current.Squish.Freeze();
        SetState(GameState.Result);
        MarkCleared(CurrentStageIndex);

        if (SoundManager.TryGetInstance(out var sound))
            sound.PlaySfx(SfxType.StageClear);

        OnStageCleared?.Invoke(bounds);
    }

    private void OnSquishFell()
    {
        IncrementTryCount();
        RespawnAfterDelayAsync().Forget();
    }

    private async UniTaskVoid RespawnAfterDelayAsync()
    {
        await UniTask.Delay((int)(respawnDelay * 1000));
        ResetSquish();
    }
}
