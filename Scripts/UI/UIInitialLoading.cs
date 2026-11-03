using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UIInitialLoading : UIPanel
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 0.8f;
    [Tooltip("로딩이 일찍 끝나도 로고가 깜빡이듯 사라지지 않도록 보장하는 최소 노출 시간(초)")]
    [SerializeField] private float minDisplayDuration = 1.2f;

    [Header("로딩 점")]
    [SerializeField] private Image[] loadingDots;
    [SerializeField] private float dotMinAlpha = 0.25f;
    [SerializeField] private float dotBlinkDuration = 0.4f;
    [Tooltip("점마다 깜빡임이 시작되는 간격")]
    [SerializeField] private float dotStagger = 0.2f;

    private Sequence _dotSequence;
    private float _shownAt;

    private void OnDestroy() => StopLoadingDots();

    public override void Initialize() { }

    protected override UniTask OnShowAnimation()
    {
        canvasGroup.alpha = 1f;
        _shownAt = Time.realtimeSinceStartup;
        PlayLoadingDots();
        return UniTask.CompletedTask;
    }

    // 표시 후 최소 노출 시간이 지나지 않았으면 남은 시간만큼 기다린 뒤 페이드아웃한다.
    protected override async UniTask OnHideAnimation()
    {
        float remaining = minDisplayDuration - (Time.realtimeSinceStartup - _shownAt);
        if (remaining > 0f)
        {
            bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(remaining), ignoreTimeScale: true,
                cancellationToken: destroyCancellationToken).SuppressCancellationThrow();
            if (canceled) return;
        }

        await canvasGroup.DOFade(0f, fadeDuration).SetEase(Ease.InOutSine).ToUniTask();
    }

    protected override UniTask OnHide()
    {
        StopLoadingDots();
        return UniTask.CompletedTask;
    }

    // 점이 왼쪽부터 차례로 밝아졌다 어두워지는 사이클을 무한 반복한다.
    private void PlayLoadingDots()
    {
        StopLoadingDots();
        if (loadingDots == null || loadingDots.Length == 0) return;

        _dotSequence = DOTween.Sequence();
        for (int i = 0; i < loadingDots.Length; i++)
        {
            var dot = loadingDots[i];
            if (dot == null) continue;

            var color = dot.color;
            color.a = dotMinAlpha;
            dot.color = color;

            _dotSequence.Insert(i * dotStagger, dot.DOFade(1f, dotBlinkDuration).SetLoops(2, LoopType.Yoyo));
        }
        _dotSequence.SetLoops(-1);
    }

    private void StopLoadingDots()
    {
        _dotSequence?.Kill();
        _dotSequence = null;
    }
}
