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
        if (StageManager.Instance == null) return;

        StageManager.Instance.OnStateChanged += HandleStateChanged;
        StageManager.Instance.OnStageCleared += HandleStageCleared;
    }

    private void OnDisable()
    {
        if (StageManager.Instance == null) return;

        StageManager.Instance.OnStateChanged -= HandleStateChanged;
        StageManager.Instance.OnStageCleared -= HandleStageCleared;
    }

    private void OnDestroy()
    {
        if (_captureRT != null)
            _captureRT.Release();
    }

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.StageReady)
            ShowStageReadyAsync().Forget();
    }

    private void HandleStageCleared(Bounds bounds)
    {
        ShowStageResultAsync(bounds).Forget();
    }

    private async UniTaskVoid ShowStageReadyAsync()
    {
        if (UIManager.Instance == null || StageManager.Instance == null) return;

        var popup = UIManager.Instance.GetPanel<UIStageReady>();
        popup.Refresh(StageManager.Instance.CurrentStageIndex, StageManager.Instance.CurrentStageData.stageDescription);
        await UIManager.Instance.ShowPopup<UIStageReady>();
    }

    private async UniTaskVoid ShowStageResultAsync(Bounds captureBounds)
    {
        if (UIManager.Instance == null) return;

        if (captureCamera != null)
        {
            CaptureScene(captureBounds);

            var resultUI = UIManager.Instance.GetPanel<UIStageResult>();
            resultUI.SetCaptureTexture(_captureRT);
            UIManager.Instance.HideAllPopups().Forget();
            await UIManager.Instance.ShowPopup<UIStageResult>();
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
