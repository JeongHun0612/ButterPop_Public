using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Serialization;

public class StageBuilder : MonoBehaviour
{
    [Header("프리팹")]
    [SerializeField] private GameObject squishRigPrefab;
    [SerializeField] private GameObject armPrefab;

    [Header("데이터")]
    [Tooltip("장착된 스퀴시 스킨을 조회하기 위한 스킨 데이터베이스.")]
    [SerializeField] private SkinDatabaseSO skinDatabase;

    [Header("참조")]
    [SerializeField] private Camera mainCamera;

    private readonly List<GameObject> _spawnedStageObjects = new();
    private readonly List<IStageContextReceiver> _contextReceivers = new();

    // 재사용 액터 인스턴스(스테이지마다 재생성하지 않는다).
    private Squish _squish;
    private ArmController _armController;
    private bool _actorsCreated;

    // 최초 액터 생성 시 1회만 발화. 이벤트 구독 등록용.
    public event Action<StageContext> OnActorsCreated;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    // 스테이지 액터를 구성해 StageContext를 새로 채워 반환한다. 소유·보관은 호출자(StageManager)가 한다.
    public StageContext Build(StageDataSO data, CancellationToken stageCancellationToken)
    {
        var context = new StageContext
        {
            Data = data,
            StageCancellationToken = stageCancellationToken
        };

        ClearStageObjects();
        SpawnStageObjects(data, context);
        SpawnArm(data, context);
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

    private void SpawnStageObjects(StageDataSO data, StageContext context)
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
                ApplyCoverSquish(instance);

            foreach (var receiver in instance.GetComponentsInChildren<IStageContextReceiver>(true))
                _contextReceivers.Add(receiver);

            context.ClearEffects.AddRange(instance.GetComponentsInChildren<IStageClearEffect>(true));

            _spawnedStageObjects.Add(instance);
        }
    }

    // 오브젝트를 스퀴시 위로 올린다. 프리팹 내부 상대 순서를 유지하려고 덮어쓰지 않고 오프셋만 더한다.
    // IgnoreCoverSquish가 붙은 하위(UFO 빔 등)는 스퀴시 뒤에 남겨야 하므로 제외한다.
    private static void ApplyCoverSquish(GameObject instance)
    {
        foreach (var sr in instance.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.GetComponentInParent<IgnoreCoverSquish>(true) != null) continue;
            sr.sortingOrder += StageDataSO.CoverSquishSortingOrder;
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

    private void SpawnArm(StageDataSO data, StageContext context)
    {
        if (armPrefab == null) return;

        if (_armController == null)
        {
            var instance = Instantiate(armPrefab);
            _armController = instance.GetComponentInChildren<ArmController>();
            if (_armController == null)
            {
                Debug.LogError("[StageBuilder] armPrefab에 ArmController 컴포넌트가 없습니다.", instance);
                Destroy(instance);
                return;
            }
        }

        context.ArmController = _armController;
        _armController.ResetAt(data.armPosition);
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

        // 장착된 스퀴시 스킨 반영
        ApplyEquippedSquishSkin(_squish);

        // 스폰 좌표만 캐싱한다. 실제 rig 배치(루트를 스폰 좌표로 이동)는 직후 호출되는 RespawnAt()이 담당.
        _squish.SetSpawnPosition(data.squishSpawnPosition);
    }

    private void ApplyEquippedSquishSkin(Squish squish)
    {
        var skin = ResolveEquippedSquishSkin();
        if (skin != null) squish.ApplySkin(skin);
    }

    // Squish 탭의 장착 스킨을 가져온다. 장착이 없으면 서비스가 기본 스킨으로 확정한다.
    // 서비스가 아직 없으면 저장 없이 기본 스킨만 적용한다.
    private SquishSkinSO ResolveEquippedSquishSkin()
    {
        if (skinDatabase == null) return null;

        var tab = skinDatabase.GetTab(SkinCategory.Squish);
        if (tab == null || tab.Count == 0) return null;

        if (!Services.TryGet(out ISkinService service)) return tab.defaultSkin as SquishSkinSO;

        return service.ResolveEquipped(tab) as SquishSkinSO;
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
