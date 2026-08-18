using System;
using UnityEngine;

// SquishBody를 손끝 힌지에 매달고 드롭 상태를 전환한다.
[RequireComponent(typeof(SquishBody))]
public class SquishThrowController : MonoBehaviour
{
    [Header("잡기")]
    [Tooltip("매달 월드 위치(손끝). 프리팹에서 이 Transform의 HingeJoint2D(connectedBody)를 원하는 뼈에 직접 배선한다. " +
             "스폰 시 프리팹에 배치한 자세로 맞춘 뒤 이 힌지를 켜서 매단다.")]
    [SerializeField] private Transform pivotAnchor;

    [Header("초기 스윙")]
    [Tooltip("스폰 시 핀(pivotAnchor) 기준으로 이만큼(도) 기울여 놓고 놓아 진자처럼 흔들리게 한다. 위치는 고정. 0 = 프리팹 자세 그대로(안 흔들림).")]
    [SerializeField] private float swingAmplitude = 20f;

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

        if (pivotAnchor != null)
        {
            _anchorHinge = pivotAnchor.GetComponent<HingeJoint2D>();
            if (_anchorHinge != null)
                _anchorHinge.enabled = false;
        }
    }

    private void FixedUpdate()
    {
        if (!_released || !clampSpeed || _body == null || !_body.IsReady) return;

        var bones = _body.BoneRigidbodies;
        for (int i = 0; i < bones.Count; i++)
        {
            Vector2 v = bones[i].linearVelocity;
            if (v.magnitude > maxSpeed)
                bones[i].linearVelocity = v.normalized * maxSpeed;
        }
    }

    // 프리팹에 배치한 자세로 초기화하고 손끝 힌지에 매단다.
    public void StartSwing()
    {
        if (_body == null || !_body.IsReady) return;

        _released = false;

        Vector2 pivot = pivotAnchor != null ? (Vector2)pivotAnchor.position : (Vector2)transform.position;
        _body.SnapPose(transform.position, transform.eulerAngles.z);
        _body.RotatePoseAround(pivot, swingAmplitude);
        _body.SetKinematic(false);

        if (mesh != null)
        {
            mesh.ResetShape();
            mesh.SetTint(Color.white);
        }

        if (_anchorHinge != null)
            _anchorHinge.enabled = true;
        else
            Debug.LogWarning("[SquishThrowController] pivotAnchor(Fingertip)에 HingeJoint2D가 없습니다. 프리팹에서 힌지를 배선하세요.");
    }

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

    private void OnDrawGizmos()
    {
        Vector3 p = pivotAnchor != null ? pivotAnchor.position : transform.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(p, 0.05f);
        Gizmos.DrawLine(p + Vector3.up * 0.1f, p + Vector3.down * 0.1f);
        Gizmos.DrawLine(p + Vector3.left * 0.1f, p + Vector3.right * 0.1f);
    }
}
