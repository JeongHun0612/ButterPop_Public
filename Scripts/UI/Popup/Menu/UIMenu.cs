using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class UIMenu : UIPopup
{
    [Header("패널")]
    [SerializeField] private MenuStorePanel storePanel;
    [SerializeField] private MenuStageSelectPanel stagePanel;
    [SerializeField] private MenuCustomPanel customPanel;

    [Header("공통")]
    [SerializeField] private Button closeButton;

    public override void Initialize()
    {
        base.Initialize();

        if (storePanel != null) storePanel.Initialize();
        if (stagePanel != null) stagePanel.Initialize(() => UIManager.Instance.HidePopup(this));
        if (customPanel != null) customPanel.Initialize();

        if (closeButton != null) closeButton.OnClickWithSound(OnClickBack);
    }

    protected override UniTask OnShow()
    {
        if (storePanel != null) storePanel.Refresh();
        if (stagePanel != null) stagePanel.Refresh();
        if (customPanel != null) customPanel.Refresh();
        return UniTask.CompletedTask;
    }

    protected override UniTask OnHide()
    {
        return UniTask.CompletedTask;
    }

    public override void OnClickBack()
    {
        if (customPanel != null) customPanel.Commit();

        if (stagePanel != null)
            stagePanel.ConfirmCurrent();
        else
            base.OnClickBack();
    }
}
