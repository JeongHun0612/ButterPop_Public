using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class UIPanel : MonoBehaviour
{
    private CanvasGroup _canvasGroup;

    public virtual void Initialize()
    {
        gameObject.SetActive(false);
    }

    public async UniTask Show()
    {
        gameObject.SetActive(true);

        SetInteractable(false);
        await OnShowAnimation();
        await OnShow();
        SetInteractable(true);
    }

    public async UniTask Hide(bool instant = false)
    {
        if (!instant) await OnHideAnimation();
        await OnHide();
        gameObject.SetActive(false);
    }

    protected virtual UniTask OnShowAnimation() => UniTask.CompletedTask;
    protected virtual UniTask OnShow() => UniTask.CompletedTask;
    protected virtual UniTask OnHideAnimation() => UniTask.CompletedTask;
    protected virtual UniTask OnHide() => UniTask.CompletedTask;

    public virtual void OnBeforeShow() { }
    public virtual void OnAfterHide() { }

    private void SetInteractable(bool value)
    {
        if (_canvasGroup == null && !TryGetComponent(out _canvasGroup))
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _canvasGroup.blocksRaycasts = value;
    }
}