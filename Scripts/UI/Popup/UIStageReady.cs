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

    protected override async UniTask OnHideAnimation()
    {
        background.SetActive(false);
        await textGroup.DOFade(0f, textFadeDuration).ToUniTask();
    }

    public override void OnAfterHide()
    {
        var stageManager = StageManager.Instance;
        Debug.Assert(stageManager != null, "[UIStageReady] StageManager가 없습니다");
        stageManager.SetState(GameState.Ready);
    }
}
