using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    public GameState CurrentState { get; private set; } = GameState.Ready;
    public event Action<GameState> OnStateChanged;

    [Header("스테이지 데이터")]
    [SerializeField] private StageDataSO stageData;

    [Header("바나나")]
    [SerializeField] private GameObject bananaPrefab;

    [Header("원숭이 팔")]
    [SerializeField] private GameObject monkeyArmPrefab;

    [Header("성공 연출")]
    [SerializeField] private SuccessEffect successEffectPrefab;

    [Header("참조")]
    [SerializeField] private Camera mainCamera;

    [SerializeField] private bool debugLog = true;

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
    public Vector2 BananaSpawnPosition => stageData != null ? stageData.bananaSpawnPosition : Vector2.zero;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void Start()
    {
        if (stageData != null)
            SetupStage(stageData);
    }

    public void SetupStage(StageDataSO data)
    {
        stageData = data;

        ClearStage();
        SpawnStageObjects();
        SetupCamera();
        SpawnMonkeyArm();
        SpawnBanana();
        SpawnSuccessEffect();

        SetState(GameState.Ready);

        if (debugLog)
            Debug.Log($"[StageManager] 스테이지 셋업 완료: {data.stageName} (ID: {data.stageId})");
    }

    private void SpawnStageObjects()
    {
        if (stageData.stageObjects == null) return;

        foreach (var objData in stageData.stageObjects)
        {
            if (objData.prefab == null) continue;

            var instance = Instantiate(objData.prefab, transform);
            instance.transform.position = new Vector3(objData.position.x, objData.position.y, 0f);
            instance.transform.rotation = Quaternion.Euler(0f, 0f, objData.rotation);
            instance.transform.localScale = new Vector3(objData.scale.x, objData.scale.y, 1f);
            _spawnedStageObjects.Add(instance);
        }
    }

    private void SetupCamera()
    {
        if (mainCamera == null) return;

        mainCamera.transform.position = new Vector3(
            stageData.cameraCenter.x,
            stageData.cameraCenter.y,
            mainCamera.transform.position.z);
        mainCamera.orthographicSize = stageData.cameraOrthographicSize;
    }

    private void SpawnMonkeyArm()
    {
        if (monkeyArmPrefab == null) return;

        if (_monkeyArmInstance != null)
            Destroy(_monkeyArmInstance);

        _monkeyArmInstance = Instantiate(monkeyArmPrefab);
        _monkeyArmInstance.transform.position = new Vector3(
            stageData.monkeyArmPosition.x, stageData.monkeyArmPosition.y, 0f);
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
            _throwController.ResetBanana(stageData.bananaSpawnPosition);

        if (_monkeyArm != null)
            _monkeyArm.ResetArm();

        if (_settleDetector != null)
        {
            _settleDetector.ResetState();
            _settleDetector.SetSpawnPosition(stageData.bananaSpawnPosition);
            _settleDetector.SetFallYThreshold(stageData.fallYThreshold);
        }

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

        if (debugLog)
            Debug.Log($"[StageManager] {prev} → {newState}");

        OnStateChanged?.Invoke(newState);

        if (newState == GameState.Success)
            PlaySuccessEffectAsync().Forget();
    }

    private async UniTaskVoid PlaySuccessEffectAsync()
    {
        _effectCts?.Cancel();
        _effectCts?.Dispose();
        _effectCts = new CancellationTokenSource();

        if (_successEffectInstance != null)
            await _successEffectInstance.PlayEffect(_effectCts.Token);

        // TODO: 이어지는 연출 추가
        // TODO: 다음 스테이지로 전환
    }

    private void OnDestroy()
    {
        _effectCts?.Cancel();
        _effectCts?.Dispose();
    }
}
