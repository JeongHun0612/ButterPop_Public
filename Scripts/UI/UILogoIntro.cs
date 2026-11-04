using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 고스트가 떨어져 통통 튄 뒤 워드마크와 로딩 점이 나타나는 로고 인트로.
// 로딩 중 프레임이 끊겨도 장면을 건너뛰지 않도록 한 프레임에 진행하는 시간을 제한한다.
public class UILogoIntro : MonoBehaviour
{
    [Header("고스트")]
    [SerializeField] private UIJellyMesh ghost;
    [Tooltip("착지 위치보다 위에 있는 높이(px). 622면 머리가 화면 위 끝에 닿는다")]
    [SerializeField] private AnimationCurve ghostHeight = SmoothCurve(
        0f, 622f, 0.067f, 454f, 0.1f, 370f, 0.133f, 279f, 0.167f, 187f, 0.2f, 103f,
        0.233f, 33f, 0.267f, -14f, 0.3f, -5f, 0.333f, -3f, 0.367f, 2f, 0.4f, 14f,
        0.433f, 33f, 0.467f, 56f, 0.5f, 77f, 0.533f, 91f, 0.567f, 97f, 0.6f, 87f,
        0.633f, 66f, 0.667f, 40f, 0.7f, 18f, 0.733f, 5f, 0.767f, 0f, 0.833f, 0f);
    [Tooltip("바닥 쪽 눌림. 양수는 납작하게, 음수는 길쭉하게")]
    [SerializeField] private AnimationCurve ghostSquash = SmoothCurve(
        0f, -0.06f, 0.067f, -0.07f, 0.167f, -0.08f, 0.233f, -0.09f, 0.267f, -0.02f, 0.3f, 0.19f,
        0.333f, 0.17f, 0.367f, 0.1f, 0.4f, 0.03f, 0.433f, -0.04f, 0.5f, -0.03f, 0.567f, 0f,
        0.633f, -0.03f, 0.667f, -0.04f, 0.7f, -0.02f, 0.733f, 0.02f, 0.767f, 0.045f,
        0.8f, 0.02f, 0.833f, -0.012f, 0.9f, 0.005f, 0.967f, 0f);
    [Tooltip("꼭대기가 바닥의 눌림을 늦게 따라오는 시간(초)")]
    [SerializeField] private float ghostTopLag = 0.04f;
    [Tooltip("처음 나타날 때 고스트가 서서히 보이는 시간(초). 0이면 바로 보인다")]
    [SerializeField] private float ghostFadeDuration = 0.1f;

    [Header("그림자")]
    [SerializeField] private Image shadow;
    [Tooltip("가로 배율")]
    [SerializeField] private AnimationCurve shadowWidth = SmoothCurve(
        0f, 0.3f, 0.067f, 0.48f, 0.1f, 0.57f, 0.133f, 0.72f, 0.167f, 0.79f, 0.2f, 0.84f,
        0.233f, 0.89f, 0.3f, 1.2f, 0.367f, 1.18f, 0.4f, 1.1f, 0.433f, 1.08f, 0.467f, 1.06f,
        0.733f, 1.06f, 0.8f, 1.05f, 0.9f, 1.03f, 0.967f, 1f);
    [Tooltip("고스트가 다가오는 동안 그림자가 진해지는 시간(초)")]
    [SerializeField] private float shadowFadeDuration = 0.13f;

    [Header("워드마크")]
    [SerializeField] private Image wordmark;
    [SerializeField] private float wordmarkDelay = 1f;
    [Tooltip("제자리보다 이만큼 아래에서 올라온다(px)")]
    [SerializeField] private float wordmarkRise = 40f;
    [SerializeField] private float wordmarkRiseDuration = 0.2f;
    [SerializeField] private float wordmarkFadeDuration = 0.3f;

    [Header("로딩 점")]
    [SerializeField] private CanvasGroup loadingDots;
    [SerializeField] private float loadingDotsDelay = 1f;
    [SerializeField] private float loadingDotsFadeDuration = 0.2f;

    [Header("효과음")]
    [Tooltip("연출 시간(초)에 맞춰 재생할 효과음. 앞 소리를 끊지 않고 겹쳐 재생한다")]
    [SerializeField] private SoundCue[] soundCues;

    [Tooltip("한 프레임에 진행하는 최대 시간(초). 이보다 긴 프레임에서는 연출이 잠시 느려진다")]
    [SerializeField] private float maxStep = 1f / 30f;

    private RectTransform _ghostRect;
    private Graphic _ghostGraphic;
    private RectTransform _wordmarkRect;
    private Vector2 _ghostRestPosition;
    private Vector2 _wordmarkRestPosition;
    private float _ghostAlpha;
    private float _shadowAlpha;
    private float _wordmarkAlpha;
    private bool _canPlay;
#if UNITY_EDITOR
    private Vector3 _previewShadowScale;
    private float _previewLoadingDotsAlpha;
#endif

    public float Duration => Mathf.Max(
        LastKeyTime(ghostHeight),
        LastKeyTime(ghostSquash) + ghostTopLag,
        ghostFadeDuration,
        LastKeyTime(shadowWidth),
        shadowFadeDuration,
        wordmarkDelay + Mathf.Max(wordmarkRiseDuration, wordmarkFadeDuration),
        loadingDotsDelay + loadingDotsFadeDuration);

    private void Awake() => _canPlay = TryCacheTargets();

