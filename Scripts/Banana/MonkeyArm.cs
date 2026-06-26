using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MonkeyArm : MonoBehaviour
{
    [Header("회전 각도")]
    [Tooltip("대기 상태 각도")]
    [SerializeField] private float idleAngle = -30f;
    [Tooltip("올린 상태 각도")]
    [SerializeField] private float flipAngle = 70f;

    [Header("물리 회전 속도 (도/초)")]
    [SerializeField] private float raiseAngularSpeed = 360f;
    [SerializeField] private float lowerAngularSpeed = 180f;

    private Rigidbody2D _rb;
    private bool _isRaising;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.MoveRotation(idleAngle);
    }

    private void FixedUpdate()
    {
        float angle = _rb.rotation;

        if (_isRaising && angle < flipAngle)
        {
            float next = Mathf.Min(angle + raiseAngularSpeed * Time.fixedDeltaTime, flipAngle);
            _rb.MoveRotation(next);
        }
        else if (!_isRaising && angle > idleAngle)
        {
            float next = Mathf.Max(angle - lowerAngularSpeed * Time.fixedDeltaTime, idleAngle);
            _rb.MoveRotation(next);
        }
    }

    public void Raise() => _isRaising = true;
    public void Lower() => _isRaising = false;

    public void ResetArm()
    {
        _isRaising = false;
        _rb.MoveRotation(idleAngle);
    }
}
