using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

public class UIStageReady : UIPopup
{
    [SerializeField] private GameObject background;
    [SerializeField] private CanvasGroup textGroup;
    [SerializeField] private TextMeshProUGUI stageIndexText;
    [SerializeField] private TextMeshProUGUI stageNameText;
    [SerializeField] private LocalizedString stageDescription = new();

    [SerializeField] private float textFadeDuration = 0.5f;

    [Header("자동 시작")]
    [Tooltip("터치 없이 이 시간(초)이 지나면 게임을 시작한다. 0 이하면 자동 시작하지 않는다")]
    [SerializeField] private float autoStartDelay = 3f;

    private CancellationTokenSource _autoStartCts;

    private void OnDisable() => CancelAutoStart();

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
            { "skinName", new StringVariable { Value = skinName } }
        };

        stageNameText.text = await description.GetLocalizedStringAsync();
    }

    private static async UniTask<string> GetLocalizedNameAsync(LocalizedString localizedName, string fallback)
    {
        if (localizedName == null || localizedName.IsEmpty) return fallback;
        return await localizedName.GetLocalizedStringAsync();
    }

    protected override async UniTask OnShowAnimation()
    {
        textGroup.alpha = 1f;
        background.SetActive(true);
        await UniTask.CompletedTask;
    }

    protected override UniTask OnShow()
    {
        StartAutoStart();
        return UniTask.CompletedTask;
    }

    protected override async UniTask OnHideAnimation()
    {
        CancelAutoStart();
        background.SetActive(false);
        await textGroup.DOFade(0f, textFadeDuration).ToUniTask();
    }

    public override void OnAfterHide()
    {
        var stageManager = StageManager.Instance;
        Debug.Assert(stageManager != null, "[UIStageReady] StageManager가 없습니다");
        stageManager.SetState(GameState.Ready);
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
