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
    private int _uiLayer = -1;

    // 캡처는 연출이라 준비되지 않아도 결과 화면은 떠야 한다.
    private bool CanCapture => captureCamera != null && mainCamera != null && _captureRT != null;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        _captureRT = new RenderTexture(captureWidth, captureHeight, 16);

        // 레이어가 없으면 NameToLayer가 -1을 주고, 1 << -1은 1 << 31로 접혀 엉뚱한 레이어를 가린다.
        _uiLayer = LayerMask.NameToLayer("UI");
        if (_uiLayer < 0)
            Debug.LogWarning("[StageResultPresenter] UI 레이어가 없어 캡처에서 UI를 제외하지 못합니다");

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
        // Release는 GPU 리소스만 반환한다. 객체까지 정리해야 누수가 남지 않는다.
        if (_captureRT == null) return;

        _captureRT.Release();
        Destroy(_captureRT);
        _captureRT = null;
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
        if (popup == null)
        {
            Debug.LogError("[StageResultPresenter] UIStageReady 패널이 없어 준비 화면을 띄우지 못했습니다");
            return;
        }

        var currentSkin = stageManager.Squish != null ? stageManager.Squish.CurrentSkin : null;
        await popup.RefreshAsync(stageManager.CurrentStageIndex, stageManager.CurrentStageData, currentSkin);
        await uiManager.ShowPopup<UIStageReady>();
    }

    private async UniTaskVoid ShowStageResultAsync(Bounds captureBounds)
    {
        var uiManager = UIManager.Instance;
        var stageManager = StageManager.Instance;
        Debug.Assert(uiManager != null, "[StageResultPresenter] UIManager가 없습니다");
        Debug.Assert(stageManager != null, "[StageResultPresenter] StageManager가 없습니다");

        var resultUI = uiManager.GetPanel<UIStageResult>();
        if (resultUI == null)
        {
            Debug.LogError("[StageResultPresenter] UIStageResult 패널이 없어 결과 화면을 띄우지 못했습니다");
            return;
        }

        // 스테이지 고유 클리어 연출이 끝난 장면을 찍는다. 그 사이 스테이지가 교체되면 결과 화면을 띄우지 않는다.
        bool canceled = await stageManager.PlayClearEffectsAsync().SuppressCancellationThrow();
        if (canceled) return;

        // 캡처 실패가 결과 화면까지 막지 않도록 분리한다. 막히면 클리어 후 진행이 끊긴다.
        if (CanCapture)
        {
            CaptureScene(captureBounds);
            resultUI.SetCaptureTexture(_captureRT);
        }
        else
        {
            resultUI.SetCaptureTexture(null);
            Debug.LogWarning("[StageResultPresenter] 카메라가 없어 캡처 없이 결과 화면을 표시합니다");
        }

        await uiManager.HideAllPopups();
        await uiManager.ShowPopup<UIStageResult>();
    }

    private void CaptureScene(Bounds bounds)
    {
        captureCamera.CopyFrom(mainCamera);
        captureCamera.enabled = false;

        if (_uiLayer >= 0)
            captureCamera.cullingMask &= ~(1 << _uiLayer);

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
