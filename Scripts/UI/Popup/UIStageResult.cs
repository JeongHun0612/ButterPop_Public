using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UIStageResult : UIPopup
{
    [Header("필름 연출")]
    [SerializeField] private RectTransform filmPanel;       // 필름 전체 패널 (테두리 + RawImage 포함)
    [SerializeField] private RawImage capturedImage;        // 캡처된 화면을 표시할 RawImage
    [SerializeField] private RectTransform filmBorder;      // 필름 테두리 이미지

    [Header("설정")]
    [SerializeField] private float slideInDuration = 0.5f;
    [SerializeField] private float holdDuration = 0.8f;
    [SerializeField] private float slideOutDuration = 0.5f;
    [SerializeField] private Ease slideEase = Ease.InOutQuart;

    private Texture2D _capturedTexture;
    private float _screenHeight;

    public override void Initialize()
    {
        base.Initialize();
        _screenHeight = ((RectTransform)transform.root).rect.height;
    }

    // StageManager에서 직접 호출
    public async UniTask CaptureScreen()
    {
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

        if (_capturedTexture != null)
            Destroy(_capturedTexture);

        _capturedTexture = ScreenCapture.CaptureScreenshotAsTexture();
        capturedImage.texture = _capturedTexture;
    }

    protected override async UniTask OnShowAnimation()
    {
        // 화면 아래에서 올라오는 필름
        filmPanel.anchoredPosition = new Vector2(0f, -_screenHeight);
        await filmPanel.DOAnchorPosY(0f, slideInDuration).SetEase(slideEase).ToUniTask();
    }

    protected override async UniTask OnHideAnimation()
    {
        await UniTask.Delay((int)(holdDuration * 1000));
        // 화면 위로 넘어가는 필름
        await filmPanel.DOAnchorPosY(_screenHeight, slideOutDuration).SetEase(slideEase).ToUniTask();
    }

    private void OnDestroy()
    {
        if (_capturedTexture != null)
            Destroy(_capturedTexture);
    }
}
