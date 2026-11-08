using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;

public class UIStageReady : UIPopup
{
    [SerializeField] private GameObject background;
    [SerializeField] private CanvasGroup textGroup;
    [SerializeField] private TextMeshProUGUI stageIndexText;
    [SerializeField] private TextMeshProUGUI stageNameText;
    [SerializeField] private LocalizedString stageDescription = new();

    [Header("등장·퇴장 연출")]
    [SerializeField] private RectTransform topLine;
    [SerializeField] private RectTransform bottomLine;
    [SerializeField] private float backgroundFadeInDuration = 0.4f;
    [SerializeField] private float backgroundFadeOutDuration = 0.6f;
    [SerializeField] private float lineSlideDuration = 0.56f;
    [SerializeField] private float textSlideDuration = 0.5f;
    [Tooltip("텍스트가 왼쪽에서 밀려 들어오는 거리")]
    [SerializeField] private float textEnterOffset = 120f;
    [Tooltip("텍스트가 오른쪽으로 빠져나가는 거리")]
    [SerializeField] private float textExitOffset = 180f;
    [SerializeField] private float exitDuration = 0.48f;
    [Tooltip("요소마다 등장이 시작되는 간격")]
    [SerializeField] private float stagger = 0.16f;

    [Header("자동 시작")]
    [Tooltip("터치 없이 이 시간(초)이 지나면 게임을 시작한다. 0 이하면 자동 시작하지 않는다")]
    [SerializeField] private float autoStartDelay = 3f;

    private CancellationTokenSource _autoStartCts;
    private Image _backgroundImage;
    private Image _topLineImage;
    private Image _bottomLineImage;
    private float _backgroundAlpha;
    private bool _canAnimate;
    private Sequence _motion;
    private Vector2 _topLineBase;
    private Vector2 _bottomLineBase;
    private Vector2 _indexTextBase;
    private Vector2 _nameTextBase;

    private float ScreenWidth => ((RectTransform)transform).rect.width;

    private void OnDisable()
    {
        CancelAutoStart();
        KillMotion();
    }

    public override void Initialize()
    {
        base.Initialize();

        _backgroundImage = background != null ? background.GetComponent<Image>() : null;
        _topLineImage = topLine != null ? topLine.GetComponent<Image>() : null;
        _bottomLineImage = bottomLine != null ? bottomLine.GetComponent<Image>() : null;
        if (_backgroundImage == null || _topLineImage == null || _bottomLineImage == null)
        {
            Debug.LogError($"[UIStageReady] 연출 대상(배경 Image·위아래 라인)이 연결되지 않아 연출 없이 표시합니다: {name}");
            return;
        }

        _backgroundAlpha = _backgroundImage.color.a;
        _canAnimate = true;
    }

    public async UniTask RefreshAsync(int stageIndex, StageDataSO stageData, SkinDataSO skinData)
    {
        stageIndexText.text = $"# {stageIndex + 1}";

        if (stageData == null || skinData == null)
        {
            stageNameText.text = string.Empty;
            return;
        }

        (string stageObjectName, string skinName) = await UniTask.WhenAll(
            GetLocalizedNameAsync(stageData.LocalizedName, stageData.stageId),
            GetLocalizedNameAsync(skinData.LocalizedName, skinData.id));

        if (stageDescription == null || stageDescription.IsEmpty)
        {
            stageNameText.text = $"{stageObjectName}\n{skinName}";
            return;
        }

        var description = new LocalizedString(
            stageDescription.TableReference,
            stageDescription.TableEntryReference)
        {
            { "stageObjectName", new StringVariable { Value = stageObjectName } },
            { "skinName", new StringVariable { Value = skinName } },
            { "skinHasFinalConsonant", new BoolVariable { Value = HasFinalConsonant(skinName) } }
        };

        stageNameText.text = await description.GetLocalizedStringAsync();
    }

    // 한국어 조사(을/를) 선택용. 마지막 글자가 받침 있는 한글 음절이면 true.
    private static bool HasFinalConsonant(string word)
    {
        if (string.IsNullOrEmpty(word)) return false;

        char last = word[^1];
        if (last < '가' || last > '힣') return false;
        return (last - '가') % 28 != 0;
    }

    private static async UniTask<string> GetLocalizedNameAsync(LocalizedString localizedName, string fallback)
    {
        if (localizedName == null || localizedName.IsEmpty) return fallback;
        return await localizedName.GetLocalizedStringAsync();
    }

