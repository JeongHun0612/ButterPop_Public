using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UIInitialLoading : UIPanel
{
    private const Ease DotEase = Ease.OutQuad;

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 0.8f;

    [Header("로고 인트로")]
    [SerializeField] private UILogoIntro logoIntro;
    [Tooltip("인트로가 끝난 뒤 최소 이 시간(초)만큼 로고를 더 보여 준다. 로딩이 그보다 늦게 끝나면 더 기다리지 않는다")]
    [SerializeField] private float holdAfterIntro = 0.7f;
    [Tooltip("페이드아웃이 시작될 때 재생할 효과음")]
    [SerializeField] private AudioClip exitSound;

    [Header("로딩 점")]
    [SerializeField] private Image[] loadingDots;
    [SerializeField] private float dotMinAlpha = 0.5f;
    [Tooltip("점 하나가 밝아지는(또는 어두워지는) 시간")]
    [SerializeField] private float dotBlinkDuration = 0.17f;
    [Tooltip("점마다 깜빡임이 시작되는 간격")]
    [SerializeField] private float dotStagger = 0.17f;
    [Tooltip("모든 점이 깜빡이고 쉬는 시간까지 포함한 한 주기 길이")]
    [SerializeField] private float dotCycleDuration = 1f;

    private Sequence _dotSequence;
    private UniTask _introTask;
    private float _introFinishedAt;
#if UNITY_EDITOR
    private float[] _previewDotAlphas;
#endif

    // 화면을 가리고 있는 동안 true. 페이드아웃이 시작되면 false가 된다.
    public bool IsCovering { get; private set; }

    private float DotBlinkSpan => (loadingDots.Length - 1) * dotStagger + dotBlinkDuration * 2f;
    private float DotCycleLength => Mathf.Max(dotCycleDuration, DotBlinkSpan);

#if UNITY_EDITOR
    public float PreviewDuration => logoIntro != null ? logoIntro.Duration : 0f;
#endif

    private void OnDestroy() => StopLoadingDots();

    public override void Initialize() { }

    protected override UniTask OnShowAnimation()
    {
        canvasGroup.alpha = 1f;
        IsCovering = true;
        _introTask = PlayIntroAsync();
        PlayLoadingDots();
        return UniTask.CompletedTask;
    }

    // 인트로가 끝난 시각부터 holdAfterIntro가 지나지 않았으면 남은 시간만 기다린 뒤 페이드아웃한다.
    protected override async UniTask OnHideAnimation()
    {
        await _introTask;
        float remaining = _introFinishedAt + holdAfterIntro - Time.time;
        if (remaining > 0f)
        {
            bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(remaining),
                cancellationToken: destroyCancellationToken).SuppressCancellationThrow();
            if (canceled) return;
        }

        IsCovering = false;
        SoundManager.TryPlaySfx(exitSound);
        await canvasGroup.DOFade(0f, fadeDuration).SetEase(Ease.InOutSine).ToUniTask();
    }

    protected override UniTask OnHide()
    {
        IsCovering = false;
        StopLoadingDots();
        return UniTask.CompletedTask;
    }

#if UNITY_EDITOR
    // 에디터 미리보기. 로고 인트로와 로딩 점을 런타임과 같은 시각의 모습으로 맞추고, 끝내면 시작 전 상태로 되돌린다.
    public void BeginPreview()
    {
        if (logoIntro != null) logoIntro.BeginPreview();

        _previewDotAlphas = new float[loadingDots?.Length ?? 0];
        for (int i = 0; i < _previewDotAlphas.Length; i++)
            _previewDotAlphas[i] = loadingDots[i] != null ? loadingDots[i].color.a : 0f;
    }

    public void PreviewAt(float time)
    {
        if (logoIntro != null) logoIntro.PreviewAt(time);

        for (int i = 0; i < _previewDotAlphas.Length; i++)
            if (loadingDots[i] != null) SetAlpha(loadingDots[i], DotAlphaAt(i, time));
    }

    public void EndPreview()
    {
        if (logoIntro != null) logoIntro.EndPreview();

        for (int i = 0; i < _previewDotAlphas.Length; i++)
            if (loadingDots[i] != null) SetAlpha(loadingDots[i], _previewDotAlphas[i]);
    }

    // PlayLoadingDots 시퀀스가 시작 후 time초에 만드는 점 알파.
    private float DotAlphaAt(int index, float time)
    {
        float local = Mathf.Repeat(time, DotCycleLength) - index * dotStagger;
        if (local < 0f || local >= dotBlinkDuration * 2f) return dotMinAlpha;

        float progress = local < dotBlinkDuration ? local / dotBlinkDuration : 2f - local / dotBlinkDuration;
        return DOVirtual.EasedValue(dotMinAlpha, 1f, progress, DotEase);
    }
#endif

    private async UniTask PlayIntroAsync()
    {
        if (logoIntro != null) await logoIntro.PlayAsync(destroyCancellationToken);
        else Debug.LogError("[UIInitialLoading] 로고 인트로가 연결되지 않아 인트로 없이 표시합니다");
        _introFinishedAt = Time.time;
    }

    // 점이 왼쪽부터 차례로 밝아졌다 어두워지고, 남은 주기 동안 쉬는 사이클을 무한 반복한다.
    private void PlayLoadingDots()
    {
        StopLoadingDots();
        if (loadingDots == null || loadingDots.Length == 0) return;

        _dotSequence = DOTween.Sequence();
        for (int i = 0; i < loadingDots.Length; i++)
        {
            var dot = loadingDots[i];
            if (dot == null) continue;

            SetAlpha(dot, dotMinAlpha);
            _dotSequence.Insert(i * dotStagger,
                dot.DOFade(1f, dotBlinkDuration).SetEase(DotEase).SetLoops(2, LoopType.Yoyo));
        }
        _dotSequence.AppendInterval(DotCycleLength - DotBlinkSpan);
        _dotSequence.SetLoops(-1);
    }

    private void StopLoadingDots()
    {
        _dotSequence?.Kill();
        _dotSequence = null;
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        var color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }
}
