using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UIInGameHUD : UIScene
{
    [Header("Buttons")]
    [SerializeField] private Button menuButton;
    [SerializeField] private Button skipButton;

    [Header("스킨 체험 (리워드)")]
    [Tooltip("실패 시도마다 후보 스킨이 로테이션되는 광고 체험 버튼")]
    [SerializeField] private Button skinTrialButton;
    [SerializeField] private Image skinTrialIcon;
    [SerializeField] private float skinTrialShowDuration = 0.35f;
    [SerializeField] private Ease skinTrialShowEase = Ease.OutBack;
    [Tooltip("후보 스킨 교체 시 아이콘 펀치 세기")]
    [SerializeField] private float skinTrialPunch = 0.2f;
    [SerializeField] private float skinTrialPunchDuration = 0.25f;

    [Header("스킵 버튼 연출")]
    [SerializeField] private float skipShowDuration = 0.35f;
    [SerializeField] private Ease skipShowEase = Ease.OutBack;

    private Tween _skipShowTween;
    private Tween _skinTrialShowTween;
    private Tween _skinTrialIconTween;

    private SquishSkinSO _trialCandidate;
    private bool _trialAppliedThisStage;

    // 메뉴 버튼 상태의 두 조건(표시 / 클릭 가능). 소유자가 갈리면 서로 덮어쓰므로 한 곳에서 합쳐 계산한다.
    private bool _menuOpen;
    private bool _settling;

    public override void Initialize()
    {
        base.Initialize();

        if (menuButton != null) menuButton.OnClickWithSound(OnMenuButtonClicked);
        if (skipButton != null) skipButton.OnClickWithSound(OnSkipButtonClicked);

        if (skinTrialButton != null) skinTrialButton.OnClickWithSound(OnSkinTrialButtonClicked);

        SetSkipButtonActive(false);
        SetSkinTrialActive(false);
    }

    public override void OnBeforeShow()
    {
        if (!StageManager.TryGetInstance(out var stageManager)) return;

        stageManager.OnSkipAvailabilityChanged += SetSkipButtonActive;
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
        _skinTrialIconTween?.Kill();

        if (!StageManager.TryGetInstance(out var stageManager)) return;

        stageManager.OnSkipAvailabilityChanged -= SetSkipButtonActive;
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

        if (AdCoordinator.TryGetInstance(out var adCoordinator))
            adCoordinator.MarkRewardWatched();

        if (StageManager.TryGetInstance(out var stageManager))
            stageManager.SkipCurrentStage();
    }

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

        // 후보가 실제로 바뀌었을 때만 아이콘 교체·연출한다(같은 후보 유지 구간에선 연출 생략).
        bool changed = candidate != _trialCandidate;
        _trialCandidate = candidate;

        if (changed && skinTrialIcon != null) skinTrialIcon.sprite = candidate.icon;
        SetSkinTrialActive(true, changed);
    }

    // active: 버튼 노출 여부. changed: 이미 표시 중인 상태에서 후보가 바뀌었는지(펀치 연출용).
    private void SetSkinTrialActive(bool active, bool changed = false)
    {
        if (skinTrialButton == null) return;

        _skinTrialShowTween?.Kill();

        var buttonTransform = skinTrialButton.transform;

        if (!active)
        {
            buttonTransform.localScale = Vector3.one;
            skinTrialButton.gameObject.SetActive(false);
            return;
        }

        bool wasActive = skinTrialButton.gameObject.activeSelf;
        skinTrialButton.gameObject.SetActive(true);

        if (!wasActive)
        {
            // 새로 등장: 스케일 팝인
            buttonTransform.localScale = Vector3.zero;
            _skinTrialShowTween = buttonTransform
                .DOScale(Vector3.one, skinTrialShowDuration)
                .SetEase(skinTrialShowEase);
        }
        else if (changed)
        {
            PunchTrialIcon();
        }
    }

    private void PunchTrialIcon()
    {
        if (skinTrialIcon == null) return;

        _skinTrialIconTween?.Kill();
        skinTrialIcon.transform.localScale = Vector3.one;
        _skinTrialIconTween = skinTrialIcon.transform.DOPunchScale(Vector3.one * skinTrialPunch, skinTrialPunchDuration);
    }

    private async UniTaskVoid SkinTrialAsync()
    {
        if (_trialCandidate == null) return;
        if (!Services.TryGet<IAdsService>(out var adsService)) return;

        bool rewarded = await adsService.ShowRewardedAsync();
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
}
