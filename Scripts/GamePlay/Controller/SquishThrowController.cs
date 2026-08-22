using System;
using System.Collections;
using UnityEngine;

// SquishBody를 손끝 offset에 핀으로 매달고 Drop 때 풀어준다.
[RequireComponent(typeof(SquishBody))]
public class SquishThrowController : MonoBehaviour
{
    private const float HangingAngleDeg = -90f;

    [Header("앵커")]
    [Tooltip("손끝에서 매달릴 위치(핀 포인트). Rigidbody 없이 offset Transform으로만 사용한다.")]
    [SerializeField] private Transform pivotAnchor;

    [Header("릴리즈")]
    [SerializeField] private bool clampSpeed = true;
    [SerializeField] private float maxSpeed = 12f;

    [Header("부착 기준점")]
    [Tooltip("어브덕션 등에서 몸체 중앙으로 삼을 앵커. 비우면 WorldBounds 중심을 사용.")]
    [SerializeField] private Transform centerAnchor;

    [Header("잡기")]
    [Tooltip("켜면 잡힌 동안 Bone_0을 손끝에 직접 고정하고 Bone_1부터 물리로 흔들리게 한다.")]
    [SerializeField] private bool holdHeadBoneToFingertip = true;

    private SquishBody _body;
    private Coroutine _startRoutine;
    private bool _arming;
    private bool _released;

    public event Action OnDropped;
    // 리셋 직후 물리 안정화(arming)가 끝났을 때 발생. 파사드가 지연된 등장 연출을 재생하는 데 사용한다.
    public event Action OnArmed;

    public Transform CenterAnchor => centerAnchor;
    public Vector2 PinPoint => GetPinPoint();
    public float HangingAngle => HangingAngleDeg;
    public bool IsArming => _arming;
    public bool CanDrop => !_released && !_arming;

    private void Awake()
    {
        _body = GetComponent<SquishBody>();
    }

    private void FixedUpdate()
    {
        if (!_released && holdHeadBoneToFingertip)
            UpdateHeldHeadBone(true);

        if (!_released || !clampSpeed || _body == null || !_body.IsReady) return;

        var bones = _body.BoneRigidbodies;
        for (int i = 0; i < bones.Count; i++)
        {
            Vector2 v = bones[i].linearVelocity;
            if (v.magnitude > maxSpeed)
                bones[i].linearVelocity = v.normalized * maxSpeed;
        }
    }

    // 리셋된 스퀴시를 떨어뜨릴 수 있는 상태로 안정화(arming)한다.
    // Body 자세/속도와 Mesh 초기화는 파사드(Squish)가 이 호출 전에 처리한다.
    public void PrepareRespawn()
    {
        if (_body == null || !_body.IsReady) return;

        _released = false;
        _arming = true;
        if (_startRoutine != null)
        {
            StopCoroutine(_startRoutine);
            _startRoutine = null;
        }

        _startRoutine = StartCoroutine(ArmAfterReset());
    }

    public void Drop()
    {
        if (!CanDrop) return;

        _released = true;
        SetHeadBoneGrabbed(false);
        OnDropped?.Invoke();
    }

    public void FreezeInPlace()
    {
        if (_startRoutine != null)
        {
            StopCoroutine(_startRoutine);
            _startRoutine = null;
        }
        _arming = false;
        SetHeadBoneGrabbed(false);
        if (_body != null)
            _body.SetKinematic(true);
    }

    // 리셋 직후 물리를 안정화(동적 전환 + 머리뼈 잡기)한다. 화면 안 등장은 하지 않는다.
    private IEnumerator ArmAfterReset()
    {
        yield return new WaitForFixedUpdate();

        _body.SetKinematic(false);
        SetHeadBoneGrabbed(true);
        _body.ClearVelocities();
        _arming = false;
        _startRoutine = null;

        // 파사드가 arming 중 예약된 등장 연출을 지금 재생하도록 알린다.
        OnArmed?.Invoke();
    }

    private Rigidbody2D GetHeadBody()
    {
        if (_body == null || !_body.IsReady || _body.BoneRigidbodies.Count == 0)
            return null;

        return _body.BoneRigidbodies[0];
    }

    private Vector2 GetPinPoint()
    {
        return pivotAnchor != null ? (Vector2)pivotAnchor.position : (Vector2)transform.position;
    }

    private float GetTopToFirstBoneOffset()
    {
        if (_body.BoneRestSamples.Count == 0) return 0f;

        return _body.Length * 0.5f + _body.BoneRestSamples[0].position.x;
    }

    private void SetHeadBoneGrabbed(bool grabbed)
    {
        Rigidbody2D head = GetHeadBody();
        if (head == null) return;

        head.bodyType = grabbed ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        head.linearVelocity = Vector2.zero;
        head.angularVelocity = 0f;

        if (grabbed)
            UpdateHeldHeadBone(false);
    }

    private void UpdateHeldHeadBone(bool sweep)
    {
        Rigidbody2D head = GetHeadBody();
        if (head == null) return;

        Vector2 position = GetPinPoint() + Vector2.down * GetTopToFirstBoneOffset();

        if (sweep && head.bodyType == RigidbodyType2D.Kinematic)
        {
            head.MovePosition(position);
            head.MoveRotation(HangingAngleDeg);
        }
        else
        {
            head.position = position;
            head.rotation = HangingAngleDeg;
            head.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, HangingAngleDeg));
        }

        head.linearVelocity = Vector2.zero;
        head.angularVelocity = 0f;
    }

    private void OnDrawGizmos()
    {
        Vector3 p = pivotAnchor != null ? pivotAnchor.position : transform.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(p, 0.05f);
        Gizmos.DrawLine(p + Vector3.up * 0.1f, p + Vector3.down * 0.1f);
        Gizmos.DrawLine(p + Vector3.left * 0.1f, p + Vector3.right * 0.1f);
    }
}