    // 첫 장면은 호출 즉시 적용되므로, 표시 직전에 부르면 완성된 로고가 먼저 보이지 않는다.
    public async UniTask PlayAsync(CancellationToken token)
    {
        if (!_canPlay) return;

        float duration = Duration;
        float elapsed = 0f;
        Evaluate(elapsed);
        PlaySoundCues(float.NegativeInfinity, elapsed);
        while (elapsed < duration)
        {
            if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow()) return;

            // 게임 시간으로 진행해야 고정 프레임 녹화에서도 영상 시간과 맞는다.
            float previous = elapsed;
            elapsed = Mathf.Min(elapsed + Mathf.Min(Time.deltaTime, maxStep), duration);
            Evaluate(elapsed);
            PlaySoundCues(previous, elapsed);
        }
    }

#if UNITY_EDITOR
    // 에디터 미리보기. 시작할 때의 배치를 제자리로 삼고, 끝내면 그 배치로 되돌린다.
    public void BeginPreview()
    {
        _canPlay = TryCacheTargets();
        if (!_canPlay) return;

        _previewShadowScale = shadow.rectTransform.localScale;
        _previewLoadingDotsAlpha = loadingDots.alpha;
    }

    public void PreviewAt(float time)
    {
        if (_canPlay) Evaluate(time);
    }

    public void EndPreview()
    {
        if (!_canPlay) return;

        _ghostRect.anchoredPosition = _ghostRestPosition;
        ghost.SetSquash(0f, 0f);
        SetAlpha(_ghostGraphic, _ghostAlpha);
        shadow.rectTransform.localScale = _previewShadowScale;
        SetAlpha(shadow, _shadowAlpha);
        _wordmarkRect.anchoredPosition = _wordmarkRestPosition;
        SetAlpha(wordmark, _wordmarkAlpha);
        loadingDots.alpha = _previewLoadingDotsAlpha;
    }
#endif

    private bool TryCacheTargets()
    {
        if (ghost == null || shadow == null || wordmark == null || loadingDots == null)
        {
            Debug.LogError($"[UILogoIntro] 연출 대상(고스트·그림자·워드마크·로딩 점)이 연결되지 않아 연출 없이 표시합니다: {name}");
            return false;
        }

        _ghostRect = (RectTransform)ghost.transform;
        _ghostGraphic = ghost.GetComponent<Graphic>();
        _wordmarkRect = wordmark.rectTransform;
        _ghostRestPosition = _ghostRect.anchoredPosition;
        _wordmarkRestPosition = _wordmarkRect.anchoredPosition;
        _ghostAlpha = _ghostGraphic.color.a;
        _shadowAlpha = shadow.color.a;
        _wordmarkAlpha = wordmark.color.a;
        return true;
    }

    private void Evaluate(float time)
    {
        _ghostRect.anchoredPosition = _ghostRestPosition + Vector2.up * ghostHeight.Evaluate(time);
        ghost.SetSquash(ghostSquash.Evaluate(time), ghostSquash.Evaluate(time - ghostTopLag));
        SetAlpha(_ghostGraphic, _ghostAlpha * Progress(time, 0f, ghostFadeDuration));

        shadow.rectTransform.localScale = new Vector3(shadowWidth.Evaluate(time), 1f, 1f);
        SetAlpha(shadow, _shadowAlpha * Progress(time, 0f, shadowFadeDuration));

        float rise = DOVirtual.EasedValue(0f, 1f, Progress(time, wordmarkDelay, wordmarkRiseDuration), Ease.OutQuad);
        _wordmarkRect.anchoredPosition = _wordmarkRestPosition + Vector2.down * (wordmarkRise * (1f - rise));
        SetAlpha(wordmark, _wordmarkAlpha * Progress(time, wordmarkDelay, wordmarkFadeDuration));

        loadingDots.alpha = Progress(time, loadingDotsDelay, loadingDotsFadeDuration);
    }

    // (from, to] 구간에 든 효과음을 재생한다. 연출과 같은 진행 시간을 쓰므로 프레임이 끊겨도 장면과 맞는다.
    private void PlaySoundCues(float from, float to)
    {
        if (soundCues == null) return;

        foreach (var cue in soundCues)
            if (cue != null && cue.time > from && cue.time <= to) SoundManager.TryPlaySfx(cue.clip);
    }

    private static float Progress(float time, float delay, float duration) =>
        duration > 0f ? Mathf.Clamp01((time - delay) / duration) : (time >= delay ? 1f : 0f);

    private static float LastKeyTime(AnimationCurve curve) =>
        curve.length > 0 ? curve[curve.length - 1].time : 0f;

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        var color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    // (시간, 값) 쌍을 이웃 키를 잇는 기울기로 매끄럽게 연결한다.
    private static AnimationCurve SmoothCurve(params float[] timeValues)
    {
        var keys = new Keyframe[timeValues.Length / 2];
        for (int i = 0; i < keys.Length; i++)
            keys[i] = new Keyframe(timeValues[i * 2], timeValues[i * 2 + 1]);

        for (int i = 0; i < keys.Length; i++)
        {
            var prev = keys[Mathf.Max(i - 1, 0)];
            var next = keys[Mathf.Min(i + 1, keys.Length - 1)];
            float slope = (next.value - prev.value) / (next.time - prev.time);
            keys[i].inTangent = slope;
            keys[i].outTangent = slope;
        }

        return new AnimationCurve(keys);
    }
}

[Serializable]
public class SoundCue
{
    public AudioClip clip;
    [Tooltip("인트로 시작 기준 재생 시각(초)")]
    public float time;
}
