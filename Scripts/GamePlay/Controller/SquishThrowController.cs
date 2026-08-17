using System;
using UnityEngine;

// 스퀴시 오브젝트의 던지기 컨트롤러. SquishBody 뼈 체인을 구동한다.
// - 공중: 끝 뼈를 고정 피벗에 런타임 HingeJoint2D로 걸어 진자 스윙(소프트바디 출렁임 유지).
// - 드롭: 앵커 힌지를 끊어 현재 속도 그대로 자유 낙하.
// - SquishBody와 같은 루트에 붙인다(뼈 콜라이더가 GetComponentInParent로 이 컴포넌트를 찾는다).
[RequireComponent(typeof(SquishBody))]
public class SquishThrowController : MonoBehaviour
{
    [Header("진자")]
    [Tooltip("시작 시 기울임 각도 (도). 클수록 넓게 흔들림.")]
    [SerializeField] private float swingAmplitude = 40f;
    [Tooltip("스윙 피벗으로 삼을 끝 뼈. 체크 시 마지막 뼈, 해제 시 첫 뼈에 매단다.")]
    [SerializeField] private bool hangFromLastBone = false;

    [Header("속도 제한")]
    [Tooltip("릴리즈 후 최대 속도 상한 (화면 이탈 방지)")]
    [SerializeField] private bool clampSpeed = true;
    [SerializeField] private float maxSpeed = 12f;

    [Header("부착 기준점")]
    [Tooltip("어브덕션 등에서 몸체 중앙으로 삼을 앵커. 비우면 WorldBounds 중심을 사용.")]
    [SerializeField] private Transform centerAnchor;

    [Header("비주얼")]
    [Tooltip("타는 연출 등 틴트에 사용할 스퀴시 메쉬. 비우면 Awake에서 GetComponent.")]
    [SerializeField] private SquishMesh mesh;

    private SquishBody _body;
    private HingeJoint2D _anchorHinge;
    private bool _released;

    public event Action OnDropped;

    public SquishBody Body => _body;
    public SquishMesh Mesh => mesh;
    public Transform CenterAnchor => centerAnchor;

    private void Awake()
    {
        _body = GetComponent<SquishBody>();
        if (mesh == null) mesh = GetComponent<SquishMesh>();
    }

    private void FixedUpdate()
    {
        if (!_released || !clampSpeed || _body == null) return;

        var bones = _body.Bones;
        for (int i = 0; i < bones.Count; i++)
        {
            Vector2 v = bones[i].linearVelocity;
            if (v.magnitude > maxSpeed)
                bones[i].linearVelocity = v.normalized * maxSpeed;
        }
    }

    // 스폰 위치에 진자 자세로 배치하고 앵커 힌지를 걸어 스윙을 시작한다.
    public void StartSwing()
    {
        if (_body == null) return;

        _released = false;
        if (mesh != null)
        {
            mesh.ResetShape();
            mesh.SetTint(Color.white);   // 이전 스테이지의 타는 연출 틴트 원복
        }

        Vector2 basePos = transform.position;
        _body.ResetLayout(basePos, swingAmplitude);
        _body.SetKinematic(false);

        EnsureAnchorHinge();
        if (_anchorHinge != null)
        {
            _anchorHinge.connectedAnchor = _anchorHinge.attachedRigidbody.position;
            _anchorHinge.enabled = true;
        }
    }

    // 앵커 힌지를 끊어 현재 속도 그대로 낙하시킨다.
    public void Drop()
    {
        if (_released) return;
        _released = true;

        if (_anchorHinge != null)
            _anchorHinge.enabled = false;

        OnDropped?.Invoke();
    }

    public void FreezeInPlace()
    {
        if (_anchorHinge != null)
            _anchorHinge.enabled = false;

        if (_body != null)
            _body.SetKinematic(true);
    }

    // 매다는 끝 뼈에 고정 피벗 힌지를 1회 생성한다(connectedBody 없음 = 월드 고정).
    private void EnsureAnchorHinge()
    {
        if (_anchorHinge != null) return;

        var bones = _body.Bones;
        if (bones.Count == 0) return;

        var endBone = hangFromLastBone ? bones[bones.Count - 1] : bones[0];

        _anchorHinge = endBone.gameObject.AddComponent<HingeJoint2D>();
        _anchorHinge.autoConfigureConnectedAnchor = false;
        _anchorHinge.anchor = Vector2.zero;   // 뼈 로컬 원점을 피벗으로
        _anchorHinge.useLimits = false;
        _anchorHinge.enabled = false;
    }
}
