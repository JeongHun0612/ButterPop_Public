using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BananaThrowController : MonoBehaviour
{
    [Header("물리")]
    [SerializeField] private float gravityScale = 1.0f;
    [SerializeField] private float linearDrag = 0.1f;
    [SerializeField] private float angularDrag = 0.2f;
    [SerializeField] private float mass = 1.0f;

    [Header("흔들림")]
    [SerializeField] private float swingAngle = 30f;
    [SerializeField] private float swingSpeed = 3f;

    [Header("드롭")]
    [SerializeField] private float dropVelocityMultiplier = 1f;
    [SerializeField] private float dropTorqueMultiplier = 1f;

    private Rigidbody2D _rb;
    private float _swingTime;
    private bool _isSwinging;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        SetKinematic();
    }

    private void Update()
    {
        if (!_isSwinging) return;

        _swingTime += Time.deltaTime;
        transform.localRotation = Quaternion.Euler(0f, 0f, swingAngle * Mathf.Sin(_swingTime * swingSpeed));
    }

    public void StartSwing()
    {
        _swingTime = 0f;
        _isSwinging = true;
        transform.localRotation = Quaternion.identity;
    }

    public void DropBanana()
    {
        float angularSpeed = swingAngle * swingSpeed * Mathf.Cos(_swingTime * swingSpeed);
        float angularSpeedRad = angularSpeed * Mathf.Deg2Rad;

        _isSwinging = false;
        SetDynamic();
        _rb.linearVelocity = new Vector2(angularSpeedRad * dropVelocityMultiplier, 0f);
        _rb.angularVelocity = angularSpeed * dropTorqueMultiplier;

        StageManager.Instance.SetState(GameState.Falling);
    }

    public void ResetBanana()
    {
        SetKinematic();
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void FreezeInPlace() => SetKinematic();

    private void SetKinematic()
    {
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
    }

    private void SetDynamic()
    {
        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.gravityScale = gravityScale;
        _rb.linearDamping = linearDrag;
        _rb.angularDamping = angularDrag;
        _rb.mass = mass;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }
}
