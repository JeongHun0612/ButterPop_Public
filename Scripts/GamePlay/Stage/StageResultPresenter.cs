using Cysharp.Threading.Tasks;
using UnityEngine;

public class StageResultPresenter : MonoBehaviour
{
    [Header("성공 연출")]
    [SerializeField] private Camera captureCamera;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private int captureWidth = 720;
    [SerializeField] private int captureHeight = 900;
    [Tooltip("캡처 여백 (월드 단위)")]
    [SerializeField] private float capturePadding = 0.5f;

    private RenderTexture _captureRT;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        _captureRT = new RenderTexture(captureWidth, captureHeight, 16);

        if (captureCamera != null)
            captureCamera.enabled = false;
    }

    private void OnEnable()
    {
        if (!StageManager.TryGetInstance(out var stageManager)) return;

        stageManager.OnStageLoaded += HandleStageLoaded;
        stageManager.OnStageCleared += HandleStageCleared;
    }

    private void OnDisable()
    {
        if (!StageManager.TryGetInstance(out var stageManager)) return;

        stageManager.OnStageLoaded -= HandleStageLoaded;
        stageManager.OnStageCleared -= HandleStageCleared;
    }

    private void OnDestroy()
    {
        if (_captureRT != null)
            _captureRT.Release();
    }

    private void HandleStageLoaded() => ShowStageReadyAsync().Forget();

    private void HandleStageCleared(Bounds bounds)
    {
        ShowStageResultAsync(bounds).Forget();
    }

    private async UniTaskVoid ShowStageReadyAsync()
    {
        var uiManager = UIManager.Instance;
        var stageManager = StageManager.Instance;
        Debug.Assert(uiManager != null, "[StageResultPresenter] UIManager가 없습니다");
        Debug.Assert(stageManager != null, "[StageResultPresenter] StageManager가 없습니다");

        var popup = uiManager.GetPanel<UIStageReady>();
        popup.Refresh(stageManager.CurrentStageIndex, stageManager.CurrentStageData.stageDescription);
        await uiManager.ShowPopup<UIStageReady>();
    }

    private async UniTaskVoid ShowStageResultAsync(Bounds captureBounds)
    {
        var uiManager = UIManager.Instance;
        Debug.Assert(uiManager != null, "[StageResultPresenter] UIManager가 없습니다");

        if (captureCamera != null)
        {
            CaptureScene(captureBounds);

            var resultUI = uiManager.GetPanel<UIStageResult>();
            resultUI.SetCaptureTexture(_captureRT);
            await uiManager.HideAllPopups();
            await uiManager.ShowPopup<UIStageResult>();
        }
    }

    private void CaptureScene(Bounds bounds)
    {
        captureCamera.CopyFrom(mainCamera);
        captureCamera.enabled = false;
        captureCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));

        captureCamera.transform.position = new Vector3(
            bounds.center.x,
            bounds.center.y,
            mainCamera.transform.position.z);

        float captureAspect = (float)captureWidth / captureHeight;
        float halfHeight = bounds.extents.y;
        float halfWidth = bounds.extents.x;
        float sizeFromHeight = halfHeight;
        float sizeFromWidth = halfWidth / captureAspect;
        captureCamera.orthographicSize = Mathf.Max(sizeFromHeight, sizeFromWidth) + capturePadding;

        captureCamera.targetTexture = _captureRT;
        captureCamera.Render();
        captureCamera.targetTexture = null;
    }
}
