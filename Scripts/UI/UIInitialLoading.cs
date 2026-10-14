using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class UIInitialLoading : UIPanel
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;

    public override void Initialize() { }

    protected override UniTask OnShowAnimation()
    {
        canvasGroup.alpha = 1f;
        return UniTask.CompletedTask;
    }

    protected override async UniTask OnHideAnimation()
    {
        await canvasGroup.DOFade(0f, fadeDuration).ToUniTask();
    }
}
