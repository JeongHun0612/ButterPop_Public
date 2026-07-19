using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UIStageResult : UIPopup
{
    [Header("셔터 연출")]
    [SerializeField] private CanvasGroup shutterPanel;
    [SerializeField] private float shutterFadeInDuration = 0.15f;
    [SerializeField] private float shutterHoldDuration = 0.2f;
    [SerializeField] private float shutterFadeOutDuration = 0.3f;

    [Header("필름 연출")]
    [SerializeField] private RectTransform filmPanel;
    [SerializeField] private RawImage capturedImage;

    [Header("파티클")]
    [SerializeField] private ParticleSystem successParticle;

    [Header("하단 버튼")]
    [SerializeField] private Button btnRetry;
    [SerializeField] private Button btnAdReward;
    [SerializeField] private Button btnNext;

    public override void Initialize()
    {
        base.Initialize();

        btnRetry.onClick.AddListener(OnClickRetry);
        btnAdReward.onClick.AddListener(OnClickAdReward);
        btnNext.onClick.AddListener(OnClickNext);
    }

    protected override async UniTask OnShowAnimation()
    {
        filmPanel.gameObject.SetActive(false);

        // 셔터 연출 (화면 플래시)
        if (shutterPanel != null)
        {
            shutterPanel.gameObject.SetActive(true);
            shutterPanel.alpha = 0f;
            await shutterPanel.DOFade(1f, shutterFadeInDuration).ToUniTask();
            await UniTask.Delay((int)(shutterHoldDuration * 1000));

            // 액자 + 파티클 동시 표시
            filmPanel.gameObject.SetActive(true);
            if (successParticle != null)
                successParticle.Play();

            await shutterPanel.DOFade(0f, shutterFadeOutDuration).ToUniTask();
            shutterPanel.gameObject.SetActive(false);
        }
    }

    protected override async UniTask OnHideAnimation()
    {
        if (successParticle != null)
            successParticle.Stop();

        filmPanel.gameObject.SetActive(false);
    }

    public void SetCaptureTexture(RenderTexture rt)
    {
        capturedImage.texture = rt;
    }

    private void OnClickRetry()
    {
        UIManager.Instance.HidePopup().Forget();
        StageManager.Instance.LoadStage(StageManager.Instance.CurrentStageIndex);
    }

    private void OnClickAdReward()
    {
        // TODO: 광고 재생 → 보상 지급
    }

    private void OnClickNext()
    {
        UIManager.Instance.HidePopup().Forget();
        StageManager.Instance.LoadNextStage();
    }
}
