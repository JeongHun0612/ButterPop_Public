using Coffee.UIExtensions;
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
    [SerializeField] private RectTransform particleAnchor;
    [SerializeField] private UIParticle successParticlePrefab;

    [Header("하단 버튼")]
    [SerializeField] private Button btnRetry;
    [SerializeField] private Button btnAdReward;
    [SerializeField] private Button btnNext;

    private UIParticle _spawnedParticle;

    public override void Initialize()
    {
        base.Initialize();

        btnRetry.onClick.AddListener(OnClickRetry);
        btnAdReward.onClick.AddListener(OnClickAdReward);
        btnNext.onClick.AddListener(OnClickNext);
    }

    public override void OnBeforeShow()
    {
        // 마지막 스테이지에서는 다음 스테이지가 없으므로 다음 버튼을 비활성화
        btnNext.gameObject.SetActive(StageManager.Instance.HasNextStage);
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

            filmPanel.gameObject.SetActive(true);
            SpawnParticle();

            await shutterPanel.DOFade(0f, shutterFadeOutDuration).ToUniTask();
            shutterPanel.gameObject.SetActive(false);
        }
    }

    public override void OnAfterHide()
    {
        DespawnParticle();
        filmPanel.gameObject.SetActive(false);
    }

    public void SetCaptureTexture(RenderTexture rt)
    {
        capturedImage.texture = rt;
    }

    private void SpawnParticle()
    {
        var prefab = ResolvePresentationPrefab();
        if (prefab == null) return;

        DespawnParticle();
        var parent = particleAnchor != null ? particleAnchor : transform;
        _spawnedParticle = Instantiate(prefab, parent);
        _spawnedParticle.Play();
    }

    private void DespawnParticle()
    {
        if (_spawnedParticle == null) return;
        Destroy(_spawnedParticle.gameObject);
        _spawnedParticle = null;
    }

    private UIParticle ResolvePresentationPrefab()
    {
        // TODO: 스킨 연동 시 여기서 장착된 스킨의 프리팹을 반환
        return successParticlePrefab;
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
