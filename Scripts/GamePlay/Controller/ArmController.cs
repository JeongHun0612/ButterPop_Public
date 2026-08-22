using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ArmController : MonoBehaviour
{
    [Header("각도")]
    [SerializeField] private float idleAngle = -15f;
    [SerializeField] private float raisedAngle = 20f;

    [Header("PD (스프링-댐퍼) 게인")]
    [Tooltip("목표각으로 당기는 강성. 높을수록 빠르고 세게 친다(오버슈트 증가).")]
    [SerializeField] private float pGain = 200f;
    [Tooltip("각속도 감쇠. 높을수록 묵직/안정, 낮을수록 출렁/진동.")]
    [SerializeField] private float dGain = 25f;

    [Header("플릭 임펄스")]
    [Tooltip("올리는 순간 각속도 버스트를 주입하여 짧은 호에서도 강하게 날림.")]
    [SerializeField] private bool useFlickImpulse = true;
    [Tooltip("주입 각속도 (deg/s).")]
    [SerializeField] private float flickImpulse = 700f;

    private Rigidbody2D _rb;
    private bool _raising;
    private float _angle;
    private float _simVel;

    public bool IsRaising => _raising;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _angle = idleAngle;
        _rb.rotation = idleAngle;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float target = _raising ? raisedAngle : idleAngle;

        float error = Mathf.DeltaAngle(_angle, target);
        float accel = pGain * error - dGain * _simVel;
        _simVel += accel * dt;
        _angle += _simVel * dt;

        if (_angle < idleAngle) 
        { 
            _angle = idleAngle; 
            if (_simVel < 0f) 
                _simVel = 0f; 
        }
        else if (_angle > raisedAngle) 
        {
            _angle = raisedAngle; 
            if (_simVel > 0f) 
                _simVel = 0f; 
        }

        _rb.MoveRotation(_angle);
    }

    // 스테이지 스폰 위치에 리그를 배치하고 자세를 초기화한다.
    // ArmController 컴포넌트는 리그 자식(arm pivot)에 있고 위치 대상은 루트 래퍼라 transform.root를 옮긴다.
    // (이 리그는 항상 독립 인스턴스화되므로 transform.root == 인스턴스 루트)
    public void ResetAt(Vector2 position)
    {
        transform.root.position = new Vector3(position.x, position.y, 0f);
        ResetArm();
    }

    public void Raise()
    {
        if (!_raising && useFlickImpulse)
            _simVel += flickImpulse;

        _raising = true;
    }

    public void Lower() => _raising = false;

    private void ResetArm()
    {
        _raising = false;
        _simVel = 0f;
        _angle = idleAngle;
        _rb.rotation = idleAngle;
    }
}
