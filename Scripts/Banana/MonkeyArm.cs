using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Rigidbody2D))]
public class MonkeyArm : MonoBehaviour
{
    [Header("회전 설정")]
    [Tooltip("대기 상태 각도 (대각선 아래)")]
    [SerializeField] private float idleAngle = -30f;
    [Tooltip("올린 상태 각도 (위쪽)")]
    [SerializeField] private float flipAngle = 70f;

    [Header("애니메이션")]
    [SerializeField] private float raiseDuration = 0.1f;
    [SerializeField] private float lowerDuration = 0.3f;
    [SerializeField] private Ease raiseEase = Ease.OutBack;
    [SerializeField] private Ease lowerEase = Ease.InOutQuad;

    [Header("타격")]
    [Tooltip("바나나 발사 세기")]
    [SerializeField] private float launchPower = 8f;
    [Tooltip("발사 각도 (0=오른쪽, 90=위, 60=오른쪽 위)")]
    [SerializeField] private float launchAngle = 60f;

    private Rigidbody2D _rb;
    private float _currentAngle;
    private Tweener _activeTween;
    private bool _isRaising;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _currentAngle = idleAngle;
        _rb.rotation = idleAngle;
    }

    private void FixedUpdate()
    {
        _rb.MoveRotation(_currentAngle);
    }

    public void Raise()
    {
        _isRaising = true;
        _activeTween?.Kill();
        _activeTween = DOTween.To(() => _currentAngle, x => _currentAngle = x, flipAngle, raiseDuration)
            .SetEase(raiseEase);
    }

    public void Lower()
    {
        _isRaising = false;
        _activeTween?.Kill();
        _activeTween = DOTween.To(() => _currentAngle, x => _currentAngle = x, idleAngle, lowerDuration)
            .SetEase(lowerEase);
    }

    public void ResetArm()
    {
        _isRaising = false;
        _activeTween?.Kill();
        _currentAngle = idleAngle;
        _rb.rotation = idleAngle;
    }
}
