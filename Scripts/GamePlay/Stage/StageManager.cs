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

    private StageContext _current;

    public event Action<GameState> OnStateChanged;
    public event Action<Bounds> OnStageCleared;

    public GameState CurrentState { get; private set; } = GameState.None;
    public int CurrentStageIndex { get; private set; }
    public bool HasNextStage => stageList != null && stageList.IsValidIndex(CurrentStageIndex + 1);
    public StageDataSO CurrentStageData => _current?.Data;
    public BananaThrowController BananaController => _current?.Banana;
    public MonkeyArm MonkeyArm => _current?.MonkeyArm;


    protected override void Awake()
    {
        base.Awake();

        if (builder != null)
            builder.OnActorsCreated += BindActors;
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
        builder.ResetBanana(_current);
        SetState(GameState.StageReady);
        Debug.Log($"[StageManager] 스테이지 셋업 완료: {data.stageName}");
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

    public void SetState(GameState newState)
    {
        if (CurrentState == newState) return;

        var prev = CurrentState;
        CurrentState = newState;

        Debug.Log($"[StageManager] {prev} → {newState}");

        OnStateChanged?.Invoke(newState);
    }

    public void ResetBanana()
    {
        builder.ResetBanana(_current);
        SetState(GameState.Ready);
    }

    private void BindActors(StageContext ctx)
    {
        ctx.Banana.OnDropped += () =>
        {
            SetState(GameState.Falling);
            ctx.SettleDetector.BeginJudging();
        };

        ctx.SettleDetector.OnSettled += bounds =>
        {
            ctx.Banana.FreezeInPlace();
            SetState(GameState.Result);
            OnStageCleared?.Invoke(bounds);
        };

        ctx.SettleDetector.OnFell += () => RespawnAfterDelayAsync().Forget();
    }

    private async UniTaskVoid RespawnAfterDelayAsync()
    {
        await UniTask.Delay((int)(respawnDelay * 1000));
        ResetBanana();
    }
}
