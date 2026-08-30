using DG.Tweening;
using UnityEngine;

public class EmergencyButton : MonoBehaviour, ISquishHittable
{
    [Header("눌림 연출")]
    [Tooltip("빨간 버튼의 스퀴시 접촉을 감지하는 센서. 비어 있으면 자식에서 자동으로 찾는다")]
    [SerializeField] private SquishContactSensor contactSensor;

    [Tooltip("눌릴 빨간 버튼의 시각 요소. 물리 콜라이더와 분리된 Transform을 지정한다")]
    [SerializeField] private Transform buttonVisual;

    [Tooltip("버튼이 눌릴 때 아래로 이동할 로컬 거리")]
    [Min(0f)]
    [SerializeField] private float pressedYOffset = 0.15f;

    [Tooltip("버튼이 눌렸을 때 적용할 세로 크기 배율")]
    [Range(0.1f, 1f)]
    [SerializeField] private float pressedScaleY = 0.9f;

    [Tooltip("버튼이 눌리는 데 걸리는 시간(초)")]
    [Min(0f)]
    [SerializeField] private float pressDuration = 0.08f;

    [Tooltip("버튼이 원래 상태로 돌아오는 데 걸리는 시간(초)")]
    [Min(0f)]
    [SerializeField] private float releaseDuration = 0.15f;

    [Header("사운드")]
    [Tooltip("버튼을 누를 때 재생할 딸깍 클립")]
    [SerializeField] private AudioClip pressClip;

    [Header("사운드 재생 제한")]
    [Tooltip("빠른 재접촉으로 소리가 연타되는 것을 막는 최소 재생 간격(초). 버튼의 눌림 상태에는 영향을 주지 않는다")]
    [Min(0f)]
    [SerializeField] private float pressCooldown = 0.5f;

    private float _lastPressTime = float.NegativeInfinity;
    private Vector3 _visualOriginPosition;
    private Vector3 _visualOriginScale;
    private Sequence _buttonSequence;

    private void Awake()
    {
        if (contactSensor == null)
            contactSensor = GetComponentInChildren<SquishContactSensor>(true);

        if (contactSensor == null)
            Debug.LogWarning("[EmergencyButton] 스퀴시 접촉 센서가 없습니다", this);

        if (buttonVisual == null)
        {
            Debug.LogWarning("[EmergencyButton] 눌림 연출 대상 Button Visual이 없습니다", this);
            return;
        }

        _visualOriginPosition = buttonVisual.localPosition;
        _visualOriginScale = buttonVisual.localScale;
    }

    private void OnEnable()
    {
        if (contactSensor == null) return;

        contactSensor.OnSquishExited += ReleaseButton;

        // 컴포넌트만 껐다 켠 경우에도 현재 접촉 상태와 버튼 상태를 맞춘다.
        if (contactSensor.HasSquish)
            PressButton();
    }

    private void OnDisable()
    {
        if (contactSensor != null)
            contactSensor.OnSquishExited -= ReleaseButton;

        _buttonSequence?.Kill();
        _buttonSequence = null;
        ResetButtonVisual();
    }

    public void OnSquishHit(Collision2D collision) => PressButton();

    // 버튼을 누른 상태로 만든다. 사운드만 쿨다운을 적용해 시각 상태가 접촉과 어긋나지 않게 한다.
    public void PressButton()
    {
        PlayPressAnimation();

        if (Time.time - _lastPressTime >= pressCooldown)
        {
            _lastPressTime = Time.time;
            PlayPressSound();
        }
    }

    // 마지막 스퀴시 콜라이더가 버튼에서 사라졌을 때 원래 상태로 복귀한다.
    public void ReleaseButton()
    {
        if (buttonVisual == null) return;

        _buttonSequence?.Kill();

        _buttonSequence = DOTween.Sequence()
            .Append(buttonVisual.DOLocalMove(_visualOriginPosition, releaseDuration).SetEase(Ease.OutBack))
            .Join(buttonVisual.DOScale(_visualOriginScale, releaseDuration).SetEase(Ease.OutBack))
            .SetLink(buttonVisual.gameObject)
            .OnComplete(() => _buttonSequence = null)
            .OnKill(() => _buttonSequence = null);
    }

    private void PlayPressAnimation()
    {
        if (buttonVisual == null) return;

        _buttonSequence?.Kill();

        var pressedPosition = _visualOriginPosition + Vector3.down * pressedYOffset;
        var pressedScale = new Vector3(
            _visualOriginScale.x,
            _visualOriginScale.y * pressedScaleY,
            _visualOriginScale.z);

        _buttonSequence = DOTween.Sequence()
            .Append(buttonVisual.DOLocalMove(pressedPosition, pressDuration).SetEase(Ease.OutQuad))
            .Join(buttonVisual.DOScale(pressedScale, pressDuration).SetEase(Ease.OutQuad))
            .SetLink(buttonVisual.gameObject)
            .OnComplete(() => _buttonSequence = null)
            .OnKill(() => _buttonSequence = null);
    }

    private void ResetButtonVisual()
    {
        if (buttonVisual == null) return;

        buttonVisual.localPosition = _visualOriginPosition;
        buttonVisual.localScale = _visualOriginScale;
    }

    private void PlayPressSound()
    {
        if (pressClip == null) return;

        if (SoundManager.TryGetInstance(out var sound))
            sound.PlaySfx(pressClip);
    }
}
