using System;
using UnityEngine;

[DisallowMultipleComponent]
public class Squish : MonoBehaviour
{
    private const float HangingAngleDeg = -90f;

    [Header("하위 컴포넌트 (비우면 Awake에서 자동 할당)")]
    [SerializeField] private SquishSettleDetector settleDetector;
    [SerializeField] private SquishBody body;
    [SerializeField] private SquishSimpleMesh mesh;
    [SerializeField] private HandRig handRig;

    [Header("앵커")]
    [Tooltip("손끝에서 매달릴 위치(핀 포인트). Rigidbody 없이 offset Transform으로만 사용한다.")]
    [SerializeField] private Transform pivotAnchor;

    [Header("릴리즈")]
    [SerializeField] private bool clampSpeed = true;
    [SerializeField] private float maxSpeed = 12f;

    private Vector2 _spawnPosition;
    private bool _released;

    public event Action OnDropped;
    public event Action<Bounds> OnSettled;
    public event Action OnFell;

    public SquishBody Body => body;
    public SquishSimpleMesh Mesh => mesh;
    public bool CanDrop => !_released && (handRig == null || !handRig.IsPlaying);

    private void Awake()
    {
        if (settleDetector == null) settleDetector = GetComponentInChildren<SquishSettleDetector>(true);
        if (body == null) body = GetComponentInChildren<SquishBody>(true);
        if (mesh == null) mesh = GetComponentInChildren<SquishSimpleMesh>(true);
        if (handRig == null) handRig = GetComponentInChildren<HandRig>(true);

        if (settleDetector == null || body == null || mesh == null || handRig == null)
        {
            Debug.LogError("[Squish] 하위 컴포넌트(settle/body/mesh/handRig)를 찾지 못했습니다.", this);
            enabled = false;
            return;
        }

        settleDetector.OnSettled += HandleSettled;
        settleDetector.OnFell += HandleFell;
    }

    private void FixedUpdate()
    {
        if (body == null || !body.IsReady) return;

        // 잡힌 동안 손끝을 따라간다. N=0(힌지) 모드에서는 물리가 전담하므로 내부에서 no-op.
        if (!_released)
            body.UpdateGrab(GetPinPoint(), HangingAngleDeg);

        if (_released && clampSpeed)
            body.ClampVelocities(maxSpeed);
    }

    private void OnDestroy()
    {
        if (settleDetector != null)
        {
            settleDetector.OnSettled -= HandleSettled;
            settleDetector.OnFell -= HandleFell;
        }
    }

    // 장착된 스퀴시 스킨(스프라이트 + 타격음)을 렌더러에 반영한다.
    public void ApplySkin(SquishSkinSO skin)
    {
        if (mesh != null) mesh.ApplySkin(skin);
    }

    public void SetSpawnPosition(Vector2 spawnPosition) => _spawnPosition = spawnPosition;

    public void RespawnAt()
    {
        if (body == null || !body.IsReady) return;

        _released = false;

        transform.position = _spawnPosition;
        handRig.PrepareEnter();

        // 전 뼈를 얼려 정확한 rest 포즈로 스냅한다(ResetPoseFromLeadingEdge가 Physics2D.SyncTransforms로 물리에 즉시 반영).
        body.SetKinematic(true);
        body.ResetPoseFromLeadingEdge(GetPinPoint(), HangingAngleDeg);
        mesh.ResetShape();
        settleDetector.ResetState();

        // 다시 Dynamic으로 살리고 잔여 속도를 청소한 뒤, 손끝(HandRig Rigidbody)에 붙잡는다.
        // 잡는 방식(힌지 매달림 vs 앞 N개 강체 그랩)은 SquishBody가 grabbedBoneCount로 결정한다.
        body.SetKinematic(false);
        body.ClearVelocities();
        body.Grab(handRig.Body, GetPinPoint(), HangingAngleDeg);
    }

    public void Enter() => handRig.PlayEnter(null);

    public void Drop()
    {
        if (!CanDrop) return;

        DropInternal();
        handRig.PlayExit();
    }

    public void Freeze()
    {
        body.Release();
        body.SetKinematic(true);
    }

    public void BeginJudging() => settleDetector.BeginJudging();

    public void SuspendJudging() => settleDetector.SuspendJudging();

    public void ForceSettle(GameObject extraObject = null) => settleDetector.ForceSettle(extraObject);

    public void UpdateCameraBounds(Camera cam) => settleDetector.UpdateCameraBounds(cam);

    private void DropInternal()
    {
        _released = true;
        body.Release();
        OnDropped?.Invoke();
    }

    private Vector2 GetPinPoint()
    {
        return pivotAnchor != null ? (Vector2)pivotAnchor.position : (Vector2)transform.position;
    }

    private void HandleSettled(Bounds bounds) => OnSettled?.Invoke(bounds);
    private void HandleFell() => OnFell?.Invoke();

    private void OnDrawGizmos()
    {
        Vector3 p = pivotAnchor != null ? pivotAnchor.position : transform.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(p, 0.05f);
        Gizmos.DrawLine(p + Vector3.up * 0.1f, p + Vector3.down * 0.1f);
        Gizmos.DrawLine(p + Vector3.left * 0.1f, p + Vector3.right * 0.1f);
    }
}
