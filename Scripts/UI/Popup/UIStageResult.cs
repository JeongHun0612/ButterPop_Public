// using Coffee.UIExtensions; // TODO: UIParticle 패키지(git URL) 설치 후 주석 해제
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
    // [SerializeField] private UIParticle successParticlePrefab; // TODO: UIParticle 패키지 설치 후 주석 해제

    [Header("하단 버튼")]
    [SerializeField] private Button btnRetry;
    [SerializeField] private Button btnAdReward;
    [SerializeField] private Button btnNext;

    // private UIParticle _spawnedParticle; // TODO: UIParticle 패키지 설치 후 주석 해제

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

            filmPanel.gameObject.SetActive(true);
            // SpawnParticle(); // TODO: UIParticle 패키지 설치 후 주석 해제

            await shutterPanel.DOFade(0f, shutterFadeOutDuration).ToUniTask();
            shutterPanel.gameObject.SetActive(false);
        }
    }

    protected override async UniTask OnHideAnimation()
    {
        // DespawnParticle(); // TODO: UIParticle 패키지 설치 후 주석 해제
        filmPanel.gameObject.SetActive(false);
    }

    public void SetCaptureTexture(RenderTexture rt)
    {
        capturedImage.texture = rt;
    }

    // TODO: UIParticle 패키지(git URL) 설치 후 아래 블록 주석 해제
    // private void SpawnParticle()
    // {
    //     var prefab = ResolvePresentationPrefab();
    //     if (prefab == null) return;
    //
    //     DespawnParticle();
    //     var parent = particleAnchor != null ? particleAnchor : transform;
    //     _spawnedParticle = Instantiate(prefab, parent);
    //     _spawnedParticle.Play();
    // }
    //
    // private void DespawnParticle()
    // {
    //     if (_spawnedParticle == null) return;
    //     Destroy(_spawnedParticle.gameObject);
    //     _spawnedParticle = null;
    // }
    //
    // private UIParticle ResolvePresentationPrefab()
    // {
    //     // TODO: 스킨 연동 시 여기서 장착된 스킨의 프리팹을 반환
    //     return successParticlePrefab;
    // }

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
