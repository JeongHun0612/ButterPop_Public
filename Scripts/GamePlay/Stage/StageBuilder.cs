using System;
using System.Collections.Generic;
using UnityEngine;

public class StageBuilder : MonoBehaviour
{
    [Header("프리팹")]
    [SerializeField] private GameObject bananaPrefab;
    [SerializeField] private GameObject monkeyArmPrefab;

    [Header("참조")]
    [SerializeField] private Camera mainCamera;

    private readonly List<GameObject> _spawnedStageObjects = new();
    private readonly List<IStageContextReceiver> _contextReceivers = new();

    private GameObject _bananaInstance;
    private GameObject _monkeyArmInstance;

    private StageContext _context;
    private bool _actorsCreated;

    // 최초 액터 생성 시 1회만 발화. 이벤트 구독 등록용.
    public event Action<StageContext> OnActorsCreated;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    public StageContext Build(StageDataSO data)
    {
        ClearStageObjects();
        SpawnStageObjects(data);
        SpawnMonkeyArm(data);
        SpawnBanana(data);
        SetupCamera(data);

        _context.Data = data;

        if (_context.SettleDetector != null)
            _context.SettleDetector.UpdateCameraBounds(mainCamera);

        InjectStageContext();

        if (!_actorsCreated)
        {
            _actorsCreated = true;
            OnActorsCreated?.Invoke(_context);
        }

        return _context;
    }

    public void ResetBanana(StageContext ctx)
    {
        if (ctx?.Banana == null) return;

        ctx.Banana.transform.position = ctx.Data.bananaSpawnPosition;
        ctx.Banana.StartSwing();

        if (ctx.SettleDetector != null)
            ctx.SettleDetector.ResetState();
    }

    private void SpawnStageObjects(StageDataSO data)
    {
        if (data.stageObjects == null) return;

        foreach (var objData in data.stageObjects)
        {
            if (objData.prefab == null) continue;

            var instance = Instantiate(objData.prefab, transform);
            instance.transform.position = new Vector3(objData.position.x, objData.position.y, 0f);
            instance.transform.rotation = Quaternion.Euler(0f, 0f, objData.rotation);
            float scaleX = objData.flipX ? -objData.scale.x : objData.scale.x;
            instance.transform.localScale = new Vector3(scaleX, objData.scale.y, 1f);

            if (objData.coverBanana)
            {
                foreach (var sr in instance.GetComponentsInChildren<SpriteRenderer>(true))
                    sr.sortingOrder += StageDataSO.CoverBananaSortingOrder;
            }

            foreach (var receiver in instance.GetComponentsInChildren<IStageContextReceiver>(true))
                _contextReceivers.Add(receiver);

            _spawnedStageObjects.Add(instance);
        }
    }

    private void SetupCamera(StageDataSO data)
    {
        if (mainCamera == null) return;

        mainCamera.transform.position = new Vector3(
            data.cameraCenter.x,
            data.cameraCenter.y,
            mainCamera.transform.position.z);

        mainCamera.orthographicSize = StageDataSO.ResolveOrthographicSize(
            data.cameraOrthographicSize, mainCamera.aspect);

        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = data.backgroundColor;
    }

    private void SpawnMonkeyArm(StageDataSO data)
    {
        if (monkeyArmPrefab == null) return;

        if (_monkeyArmInstance == null)
        {
            _monkeyArmInstance = Instantiate(monkeyArmPrefab);
            EnsureContext();
            _context.MonkeyArm = _monkeyArmInstance.GetComponentInChildren<MonkeyArm>();
        }

        _monkeyArmInstance.transform.position = new Vector3(
            data.monkeyArmPosition.x, data.monkeyArmPosition.y, 0f);

        if (_context.MonkeyArm != null)
            _context.MonkeyArm.ResetArm();
    }

    private void SpawnBanana(StageDataSO data)
    {
        if (bananaPrefab == null) return;

        if (_bananaInstance == null)
        {
            _bananaInstance = Instantiate(bananaPrefab);
            EnsureContext();
            _context.Banana = _bananaInstance.GetComponentInChildren<BananaThrowController>();
            _context.SettleDetector = _bananaInstance.GetComponentInChildren<BananaSettleDetector>();
        }

        _bananaInstance.transform.position = new Vector3(
            data.bananaSpawnPosition.x, data.bananaSpawnPosition.y, 0f);
    }

    private void ClearStageObjects()
    {
        foreach (var obj in _spawnedStageObjects)
        {
            if (obj != null) Destroy(obj);
        }
        _spawnedStageObjects.Clear();
        _contextReceivers.Clear();
    }

    private void InjectStageContext()
    {
        if (_context == null) return;

        foreach (var receiver in _contextReceivers)
            receiver?.ReceiveStageContext(_context);
    }

    private void EnsureContext()
    {
        _context ??= new StageContext();
    }
}
