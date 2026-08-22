using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class StageBuilder : MonoBehaviour
{
    [Header("프리팹")]
    [Tooltip("붙잡는 손 + 중첩 Squish를 담은 HandRig 프리팹. 루트 원점 = 매달림 점(손끝).")]
    [FormerlySerializedAs("squishPrefab")]
    [SerializeField] private GameObject squishRigPrefab;
    [SerializeField] private GameObject monkeyArmPrefab;

    [Header("참조")]
    [SerializeField] private Camera mainCamera;

    private readonly List<GameObject> _spawnedStageObjects = new();
    private readonly List<IStageContextReceiver> _contextReceivers = new();

    // 재사용 액터 인스턴스(스테이지마다 재생성하지 않는다).
    private Squish _squish;
    private MonkeyArm _monkeyArm;
    private bool _actorsCreated;

    // 최초 액터 생성 시 1회만 발화. 이벤트 구독 등록용.
    public event Action<StageContext> OnActorsCreated;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    // 스테이지 액터를 구성해 StageContext를 새로 채워 반환한다. 소유·보관은 호출자(StageManager)가 한다.
    public StageContext Build(StageDataSO data)
    {
        var context = new StageContext { Data = data };

        ClearStageObjects();
        SpawnStageObjects(data);
        SpawnMonkeyArm(data, context);
        SpawnSquish(data, context);
        SetupCamera(data);

        if (context.Squish != null)
            context.Squish.UpdateCameraBounds(mainCamera);

        InjectStageContext(context);

        if (!_actorsCreated)
        {
            _actorsCreated = true;
            OnActorsCreated?.Invoke(context);
        }

        return context;
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

            if (objData.coverSquish)
            {
                foreach (var sr in instance.GetComponentsInChildren<SpriteRenderer>(true))
                    sr.sortingOrder += StageDataSO.CoverSquishSortingOrder;
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

    private void SpawnMonkeyArm(StageDataSO data, StageContext context)
    {
        if (monkeyArmPrefab == null) return;

        if (_monkeyArm == null)
        {
            var instance = Instantiate(monkeyArmPrefab);
            _monkeyArm = instance.GetComponentInChildren<MonkeyArm>();
            if (_monkeyArm == null)
            {
                Debug.LogError("[StageBuilder] monkeyArmPrefab에 MonkeyArm 컴포넌트가 없습니다.", instance);
                Destroy(instance);
                return;
            }
        }

        context.MonkeyArm = _monkeyArm;
        _monkeyArm.ResetAt(data.monkeyArmPosition);
    }

    private void SpawnSquish(StageDataSO data, StageContext context)
    {
        if (squishRigPrefab == null) return;

        if (_squish == null)
        {
            var instance = Instantiate(squishRigPrefab);
            _squish = instance.GetComponentInChildren<Squish>();
            if (_squish == null)
            {
                Debug.LogError("[StageBuilder] squishRigPrefab에 Squish 컴포넌트가 없습니다.", instance);
                Destroy(instance);
                return;
            }
        }

        context.Squish = _squish;

        // 스폰 좌표만 캐싱한다. 실제 rig 배치(루트를 스폰 좌표로 이동)는 직후 호출되는 RespawnAt()이 담당.
        _squish.SetSpawnPosition(data.squishSpawnPosition);
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

    private void InjectStageContext(StageContext context)
    {
        foreach (var receiver in _contextReceivers)
            receiver?.ReceiveStageContext(context);
    }
}
