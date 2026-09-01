using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class UIPanel : MonoBehaviour
{
    public virtual void Initialize()
    {
        gameObject.SetActive(false);
    }

    public async UniTask Show()
    {
        gameObject.SetActive(true);

        SetShowGuard(true);
        await OnShowAnimation();
        await OnShow();
        SetShowGuard(false);
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

    // 연출이 끝나기 전에 들어온 클릭을 막는다.
    protected virtual void SetShowGuard(bool guarded) { }

    public virtual void OnBeforeShow() { }
    public virtual void OnAfterHide() { }
}
