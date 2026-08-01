using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class UIStageReady : UIPopup
{
    [SerializeField] private GameObject background;
    [SerializeField] private CanvasGroup textGroup;
    [SerializeField] private TextMeshProUGUI stageIndexText;
    [SerializeField] private TextMeshProUGUI stageNameText;

    [SerializeField] private float textFadeDuration = 0.5f;

    public void Refresh(int stageIndex, string stageDescription)
    {
        stageIndexText.text = $"# {stageIndex + 1}";
        stageNameText.text = stageDescription;
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
        if (StageManager.Instance != null)
            StageManager.Instance.SetState(GameState.Ready);
    }
}
