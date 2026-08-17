using System;
using UnityEngine;

// 스퀴시 오브젝트의 던지기 컨트롤러. SquishBody 뼈 체인을 구동한다.
// - 공중: 끝 뼈를 고정 피벗에 런타임 HingeJoint2D로 걸어 진자 스윙(소프트바디 출렁임 유지).
// - 드롭: 앵커 힌지를 끊어 현재 속도 그대로 자유 낙하.
// - SquishBody와 같은 루트에 붙인다(뼈 콜라이더가 GetComponentInParent로 이 컴포넌트를 찾는다).
[RequireComponent(typeof(SquishBody))]
public class SquishThrowController : MonoBehaviour
{
    [Header("잡기")]
    [Tooltip("손끝(pivotAnchor 위치)에 매달 스파인 위 지점. 0 = 한쪽 끝, 0.5 = 중앙, 1 = 반대 끝. 세로로 끝을 잡으려면 0 또는 1. (회전·스케일 무관)")]
    [Range(0f, 1f)]
    [SerializeField] private float grabPoint = 0f;
    [Tooltip("매달 월드 위치(손끝). 이 Transform 자리에 grabPoint 지점을 핀으로 고정한다. 비우면 루트 원점.")]
    [SerializeField] private Transform pivotAnchor;

    [Header("진자")]
    [Tooltip("시작 시 기울임 각도 (도). 클수록 넓게 흔들림.")]
    [SerializeField] private float swingAmplitude = 40f;

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

        // 손끝(pivotAnchor 월드 위치)에 스파인의 grabPoint 지점을 핀으로 고정한다.
        // grabPoint = "형태의 어느 점을 잡을지"(shape-local, 회전·스케일 무관), pivotAnchor = "어느 월드 위치에 매달지".
        Vector2 pivotWorld = pivotAnchor != null ? (Vector2)pivotAnchor.position : (Vector2)transform.position;
        int grabIndex = GrabBoneIndex();
        Vector2 pivotLocal = grabIndex >= 0 ? _body.BoneRestSamples[grabIndex].position : Vector2.zero;

        _body.ResetLayout(pivotWorld, pivotLocal, _body.SpawnRotationDeg + swingAmplitude);
        _body.SetKinematic(false);

        EnsureAnchorHinge(grabIndex);
        if (_anchorHinge != null)
            _anchorHinge.enabled = true;
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

    // grabPoint 뼈를 Fingertip(pivotAnchor)의 Kinematic Rigidbody2D에 힌지로 매단다.
    // 힌지는 뼈가 아니라 Fingertip에 살고(connectedBody = grab 뼈), 손끝이 움직이면 매달린 몸체가 따라온다.
    // ResetLayout이 grab 뼈 원점을 손끝 위치에 배치하므로 양쪽 anchor 모두 원점(0,0)으로 딱 맞물린다.
    private void EnsureAnchorHinge(int boneIndex)
    {
        if (_anchorHinge != null) return;

        if (pivotAnchor == null)
        {
            Debug.LogError("[SquishThrowController] pivotAnchor(Fingertip)가 필요합니다.");
            return;
        }

        var bones = _body.Bones;
        if (boneIndex < 0 || boneIndex >= bones.Count) return;

        var fingertipRb = pivotAnchor.GetComponent<Rigidbody2D>();
        if (fingertipRb == null)
        {
            Debug.LogError("[SquishThrowController] pivotAnchor에 Rigidbody2D가 필요합니다(Kinematic 권장).");
            return;
        }
        fingertipRb.bodyType = RigidbodyType2D.Kinematic;   // 손끝은 물리에 안 밀리고 Transform으로만 움직이는 고정 앵커

        _anchorHinge = pivotAnchor.GetComponent<HingeJoint2D>();
        if (_anchorHinge == null) _anchorHinge = pivotAnchor.gameObject.AddComponent<HingeJoint2D>();
        _anchorHinge.autoConfigureConnectedAnchor = false;
        _anchorHinge.connectedBody = bones[boneIndex];
        _anchorHinge.anchor = Vector2.zero;            // 손끝 원점 = 피벗
        _anchorHinge.connectedAnchor = Vector2.zero;   // grab 뼈 원점
        _anchorHinge.useLimits = false;
        _anchorHinge.enabled = false;
    }

    // grabPoint(0~1)를 뼈 인덱스로 변환. -1 = 뼈 없음.
    private int GrabBoneIndex()
    {
        int count = _body.Bones.Count;
        if (count == 0) return -1;
        return Mathf.Clamp(Mathf.RoundToInt(grabPoint * (count - 1)), 0, count - 1);
    }

    // 편집 시점 피벗 마커. SquishBody 실루엣 기즈모와 함께 보고 피벗 위치를 잡는다.
    private void OnDrawGizmos()
    {
        Vector3 p = pivotAnchor != null ? pivotAnchor.position : transform.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(p, 0.05f);
        Gizmos.DrawLine(p + Vector3.up * 0.1f, p + Vector3.down * 0.1f);
        Gizmos.DrawLine(p + Vector3.left * 0.1f, p + Vector3.right * 0.1f);
    }
}
