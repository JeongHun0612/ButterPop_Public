using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 포인터를 누르면 UI를 축소하고 놓으면 원래 크기로 복원한다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ButtonAnimation : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private const int InvalidPointerId = int.MinValue;

    [Header("애니메이션")]
    [Tooltip("눌렀을 때 원래 크기에 곱할 값")]
    [Min(0f)]
    [SerializeField] private float pressScale = 0.95f;

    [Tooltip("눌림 애니메이션 시간(초)")]
    [Min(0f)]
    [SerializeField] private float pressDuration = 0.1f;

    [Tooltip("복원 애니메이션 시간(초)")]
    [Min(0f)]
    [SerializeField] private float releaseDuration = 0.4f;

    [SerializeField] private Ease pressEase = Ease.OutQuad;
    [SerializeField] private Ease releaseEase = Ease.OutBack;

    private RectTransform _rectTransform;
    private Selectable _selectable;
    private Vector3 _originalScale;
    private Tween _currentTween;
    private int _activePointerId = InvalidPointerId;
    private bool _isPressed;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _selectable = GetComponent<Selectable>();
        _originalScale = _rectTransform.localScale;
    }

    private void OnEnable()
    {
        ResetImmediately();
    }

    private void OnDisable()
    {
        ResetImmediately();
    }

    private void OnDestroy()
    {
        KillCurrentTween();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && _isPressed)
            ResetImmediately();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        if (_isPressed || !IsInteractable())
            return;

        _isPressed = true;
        _activePointerId = eventData.pointerId;

        PlayScaleTween(_originalScale * pressScale, pressDuration, pressEase);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData == null || !_isPressed || eventData.pointerId != _activePointerId)
            return;

        _isPressed = false;
        _activePointerId = InvalidPointerId;

        PlayScaleTween(_originalScale, releaseDuration, releaseEase);
    }

    /// <summary>
    /// 현재 크기를 이후 애니메이션의 기준 크기로 사용한다.
    /// </summary>
    public void UpdateOriginalScale()
    {
        KillCurrentTween();
        _isPressed = false;
        _activePointerId = InvalidPointerId;
        _originalScale = _rectTransform.localScale;
    }

    private bool IsInteractable() => _selectable == null || _selectable.IsInteractable();

    private void PlayScaleTween(Vector3 targetScale, float duration, Ease ease)
    {
        KillCurrentTween();

        if (duration <= 0f)
        {
            _rectTransform.localScale = targetScale;
            return;
        }

        _currentTween = _rectTransform
            .DOScale(targetScale, duration)
            .SetEase(ease)
            .SetUpdate(true);
    }

    private void ResetImmediately()
    {
        KillCurrentTween();
        _isPressed = false;
        _activePointerId = InvalidPointerId;

        if (_rectTransform != null)
            _rectTransform.localScale = _originalScale;
    }

    private void KillCurrentTween()
    {
        _currentTween?.Kill();
        _currentTween = null;
    }
}
