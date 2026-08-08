using System;
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

    [Header("부착 기준점")]
    [SerializeField] private Transform centerAnchor;

    [Header("비주얼")]
    [Tooltip("실제 바나나 스프라이트 렌더러(BananaVisual)")]
    [SerializeField] private SpriteRenderer bananaRenderer;

    private Rigidbody2D _rb;
    private HingeJoint2D _hinge;

    private Color _originalBananaColor = Color.white;
    private bool _released;

    public event Action OnDropped;

    public Transform CenterAnchor => centerAnchor;
    public SpriteRenderer BananaRenderer => bananaRenderer;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _hinge = GetComponent<HingeJoint2D>();
        _hinge.enabled = false;
        SetKinematic();

        if (bananaRenderer != null)
            _originalBananaColor = bananaRenderer.color;
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

        if (bananaRenderer != null)
            bananaRenderer.color = _originalBananaColor;

        if (_hinge != null)
            _hinge.enabled = false;

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

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

        OnDropped?.Invoke();
    }

    public void FreezeInPlace() => SetKinematic();

    private void SetKinematic()
    {
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
    }
}
