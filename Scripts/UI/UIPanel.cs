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

        SetInputGuard(true);
        await OnShowAnimation();
        await OnShow();
        SetInputGuard(false);
    }

    public async UniTask Hide(bool instant = false)
    {
        // 숨김 연출 중 다시 눌리면 Hide가 중복 실행되고 OnAfterHide도 두 번 돈다.
        SetInputGuard(true);

        if (!instant) await OnHideAnimation();
        await OnHide();
        gameObject.SetActive(false);
    }

    protected virtual UniTask OnShowAnimation() => UniTask.CompletedTask;
    protected virtual UniTask OnShow() => UniTask.CompletedTask;
    protected virtual UniTask OnHideAnimation() => UniTask.CompletedTask;
    protected virtual UniTask OnHide() => UniTask.CompletedTask;

    // 표시·숨김 연출 중에 들어온 클릭을 막는다.
    protected virtual void SetInputGuard(bool guarded) { }

    public virtual void OnBeforeShow() { }
    public virtual void OnAfterHide() { }
}
