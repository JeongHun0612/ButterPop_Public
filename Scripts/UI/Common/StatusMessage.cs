using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 짧은 안내 문구를 잠시 띄웠다 지운다. 오류는 색과 흔들림으로 구분한다.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class StatusMessage : MonoBehaviour
{
    [Header("표시")]
    [SerializeField] private float duration = 3f;           // 문구를 유지하는 시간(초)
    [SerializeField] private float fadeDuration = 0.4f;     // 유지 후 사라지는 시간(초)
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color errorColor = Color.red;

    [Header("흔들림")]
    [SerializeField] private float shakeDuration = 0.4f;
    [SerializeField] private float shakeStrength = 12f;
    [SerializeField] private int shakeVibrato = 20;

    private TMP_Text _text;
    private RectTransform _rect;
    private Vector2 _shakeOrigin;
    private bool _shaking;
    private Tween _shakeTween;
    private Tween _fadeTween;
    private CancellationTokenSource _hideCts;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        _rect = GetComponent<RectTransform>();
        _text.text = string.Empty;
    }

    private void OnDestroy() => CancelPending();

    public void Show(string message, bool isError = false)
    {
        CancelPending();

        _text.text = message;
        _text.color = isError ? errorColor : normalColor;

        if (isError) Shake();

        _hideCts = new CancellationTokenSource();
        HideAfterDelayAsync(_hideCts.Token).Forget();
    }

    public void Clear()
    {
        CancelPending();
        _text.text = string.Empty;
    }

    // LayoutGroup 안에서도 어긋나지 않도록 원위치를 흔들기 직전에 잡는다.
    private void Shake()
    {
        _shakeOrigin = _rect.anchoredPosition;
        _shaking = true;

        _shakeTween = _rect
            .DOShakeAnchorPos(shakeDuration, shakeStrength, shakeVibrato)
            .SetLink(gameObject)
            .OnComplete(() => _shaking = false);
    }

    private async UniTaskVoid HideAfterDelayAsync(CancellationToken token)
    {
        bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: token)
            .SuppressCancellationThrow();
        if (canceled) return;

        _fadeTween = DOTween.To(() => _text.alpha, a => _text.alpha = a, 0f, fadeDuration)
            .SetLink(gameObject)
            .OnComplete(() => _text.text = string.Empty);
    }

    // 흔들리는 도중 교체·파괴되면 위치가 틀어진 채 남으므로 원위치로 되돌린다.
    // 페이드 중 잘려 알파가 낮아진 것은 다음 Show의 색 대입으로 복구된다.
    private void CancelPending()
    {
        _hideCts?.Cancel();
        _hideCts?.Dispose();
        _hideCts = null;

        _shakeTween?.Kill();
        _shakeTween = null;

        _fadeTween?.Kill();
        _fadeTween = null;

        // 흔드는 중이었을 때만 되돌린다. 아니면 레이아웃이 잡아둔 위치를 망친다.
        if (_shaking && _rect != null) _rect.anchoredPosition = _shakeOrigin;
        _shaking = false;
    }
}
