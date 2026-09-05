using System;
using System.Collections.Generic;
using UnityEngine;

// 끈끈이 표면. 낙하 중인 스퀴시 뼈가 닿는 순간 FixedJoint2D로 그 자리에 고정한다.
// 뼈마다 강체가 따로라 접착점도 뼈 단위로 생기고, 붙지 않은 뼈는 늘어져 흔들린다.
[RequireComponent(typeof(Collider2D))]
public class SquishSticky : MonoBehaviour
{
    // 용접할 때 표면 쪽으로 물려 두는 깊이. contactOffset(0.01)보다 작아 떠 보이지도, 밀려나지도 않는다.
    private const float ContactBite = 0.005f;

    // 끊어진 조인트는 Unity가 컴포넌트째 파괴하므로 목록에서 걷어낸다.
    private static readonly Predicate<FixedJoint2D> IsBroken = joint => joint == null;

    [Header("접착")]
    [Tooltip("동시에 붙일 수 있는 최대 뼈 수. 1이면 처음 닿은 뼈 하나만 붙고 나머지는 매달려 흔들린다")]
    [Min(1)]
    [SerializeField] private int maxStickPoints = 3;
    [Tooltip("충돌 상대속도가 이 값보다 느리면 스친 것으로 보고 붙이지 않는다. 0이면 스치기만 해도 붙는다")]
    [Min(0f)]
    [SerializeField] private float minStickSpeed;

    [Header("접착 강도")]
    [Tooltip("접착 스프링 진동수(Hz). 0이면 완전히 굳고, 올릴수록 끈적하게 늘어난다")]
    [Min(0f)]
    [SerializeField] private float stickFrequency;
    [Tooltip("접착 스프링 감쇠비. stickFrequency가 0이면 무시된다")]
    [Range(0f, 1f)]
    [SerializeField] private float stickDamping = 1f;
    [Tooltip("이 힘을 넘게 당기면 접착이 떨어진다. 0이면 떨어지지 않는다")]
    [Min(0f)]
    [SerializeField] private float breakForce;

    private readonly List<FixedJoint2D> _joints = new();

    private StageObject _stageObject;
    private Squish _heldSquish;
    private Rigidbody2D _body;   // 없으면 월드 좌표에 고정한다
    private bool _sticking;      // 낙하 중에만 붙인다

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _stageObject = GetComponentInParent<StageObject>();

        if (_stageObject == null)
            Debug.LogWarning($"[SquishSticky] StageObject를 찾지 못해 안착 판정에 접착을 알릴 수 없습니다: {name}", this);
    }

    private void OnEnable()
    {
        if (!StageManager.TryGetInstance(out var stageManager)) return;

        stageManager.OnStateChanged += HandleStateChanged;
        _sticking = stageManager.CurrentState == GameState.Falling;
    }

    // 스퀴시는 스테이지 간 재사용되므로 파괴 전에 조인트를 반드시 뗀다.
    private void OnDisable()
    {
        ReleaseAll();

        if (!StageManager.TryGetInstance(out var stageManager)) return;

        stageManager.OnStateChanged -= HandleStateChanged;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!_sticking) return;

        _joints.RemoveAll(IsBroken);
        if (_joints.Count >= maxStickPoints) return;

        var bone = collision.rigidbody;
        if (bone == null || collision.contactCount == 0) return;

        var squish = collision.collider.GetComponentInParent<Squish>();
        if (squish == null) return;
        if (IsStuck(bone)) return;
        if (collision.relativeVelocity.magnitude < minStickSpeed) return;

        Stick(squish, bone, collision.GetContact(0));
    }

    // 붙는 순간 되튕기지 않도록 그 뼈의 속도만 지우고 용접한다.
    // 좌표는 Transform이 아니라 Rigidbody2D.position에서 읽는다(autoSyncTransforms가 꺼져 있어 어긋날 수 있다).
    private void Stick(Squish squish, Rigidbody2D bone, ContactPoint2D contact)
    {
        bone.linearVelocity = Vector2.zero;
        bone.angularVelocity = 0f;

        var joint = bone.gameObject.AddComponent<FixedJoint2D>();
        joint.connectedBody = _body;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector2.zero;

        Vector2 target = ResolveWeldPosition(bone, contact);
        joint.connectedAnchor = _body != null ? _body.GetPoint(target) : target;

        // 조인트는 연결된 두 바디의 충돌을 기본으로 끈다. 표면을 뚫고 들어가지 않게 켜둔다.
        joint.enableCollision = true;
        joint.frequency = stickFrequency;
        joint.dampingRatio = stickDamping;
        joint.breakForce = breakForce > 0f ? breakForce : Mathf.Infinity;

        _joints.Add(joint);

        // 용접된 뼈는 물리 접촉이 끊겨도 얹힌 상태라 안착 판정에 직접 알린다.
        _heldSquish = squish;
        squish.HoldStageContact(_stageObject);
    }

    // 뼈를 고정할 월드 위치. 충돌은 실제로 맞닿기 전에 통지되므로 남은 틈만큼 표면 쪽으로 당긴다.
    // 방향은 접촉점→뼈 중심 벡터로 구한다(뼈 콜라이더가 원·정사각형이라 곧 바깥 법선이다).
    private static Vector2 ResolveWeldPosition(Rigidbody2D bone, ContactPoint2D contact)
    {
        float gap = contact.separation + ContactBite;
        if (gap <= 0f) return bone.position;

        Vector2 outward = bone.position - contact.point;
        if (outward.sqrMagnitude <= Mathf.Epsilon) return bone.position;

        return bone.position - outward.normalized * gap;
    }

    private bool IsStuck(Rigidbody2D bone)
    {
        for (int i = 0; i < _joints.Count; i++)
        {
            if (_joints[i].attachedRigidbody == bone) return true;
        }

        return false;
    }

    private void ReleaseAll()
    {
        for (int i = 0; i < _joints.Count; i++)
        {
            if (_joints[i] != null) Destroy(_joints[i]);
        }

        _joints.Clear();

        if (_heldSquish == null) return;

        _heldSquish.ReleaseStageContact(_stageObject);
        _heldSquish = null;
    }

    // 낙하 중에만 붙이고, 다른 상태로 넘어가면 붙은 것을 모두 뗀다.
    private void HandleStateChanged(GameState state)
    {
        _sticking = state == GameState.Falling;
        if (!_sticking) ReleaseAll();
    }
}
