using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class StageManager : MonoSingleton<StageManager>
{
    public GameState CurrentState { get; private set; } = GameState.StageReady;
    public event Action<GameState> OnStateChanged;

    [Header("스테이지 데이터")]
    [SerializeField] private StageListSO stageList;

    public int CurrentStageIndex { get; private set; }
    public StageDataSO CurrentStageData { get; private set; }
    public bool HasNextStage => stageList != null && stageList.IsValidIndex(CurrentStageIndex + 1);

    [Header("바나나")]
    [SerializeField] private GameObject bananaPrefab;

    [Header("원숭이 팔")]
    [SerializeField] private GameObject monkeyArmPrefab;

    [Header("성공 연출")]
    [SerializeField] private SuccessEffect successEffectPrefab;

    [Header("참조")]
    [SerializeField] private Camera mainCamera;

    [Header("디버그")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private StageDataSO debugStageData;

    private GameObject _bananaInstance;
    private GameObject _monkeyArmInstance;
    private MonkeyArm _monkeyArm;
    private BananaThrowController _throwController;
    private BananaSettleDetector _settleDetector;
    private readonly List<GameObject> _spawnedStageObjects = new();
    private SuccessEffect _successEffectInstance;
    private CancellationTokenSource _effectCts;

    public BananaThrowController BananaController => _throwController;
    public MonkeyArm MonkeyArm => _monkeyArm;
    public Vector2 BananaSpawnPosition => CurrentStageData != null ? CurrentStageData.bananaSpawnPosition : Vector2.zero;

    protected override void Awake()
    {
        base.Awake();
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    public void LoadNextStage()
    {
        if (HasNextStage)
            LoadStage(CurrentStageIndex + 1);
        else
            Debug.Log("[StageManager] 마지막 스테이지입니다.");
    }

    public void LoadStage(int index)
    {
        if (debugMode)
        {
            if (debugStageData == null)
            {
                Debug.LogWarning("[StageManager] Debug Mode: debugStageData가 비어있습니다.");
                return;
            }

            Debug.Log($"[StageManager] Debug Mode: {debugStageData.stageName} 강제 로드");
            CurrentStageIndex = index;
            SetupStage(debugStageData);
            return;
        }

        if (stageList == null || !stageList.IsValidIndex(index))
        {
            Debug.LogWarning($"[StageManager] 유효하지 않은 스테이지 인덱스: {index}");
            return;
        }

        CurrentStageIndex = index;
        SetupStage(stageList.Get(index));
    }

    public void SetupStage(StageDataSO data)
    {
        CurrentStageData = data;

        ClearStage();
        SpawnStageObjects();
        SetupCamera();
        SpawnMonkeyArm();
        SpawnBanana();
        SpawnSuccessEffect();

        SetState(GameState.StageReady);

        Debug.Log($"[StageManager] 스테이지 셋업 완료: {data.stageName}");
    }

    private void SpawnStageObjects()
    {
        if (CurrentStageData.stageObjects == null) return;

        foreach (var objData in CurrentStageData.stageObjects)
        {
            if (objData.prefab == null) continue;

            var instance = Instantiate(objData.prefab, transform);
            instance.transform.position = new Vector3(objData.position.x, objData.position.y, 0f);
            instance.transform.rotation = Quaternion.Euler(0f, 0f, objData.rotation);
            float scaleX = objData.flipX ? -objData.scale.x : objData.scale.x;
            instance.transform.localScale = new Vector3(scaleX, objData.scale.y, 1f);
            _spawnedStageObjects.Add(instance);
        }
    }

    private void SetupCamera()
    {
        if (mainCamera == null) return;

        mainCamera.transform.position = new Vector3(
            CurrentStageData.cameraCenter.x,
            CurrentStageData.cameraCenter.y,
            mainCamera.transform.position.z);
        mainCamera.orthographicSize = CurrentStageData.cameraOrthographicSize;
    }

    private void SpawnMonkeyArm()
    {
        if (monkeyArmPrefab == null) return;

        if (_monkeyArmInstance != null)
            Destroy(_monkeyArmInstance);

        _monkeyArmInstance = Instantiate(monkeyArmPrefab);
        _monkeyArmInstance.transform.position = new Vector3(
            CurrentStageData.monkeyArmPosition.x, CurrentStageData.monkeyArmPosition.y, 0f);
        _monkeyArm = _monkeyArmInstance.GetComponentInChildren<MonkeyArm>();
    }

    private void SpawnBanana()
    {
        if (bananaPrefab == null) return;

        if (_bananaInstance == null)
        {
            _bananaInstance = Instantiate(bananaPrefab);
            _throwController = _bananaInstance.GetComponent<BananaThrowController>();
            _settleDetector = _bananaInstance.GetComponent<BananaSettleDetector>();
        }

        ResetBanana();
    }

    public void ResetBanana()
    {
        if (_throwController != null)
            _throwController.ResetBanana(CurrentStageData.bananaSpawnPosition);

        if (_monkeyArm != null)
            _monkeyArm.ResetArm();

        if (_settleDetector != null)
        {
            _settleDetector.ResetState();
            _settleDetector.InitializeBounds(mainCamera);
        }

        if (_throwController != null)
            _throwController.StartSwing();

        SetState(GameState.Ready);
    }

    private void SpawnSuccessEffect()
    {
        if (_successEffectInstance != null)
        {
            Destroy(_successEffectInstance.gameObject);
            _successEffectInstance = null;
        }

        if (successEffectPrefab == null) return;

        _successEffectInstance = Instantiate(successEffectPrefab);
        _successEffectInstance.Initialize();
    }

    private void ClearStage()
    {
        if (_monkeyArmInstance != null)
        {
            Destroy(_monkeyArmInstance);
            _monkeyArmInstance = null;
            _monkeyArm = null;
        }

        foreach (var obj in _spawnedStageObjects)
        {
            if (obj != null) Destroy(obj);
        }
        _spawnedStageObjects.Clear();
    }

    public void SetState(GameState newState)
    {
        if (CurrentState == newState) return;

        var prev = CurrentState;
        CurrentState = newState;

        Debug.Log($"[StageManager] {prev} → {newState}");

        OnStateChanged?.Invoke(newState);

        if (newState == GameState.StageReady)
            ShowStageReadyAsync().Forget();

        if (newState == GameState.Success)
            PlaySuccessEffectAsync().Forget();
    }

    private async UniTaskVoid ShowStageReadyAsync()
    {
        var popup = await UIManager.Instance.ShowPopup<UIStageReady>();
        popup.Refresh(CurrentStageIndex, CurrentStageData.stageDescription);
    }

    private async UniTaskVoid PlaySuccessEffectAsync()
    {
        _effectCts?.Cancel();
        _effectCts?.Dispose();
        _effectCts = new CancellationTokenSource();

        if (_successEffectInstance != null)
            await _successEffectInstance.PlayEffect(_effectCts.Token);

        //var resultUI = UIManager.Instance.GetPanel<UIStageResult>();
        //await resultUI.CaptureScreen();

        //await UIManager.Instance.ShowPopup<UIStageResult>();
        //await UIManager.Instance.HidePopup();

        LoadNextStage();
    }

    private void OnDestroy()
    {
        _effectCts?.Cancel();
        _effectCts?.Dispose();
    }
}
