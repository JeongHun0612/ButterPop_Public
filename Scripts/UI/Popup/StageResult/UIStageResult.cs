using Coffee.UIExtensions;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class UIStageResult : UIPopup
{
    [Header("셔터 연출")]
    [SerializeField] private CanvasGroup shutterPanel;
    [SerializeField] private float shutterFadeInDuration = 0.15f;
    [SerializeField] private float shutterHoldDuration = 0.2f;
    [SerializeField] private float shutterFadeOutDuration = 0.3f;

    [Header("결과 화면")]
    [FormerlySerializedAs("filmPanel")]
    [SerializeField] private RectTransform resultLayout;
    [SerializeField] private RawImage capturedImage;

    [Header("파티클")]
    [SerializeField] private RectTransform particleAnchor;
    [SerializeField] private UIParticle successParticlePrefab;

    [Header("하트 연출")]
    [SerializeField] private UIHeartLoopEffect heartLoopEffect;

    [Header("광고")]
    [Tooltip("클리어 연출 종료 후 전면 광고 노출까지의 대기 시간(초)")]
    [SerializeField] private float adPresentDelay = 0.6f;

    [Header("하단 버튼")]
    [SerializeField] private Button btnRetry;
    [SerializeField] private Button btnNext;

    private UIParticle _spawnedParticle;

    public override void Initialize()
    {
        base.Initialize();

        btnRetry.OnClickWithSound(OnClickRetry);
        btnNext.OnClickWithSound(OnClickNext);
    }

    public override void OnBeforeShow()
    {
        var stageManager = StageManager.Instance;
        Debug.Assert(stageManager != null, "[UIStageResult] StageManager가 없습니다");
        btnNext.gameObject.SetActive(stageManager.HasNextStage);
    }

    protected override async UniTask OnShowAnimation()
    {
        if (heartLoopEffect != null) heartLoopEffect.Stop();
        resultLayout.gameObject.SetActive(false);

        // 셔터 연출 (화면 플래시)
        if (shutterPanel != null)
        {
            shutterPanel.gameObject.SetActive(true);
            shutterPanel.alpha = 0f;
            await shutterPanel.DOFade(1f, shutterFadeInDuration).ToUniTask();
            await UniTask.Delay((int)(shutterHoldDuration * 1000));

            resultLayout.gameObject.SetActive(true);
            SpawnParticle();

            await shutterPanel.DOFade(0f, shutterFadeOutDuration).ToUniTask();
            shutterPanel.gameObject.SetActive(false);
        }
        else
        {
            resultLayout.gameObject.SetActive(true);
            SpawnParticle();
        }

        if (heartLoopEffect != null) heartLoopEffect.Play();

        // 클리어 연출 종료 후 전면 광고 자동 노출 시도. 실제로 광고가 나갈 때만 잠깐의 딜레이를 준다.
        if (AdCoordinator.TryGetInstance(out var adCoordinator))
        {
            if (adPresentDelay > 0f && adCoordinator.ShouldShowClearInterstitial)
                await UniTask.Delay((int)(adPresentDelay * 1000));

            adCoordinator.NotifyStageClearPresented();
        }
    }

    public override void OnAfterHide()
    {
        if (heartLoopEffect != null) heartLoopEffect.Stop();
        DespawnParticle();
        resultLayout.gameObject.SetActive(false);
    }

    // 캡처가 없으면 RawImage를 꺼둔다. 켜둔 채 텍스처만 비우면 흰 사각형이 그려진다.
    public void SetCaptureTexture(RenderTexture rt)
    {
        capturedImage.texture = rt;
        capturedImage.enabled = rt != null;
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
        UIManager.Instance.HidePopup(this).Forget();
        StageManager.Instance.LoadStage(StageManager.Instance.CurrentStageIndex);
    }

    private void OnClickNext()
    {
        UIManager.Instance.HidePopup(this).Forget();
        StageManager.Instance.LoadNextStage();
    }
}
