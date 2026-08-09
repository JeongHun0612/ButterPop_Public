using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UIInGameHUD : UIScene
{
    [Header("Buttons")]
    [SerializeField] private Button menuButton;
    [SerializeField] private Button skipButton;

    [Header("Buttons")]
    [SerializeField] private Button customButton;
    [SerializeField] private Button storeButton;
    [SerializeField] private Button stageSelectButton;

    [Header("스킵 버튼 연출")]
    [SerializeField] private float skipShowDuration = 0.35f;
    [SerializeField] private Ease skipShowEase = Ease.OutBack;

    private Tween _skipShowTween;

    public override void Initialize()
    {
        base.Initialize();

        if (menuButton != null) menuButton.OnClickWithSound(OnMenuButtonClicked);
        if (skipButton != null) skipButton.OnClickWithSound(OnSkipButtonClicked);

        if (customButton != null) customButton.OnClickWithSound(OnCustomButtonClicked);
        if (storeButton != null) storeButton.OnClickWithSound(OnStoreButtonClicked);
        if (stageSelectButton != null) stageSelectButton.OnClickWithSound(OnStageSelectButtonClicked);

        SetSkipButtonActive(false);
    }

    public override void OnBeforeShow()
    {
        if (!StageManager.TryGetInstance(out var stageManager)) return;

        stageManager.OnSkipAvailabilityChanged += SetSkipButtonActive;
        SetSkipButtonActive(stageManager.IsSkipAvailable);
    }

    public override void OnAfterHide()
    {
        _skipShowTween?.Kill();

        if (!StageManager.TryGetInstance(out var stageManager)) return;

        stageManager.OnSkipAvailabilityChanged -= SetSkipButtonActive;
    }

    private void SetSkipButtonActive(bool active)
    {
        if (skipButton == null) return;

        _skipShowTween?.Kill();

        var buttonTransform = skipButton.transform;

        if (!active)
        {
            buttonTransform.localScale = Vector3.one;
            skipButton.gameObject.SetActive(false);
            return;
        }

        // 활성화될 때 스케일 팝인으로 가볍게 등장시킨다.
        skipButton.gameObject.SetActive(true);
        buttonTransform.localScale = Vector3.zero;
        _skipShowTween = buttonTransform
            .DOScale(Vector3.one, skipShowDuration)
            .SetEase(skipShowEase);
    }

    private async UniTaskVoid SkipStageAsync()
    {
        if (!Services.TryGet<IAdsService>(out var adsService))
        {
            Debug.LogWarning("[UIInGameHUD] IAdsService가 없어 스킵을 진행할 수 없습니다");
            return;
        }

        bool rewarded = await adsService.ShowRewardedAsync();
        if (!rewarded)
        {
            Debug.Log("[UIInGameHUD] 광고 미시청으로 스킵 취소");
            return;
        }

        if (StageManager.TryGetInstance(out var stageManager))
            stageManager.SkipCurrentStage();
    }

    private void OnMenuButtonClicked()
    {
        UIManager.Instance.ShowPopup<UIMenu>().Forget();
    }

    private void OnSkipButtonClicked() => SkipStageAsync().Forget();

    private void OnCustomButtonClicked()
    {
        UIManager.Instance.ShowPopup<UICustom>().Forget();
    }

    private void OnStoreButtonClicked()
    {
        UIManager.Instance.ShowPopup<UIStore>().Forget();
    }

    private void OnStageSelectButtonClicked()
    {
        UIManager.Instance.ShowPopup<UIStageSelect>().Forget();
    }
}
