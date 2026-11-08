using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UIInGameHUD : UIScene
{
    [Header("Buttons")]
    [SerializeField] private Button menuButton;
    [SerializeField] private Button skipButton;
    [Tooltip("전면 광고 직후 건너뛰기 자리에 대신 뜨는 전체 광고 제거 구매 버튼")]
    [SerializeField] private Button removeAdsButton;

    [Header("스킨 체험 (리워드)")]
    [Tooltip("실패 시도마다 후보 스킨이 로테이션되는 광고 체험 버튼")]
    [SerializeField] private Button skinTrialButton;
    [SerializeField] private Image skinTrialIcon;
    [SerializeField] private float skinTrialShowDuration = 0.35f;
    [SerializeField] private Ease skinTrialShowEase = Ease.OutBack;

    [Header("스킵 버튼 연출")]
    [SerializeField] private float skipShowDuration = 0.35f;
    [SerializeField] private Ease skipShowEase = Ease.OutBack;
    [Tooltip("스킵 버튼 리마인드 시 펀치 세기")]
    [SerializeField] private float skipReminderPunch = 0.15f;
    [SerializeField] private float skipReminderDuration = 0.3f;

    private Tween _skipShowTween;
    private Tween _skinTrialShowTween;
    private Tween _removeAdsShowTween;

    private SquishSkinSO _trialCandidate;
    private bool _trialAppliedThisStage;
    private bool _skipEligible;
    private bool _skinTrialEligible;
    private bool _rewardedInProgress;
    private bool _purchaseInProgress;
    private int _skipShownFailCount;   // 건너뛰기가 마지막으로 등장했을 때의 클리어 이후 실패 수. 리마인드 주기의 기준점

    // 메뉴 버튼 상태의 두 조건(표시 / 클릭 가능). 소유자가 갈리면 서로 덮어쓰므로 한 곳에서 합쳐 계산한다.
    private bool _menuOpen;
    private bool _settling;

    public override void Initialize()
    {
        base.Initialize();

        if (menuButton != null) menuButton.OnClickWithSound(OnMenuButtonClicked);
        if (skipButton != null) skipButton.OnClickWithSound(OnSkipButtonClicked);

        if (skinTrialButton != null) skinTrialButton.OnClickWithSound(OnSkinTrialButtonClicked);
        if (removeAdsButton != null) removeAdsButton.OnClickWithSound(OnRemoveAdsButtonClicked);

        SetSkipButtonActive(false);
        RefreshRemoveAdsButton();
        SetSkinTrialActive(false);
    }

    public override void OnBeforeShow()
    {
        if (Services.TryGet<IAdsService>(out var adsService))
            adsService.OnRewardedAvailabilityChanged += OnRewardedAvailabilityChanged;
        if (Services.TryGet<IPurchaseService>(out var purchaseService))
            purchaseService.OnPurchasesUpdated += RefreshSkipSlot;
        if (AdCoordinator.TryGetInstance(out var adCoordinator))
            adCoordinator.OnRemoveAdsPromptChanged += RefreshSkipSlot;

        RefreshRemoveAdsButton();

        if (!StageManager.TryGetInstance(out var stageManager)) return;

        stageManager.OnSkipAvailabilityChanged += SetSkipButtonActive;
        stageManager.OnTryCountChanged += RemindSkipButton;
        stageManager.OnTryCountChanged += UpdateSkinTrial;
        stageManager.OnSettlingChanged += SetSettling;

        SetSkipButtonActive(stageManager.IsSkipAvailable);
        UpdateSkinTrial(stageManager.CurrentTryCount);
        SetSettling(stageManager.IsSettling);
    }

    public override void OnAfterHide()
    {
        _skipShowTween?.Kill();
        _skinTrialShowTween?.Kill();
        _removeAdsShowTween?.Kill();

        if (Services.TryGet<IAdsService>(out var adsService))
            adsService.OnRewardedAvailabilityChanged -= OnRewardedAvailabilityChanged;
        if (Services.TryGet<IPurchaseService>(out var purchaseService))
            purchaseService.OnPurchasesUpdated -= RefreshSkipSlot;
        if (AdCoordinator.TryGetInstance(out var adCoordinator))
            adCoordinator.OnRemoveAdsPromptChanged -= RefreshSkipSlot;

        if (!StageManager.TryGetInstance(out var stageManager)) return;

        stageManager.OnSkipAvailabilityChanged -= SetSkipButtonActive;
        stageManager.OnTryCountChanged -= RemindSkipButton;
        stageManager.OnTryCountChanged -= UpdateSkinTrial;
        stageManager.OnSettlingChanged -= SetSettling;
    }

    // ---------------- 메뉴 버튼  ----------------------

    // 안착 판정이 진행 중이면 메뉴 버튼을 잠근다. 판정이 끝나기 직전에 팝업이 끼어들면 결과 연출과 겹친다.
    private void SetSettling(bool settling)
    {
        _settling = settling;
        RefreshMenuButton();
    }

    // 메뉴가 떠 있는 동안엔 버튼 자체를 감추고, 안착 판정 중에는 보이되 클릭만 막는다.
    private void RefreshMenuButton()
    {
        if (menuButton == null) return;

        menuButton.gameObject.SetActive(!_menuOpen);
        menuButton.interactable = !_settling;
    }

    // ---------------- 스킵  ------------------------
    private void SetSkipButtonActive(bool active)
    {
        _skipEligible = active;
        RefreshSkipButton();
    }

    private void RefreshSkipButton()
    {
        if (skipButton == null) return;

        // 같은 자리를 쓰는 광고 제거 버튼이 떠 있으면 그쪽에 양보한다.
        bool active = _skipEligible && IsRewardedReady() && !IsRemoveAdsPromptActive();
        _skipShowTween?.Kill();

        var buttonTransform = skipButton.transform;

        if (!active)
        {
            buttonTransform.localScale = Vector3.one;
            skipButton.gameObject.SetActive(false);
            return;
        }

        // 활성화될 때 스케일 팝인으로 가볍게 등장시킨다.
        if (StageManager.TryGetInstance(out var stageManager)) _skipShownFailCount = stageManager.FailsSinceClear;
        skipButton.gameObject.SetActive(true);
        buttonTransform.localScale = Vector3.zero;
        _skipShowTween = buttonTransform
            .DOScale(Vector3.one, skipShowDuration)
            .SetEase(skipShowEase);
    }

    // 건너뛰기가 등장한 뒤 정해진 횟수만큼 실패할 때마다 버튼을 가볍게 강조한다.
    // 등장 시점을 기준으로 세므로 광고 제거 버튼과 슬롯을 주고받아도 숨겨진 동안의 실패는 섞이지 않는다.
    private void RemindSkipButton(int tryCount)
    {
        if (tryCount <= 0 || skipButton == null || !skipButton.gameObject.activeSelf) return;
        if (!StageManager.TryGetInstance(out var stageManager)) return;
        if (!AdCoordinator.TryGetInstance(out var adCoordinator)) return;

        int failsSinceShown = stageManager.FailsSinceClear - _skipShownFailCount;
        if (failsSinceShown <= 0 || failsSinceShown % adCoordinator.SkipReminderInterval != 0) return;

        _skipShowTween?.Kill();
        skipButton.transform.localScale = Vector3.one;
        _skipShowTween = skipButton.transform.DOPunchScale(
            Vector3.one * skipReminderPunch,
            skipReminderDuration);
    }

    private async UniTaskVoid SkipStageAsync()
    {
        if (!Services.TryGet<IAdsService>(out var adsService))
        {
            Debug.LogWarning("[UIInGameHUD] IAdsService가 없어 스킵을 진행할 수 없습니다");
            return;
        }

        if (_rewardedInProgress || !adsService.IsRewardedReady) return;

        _rewardedInProgress = true;
        RefreshRewardedButtons();

        bool rewarded;
        try
        {
            rewarded = await adsService.ShowRewardedAsync();
        }
        finally
        {
            _rewardedInProgress = false;
            RefreshRewardedButtons();
        }
        if (!rewarded)
        {
            Debug.Log("[UIInGameHUD] 광고 미시청으로 스킵 취소");
            return;
        }

        if (AdCoordinator.TryGetInstance(out var adCoordinator))
            adCoordinator.MarkRewardWatched();

        if (StageManager.TryGetInstance(out var stageManager))
            stageManager.SkipCurrentStage();
    }

    // ---------------- 광고 제거  ----------------------

    // 건너뛰기와 같은 자리라 두 버튼을 함께 갱신한다.
    private void RefreshSkipSlot()
    {
        RefreshRemoveAdsButton();
        RefreshSkipButton();
    }

    private void RefreshRemoveAdsButton()
    {
        if (removeAdsButton == null) return;

        // 결제 창이 떠 있는 동안 상태가 바뀌어도 버튼이 사라지지 않게 유지하고 클릭만 막는다.
        bool active = _purchaseInProgress || IsRemoveAdsPromptActive();
        removeAdsButton.interactable = !_purchaseInProgress;

        var buttonTransform = removeAdsButton.transform;

        if (!active)
        {
            _removeAdsShowTween?.Kill();
            buttonTransform.localScale = Vector3.one;
            removeAdsButton.gameObject.SetActive(false);
            return;
        }

        if (removeAdsButton.gameObject.activeSelf) return;

        removeAdsButton.gameObject.SetActive(true);
        buttonTransform.localScale = Vector3.zero;
        _removeAdsShowTween = buttonTransform
            .DOScale(Vector3.one, skipShowDuration)
            .SetEase(skipShowEase);
    }

    // 결제 창이 떠 있는 동안 안착·낙하가 판정되지 않게 한다.
    private async UniTaskVoid PurchaseRemoveAdsAsync()
    {
        if (_purchaseInProgress) return;
        if (!Services.TryGet<IPurchaseService>(out var purchaseService))
        {
            Debug.LogWarning("[UIInGameHUD] IPurchaseService가 없어 광고 제거를 구매할 수 없습니다");
            return;
        }

        _purchaseInProgress = true;
        RefreshSkipSlot();

        bool paused = StageManager.TryGetInstance(out var stageManager);
        if (paused) stageManager.SetJudgingPaused(true);

        PurchaseState state;
        try
        {
            state = await purchaseService.PurchaseAsync(ProductIds.RemoveAllAds);
        }
        finally
        {
            if (paused) stageManager.SetJudgingPaused(false);

            _purchaseInProgress = false;
            RefreshSkipSlot();
        }

        if (state != PurchaseState.Success)
            Debug.Log($"[UIInGameHUD] 광고 제거 구매 미완료: {state}");
    }

    private bool IsRemoveAdsPromptActive() =>
        AdCoordinator.TryGetInstance(out var adCoordinator)
        && adCoordinator.IsRemoveAdsPromptActive;

    // ---------------- 스킨 체험  ----------------------

    // 실패 시도에 따라 후보 스킨을 로테이션하며 체험 버튼을 갱신한다.
    private void UpdateSkinTrial(int tryCount)
    {
        if (tryCount == 0) // 새 스테이지 진입
        {
            _trialAppliedThisStage = false;
            _trialCandidate = null;
        }

        if (_trialAppliedThisStage
            || !AdCoordinator.TryGetInstance(out var adCoordinator)
            || !adCoordinator.IsSkinTrialAvailable)
        {
            SetSkinTrialActive(false);
            return;
        }

        var candidate = adCoordinator.GetTrialSkin(tryCount);
        if (candidate == null)
        {
            SetSkinTrialActive(false);
            return;
        }

        // 후보가 실제로 바뀌었을 때만 아이콘을 교체한다.
        bool changed = candidate != _trialCandidate;
        _trialCandidate = candidate;

        if (changed && skinTrialIcon != null) skinTrialIcon.sprite = candidate.icon;
        SetSkinTrialActive(true, changed);
    }

    // 최초 노출 또는 후보 스킨 교체 시에만 버튼 전체를 다시 등장시킨다.
    private void SetSkinTrialActive(bool active, bool changed = false)
    {
        _skinTrialEligible = active;
        RefreshSkinTrialButton(changed);
    }

    private void RefreshSkinTrialButton(bool changed = false)
    {
        if (skinTrialButton == null) return;

        bool active = _skinTrialEligible && IsRewardedReady();
        var buttonTransform = skinTrialButton.transform;

        if (!active)
        {
            _skinTrialShowTween?.Kill();
            buttonTransform.localScale = Vector3.one;
            skinTrialButton.gameObject.SetActive(false);
            return;
        }

        bool wasActive = skinTrialButton.gameObject.activeSelf;
        skinTrialButton.gameObject.SetActive(true);

        if (!wasActive || changed)
        {
            _skinTrialShowTween?.Kill();
            buttonTransform.localScale = Vector3.zero;
            _skinTrialShowTween = buttonTransform
                .DOScale(Vector3.one, skinTrialShowDuration)
                .SetEase(skinTrialShowEase);
        }
    }

    private async UniTaskVoid SkinTrialAsync()
    {
        if (_trialCandidate == null) return;
        if (!Services.TryGet<IAdsService>(out var adsService)) return;
        if (_rewardedInProgress || !adsService.IsRewardedReady) return;

        _rewardedInProgress = true;
        RefreshRewardedButtons();

        bool rewarded;
        try
        {
            rewarded = await adsService.ShowRewardedAsync();
        }
        finally
        {
            _rewardedInProgress = false;
            RefreshRewardedButtons();
        }
        if (!rewarded)
        {
            Debug.Log("[UIInGameHUD] 광고 미시청으로 스킨 체험 취소");
            return;
        }

        // 이번 스테이지 한정으로 체험 스킨 적용(다음 Build에서 장착 스킨으로 원복).
        if (StageManager.TryGetInstance(out var stageManager) && stageManager.Squish != null)
            stageManager.Squish.ApplySkin(_trialCandidate);

        if (AdCoordinator.TryGetInstance(out var adCoordinator))
            adCoordinator.MarkRewardWatched();

        _trialAppliedThisStage = true;
        SetSkinTrialActive(false);
    }

    private void OnMenuButtonClicked() => ShowMenuAsync().Forget();

    // 메뉴가 떠 있는 동안 메뉴 버튼을 감추고 안착 판정을 멈춘다. 닫히면 둘 다 되돌린다.
    private async UniTaskVoid ShowMenuAsync()
    {
        var uiManager = UIManager.Instance;
        Debug.Assert(uiManager != null, "[UIInGameHUD] UIManager가 없습니다");

        // 다른 팝업이 떠 있는 동안엔 열지 않는다. 그 팝업이 스택에 깔린 채 남으면 화면이 잠긴다.
        if (uiManager.CurrentPopup != null) return;

        _menuOpen = true;
        RefreshMenuButton();

        // 메뉴가 떠 있는 동안엔 안착·낙하가 판정되지 않게 한다(뒤에서 클리어돼 결과 화면이 끼어드는 것을 막는다).
        bool paused = StageManager.TryGetInstance(out var stageManager);
        if (paused) stageManager.SetJudgingPaused(true);

        var menu = await uiManager.ShowPopup<UIMenu>();

        // 팝업이 닫힐 때까지(Hide로 비활성화될 때까지) 대기.
        if (menu != null)
            await UniTask.WaitUntil(() => !menu.gameObject.activeSelf);

        if (paused) stageManager.SetJudgingPaused(false);

        _menuOpen = false;
        RefreshMenuButton();
    }

    private void OnSkipButtonClicked() => SkipStageAsync().Forget();

    private void OnSkinTrialButtonClicked() => SkinTrialAsync().Forget();

    private void OnRemoveAdsButtonClicked() => PurchaseRemoveAdsAsync().Forget();

    private bool IsRewardedReady() =>
        !_rewardedInProgress
        && Services.TryGet<IAdsService>(out var adsService)
        && adsService.IsRewardedReady;

    private void RefreshRewardedButtons()
    {
        RefreshSkipButton();
        RefreshSkinTrialButton();
    }

    private void OnRewardedAvailabilityChanged(bool _) => RefreshRewardedButtons();
}
