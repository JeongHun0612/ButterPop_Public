using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ArmController : MonoBehaviour
{
    private const int ContactBufferSize = 16;

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

    [Header("끼임 방지")]
    [Tooltip("팔이 스퀴시를 미는 방향과 고정된 오브젝트가 스퀴시를 미는 방향의 내적이 이 값 이하면 끼임으로 보고 올리기를 멈춘다.")]
    [Range(-1f, 0f)]
    [SerializeField] private float pinchDotThreshold = -0.5f;

    private readonly ContactPoint2D[] _armContacts = new ContactPoint2D[ContactBufferSize];
    private readonly ContactPoint2D[] _boneContacts = new ContactPoint2D[ContactBufferSize];
    private Rigidbody2D _rb;
    private ContactFilter2D _contactFilter;
    private bool _raising;
    private float _angle;
    private float _simVel;

    public bool IsRaising => _raising;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _contactFilter = new ContactFilter2D { useTriggers = false };
        _angle = idleAngle;
        _rb.rotation = idleAngle;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float prevAngle = _angle;
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

        // Kinematic 팔은 끼인 스퀴시에 밀려나지 않아 계속 올리면 스퀴시가 얇은 팔을 관통한다.
        if (_angle > prevAngle && IsSquishPinched())
        {
            _angle = prevAngle;
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

    // 팔에 닿은 스퀴시가 반대편에서 고정된 오브젝트에 막혀 있는지 확인한다.
    private bool IsSquishPinched()
    {
        int count = _rb.GetContacts(_contactFilter, _armContacts);
        for (int i = 0; i < count; i++)
        {
            Rigidbody2D bone = _armContacts[i].rigidbody;
            if (bone == null) continue;

            var body = bone.GetComponentInParent<SquishBody>();
            if (body == null) continue;

            // 접촉 법선은 접촉을 받는 쪽(팔)을 향하므로 팔이 스퀴시를 미는 방향은 그 반대다.
            if (IsBlockedAgainst(body, -_armContacts[i].normal))
                return true;
        }

        return false;
    }

    // 밀려나지 않는 오브젝트(정적·Kinematic)가 armPush와 반대 방향으로 스퀴시를 받치고 있는지 확인한다.
    private bool IsBlockedAgainst(SquishBody body, Vector2 armPush)
    {
        var bones = body.BoneRigidbodies;
        for (int b = 0; b < bones.Count; b++)
        {
            int count = bones[b].GetContacts(_contactFilter, _boneContacts);
            for (int i = 0; i < count; i++)
            {
                Rigidbody2D other = _boneContacts[i].rigidbody;
                if (other == _rb) continue;
                if (other != null && other.bodyType == RigidbodyType2D.Dynamic) continue;

                if (Vector2.Dot(_boneContacts[i].normal, armPush) <= pinchDotThreshold)
                    return true;
            }
        }

        return false;
    }
}
