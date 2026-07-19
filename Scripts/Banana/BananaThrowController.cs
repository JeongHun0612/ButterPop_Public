using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(HingeJoint2D))]
public class BananaThrowController : MonoBehaviour
{
    [Header("진자")]
    [Tooltip("시작 시 기울임 각도 (도). 클수록 넓게 흔들림.")]
    [SerializeField] private float swingAmplitude = 40f;

    [Header("속도 제한")]
    [Tooltip("릴리즈 후 최대 속도 상한 (화면 이탈 방지)")]
    [SerializeField] private bool clampSpeed = true;
    [SerializeField] private float maxSpeed = 12f;

    private bool _released;

    private Rigidbody2D _rb;
    private HingeJoint2D _hinge;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _hinge = GetComponent<HingeJoint2D>();
        _hinge.enabled = false;
        SetKinematic();
    }

    private void FixedUpdate()
    {
        if (_released && clampSpeed)
        {
            Vector2 v = _rb.linearVelocity;
            if (v.magnitude > maxSpeed)
                _rb.linearVelocity = v.normalized * maxSpeed;
        }
    }

    public void StartSwing()
    {
        _released = false;

        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.position = transform.position;
        _rb.rotation = swingAmplitude;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;

        _hinge.enabled = true;
        _hinge.anchor = Vector2.zero;
        _hinge.connectedAnchor = transform.position;
    }

    public void DropBanana()
    {
        if (_released) return;
        _released = true;

        if (_hinge != null)
            _hinge.enabled = false;

        StageManager.Instance.SetState(GameState.Falling);
    }

    public void ResetBanana()
    {
        _released = false;

        if (_hinge != null)
            _hinge.enabled = false;

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
}