    // 위 라인은 왼쪽, 아래 라인은 오른쪽 화면 밖에서 들어오고 텍스트가 시차를 두고 뒤따른다.
    protected override async UniTask OnShowAnimation()
    {
        KillMotion();
        textGroup.alpha = 1f;
        background.SetActive(true);
        if (!_canAnimate) return;

        CacheLayoutPositions();
        float travel = ScreenWidth;

        SetAlpha(_backgroundImage, 0f);
        topLine.anchoredPosition = _topLineBase + Vector2.left * travel;
        bottomLine.anchoredPosition = _bottomLineBase + Vector2.right * travel;
        stageIndexText.rectTransform.anchoredPosition = _indexTextBase + Vector2.left * textEnterOffset;
        stageNameText.rectTransform.anchoredPosition = _nameTextBase + Vector2.left * textEnterOffset;
        stageIndexText.alpha = 0f;
        stageNameText.alpha = 0f;
        SetAlpha(_topLineImage, 0f);
        SetAlpha(_bottomLineImage, 0f);

        _motion = DOTween.Sequence()
            .Insert(0f, _backgroundImage.DOFade(_backgroundAlpha, backgroundFadeInDuration))
            .Insert(0f, topLine.DOAnchorPos(_topLineBase, lineSlideDuration).SetEase(Ease.OutExpo))
            .Insert(0f, _topLineImage.DOFade(1f, lineSlideDuration).SetEase(Ease.OutQuad))
            .Insert(stagger, bottomLine.DOAnchorPos(_bottomLineBase, lineSlideDuration).SetEase(Ease.OutExpo))
            .Insert(stagger, _bottomLineImage.DOFade(1f, lineSlideDuration).SetEase(Ease.OutQuad))
            .Insert(stagger * 3f, SlideText(stageIndexText, _indexTextBase, 1f, textSlideDuration, Ease.OutQuad))
            .Insert(stagger * 4f, SlideText(stageNameText, _nameTextBase, 1f, textSlideDuration, Ease.OutQuad));

        await _motion.ToUniTask();
    }

    protected override UniTask OnShow()
    {
        StartAutoStart();
        return UniTask.CompletedTask;
    }

    // 들어온 반대 방향으로 빠져나가며 배경이 걷힌다.
    protected override async UniTask OnHideAnimation()
    {
        CancelAutoStart();
        KillMotion();

        if (_canAnimate)
        {
            float travel = ScreenWidth;
            float offset = stagger * 0.5f;

            _motion = DOTween.Sequence()
                .Insert(0f, _backgroundImage.DOFade(0f, backgroundFadeOutDuration))
                .Insert(0f, topLine.DOAnchorPos(_topLineBase + Vector2.right * travel, exitDuration).SetEase(Ease.OutExpo))
                .Insert(offset, bottomLine.DOAnchorPos(_bottomLineBase + Vector2.left * travel, exitDuration).SetEase(Ease.OutExpo))
                .Insert(0f, SlideText(stageIndexText, _indexTextBase + Vector2.right * textExitOffset, 0f, exitDuration, Ease.OutExpo))
                .Insert(offset, SlideText(stageNameText, _nameTextBase + Vector2.right * textExitOffset, 0f, exitDuration, Ease.OutExpo));

            await _motion.ToUniTask();
        }

        background.SetActive(false);
    }

    public override void OnAfterHide()
    {
        var stageManager = StageManager.Instance;
        Debug.Assert(stageManager != null, "[UIStageReady] StageManager가 없습니다");
        stageManager.SetState(GameState.Ready);
    }

    // 레이아웃 그룹이 정한 자리를 연출의 기준 위치로 쓴다.
    // 텍스트 변경으로 밀린 재배치를 먼저 비워야 연출 첫 프레임에 레이아웃이 위치를 되돌리지 않고,
    // 지난 퇴장 연출로 화면 밖에 있던 자리도 다시 계산된다.
    private void CacheLayoutPositions()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)textGroup.transform);

        _topLineBase = topLine.anchoredPosition;
        _bottomLineBase = bottomLine.anchoredPosition;
        _indexTextBase = stageIndexText.rectTransform.anchoredPosition;
        _nameTextBase = stageNameText.rectTransform.anchoredPosition;
    }

    // 위치는 빠르게 자리를 잡고, 투명도는 alphaEase로 따로 변하게 한다.
    private static Sequence SlideText(TMP_Text text, Vector2 to, float alpha, float duration, Ease alphaEase) =>
        DOTween.Sequence()
            .Join(text.rectTransform.DOAnchorPos(to, duration).SetEase(Ease.OutExpo))
            .Join(DOTween.To(() => text.alpha, a => text.alpha = a, alpha, duration).SetEase(alphaEase));

    private static void SetAlpha(Image image, float alpha)
    {
        var color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private void KillMotion()
    {
        _motion?.Kill();
        _motion = null;
    }

    private void StartAutoStart()
    {
        CancelAutoStart();
        if (autoStartDelay <= 0f) return;

        _autoStartCts = new CancellationTokenSource();
        AutoStartAsync(_autoStartCts.Token).Forget();
    }

    private void CancelAutoStart()
    {
        if (_autoStartCts == null) return;

        _autoStartCts.Cancel();
        _autoStartCts.Dispose();
        _autoStartCts = null;
    }

    // 다른 팝업이 위에 떠 있는 동안에는 시간을 세지 않는다.
    private async UniTaskVoid AutoStartAsync(CancellationToken token)
    {
        var uiManager = UIManager.Instance;
        Debug.Assert(uiManager != null, "[UIStageReady] UIManager가 없습니다");

        float elapsed = 0f;
        while (elapsed < autoStartDelay)
        {
            if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow()) return;
            if (uiManager.CurrentPopup == this) elapsed += Time.deltaTime;
        }

        OnClickBack();
    }
}
