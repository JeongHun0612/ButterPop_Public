using System;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class Squish : MonoBehaviour
{
    private const float HangingAngleDeg = -90f;

    [Header("하위 컴포넌트 (비우면 Awake에서 자동 할당)")]
    [SerializeField] private SquishSettleDetector settleDetector;
    [SerializeField] private SquishBody body;
    [SerializeField] private SquishMesh mesh;
    [SerializeField] private SquishImpactAudio impactAudio;
    [SerializeField] private CraneRig craneRig;

    [Header("앵커")]
    [Tooltip("크레인 그립 포인트에서 매달릴 위치. Rigidbody 없이 offset Transform으로만 사용한다.")]
    [SerializeField] private Transform pivotAnchor;

    [Header("릴리즈")]
    [SerializeField] private bool clampSpeed = true;
    [SerializeField] private float maxSpeed = 12f;

    private Vector2 _spawnPosition;
    private bool _released;
    private bool _dropRequested;

    public event Action OnDropped;
    public event Action<Bounds> OnSettled;
    public event Action OnFell;
    public event Action<bool> OnSettlingChanged;

    public SquishBody Body => body;
    public SquishMesh Mesh => mesh;
    public SquishSkinSO CurrentSkin { get; private set; }
    public bool IsSettling => settleDetector != null && settleDetector.IsSettling;
    public bool CanDrop => !_released && !_dropRequested && craneRig != null && !craneRig.IsPlaying;

    private void Awake()
    {
        if (settleDetector == null) settleDetector = GetComponentInChildren<SquishSettleDetector>(true);
        if (body == null) body = GetComponentInChildren<SquishBody>(true);
        if (mesh == null) mesh = GetComponentInChildren<SquishMesh>(true);
        if (impactAudio == null) impactAudio = GetComponentInChildren<SquishImpactAudio>(true);
        if (craneRig == null) craneRig = GetComponentInChildren<CraneRig>(true);

        if (settleDetector == null || body == null || mesh == null || craneRig == null || craneRig.Body == null)
        {
            Debug.LogError("[Squish] 하위 컴포넌트(settle/body/mesh/craneRig)를 찾지 못했습니다.", this);
            enabled = false;
            return;
        }

        settleDetector.OnSettled += HandleSettled;
        settleDetector.OnFell += HandleFell;
        settleDetector.OnSettlingChanged += HandleSettlingChanged;
    }

    private void FixedUpdate()
    {
        if (body == null || !body.IsReady) return;

        // 잡힌 동안 크레인 그립 포인트를 따라간다. N=0(힌지) 모드에서는 물리가 전담하므로 내부에서 no-op.
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
            settleDetector.OnSettlingChanged -= HandleSettlingChanged;
        }
    }

    // 장착된 스킨의 이미지와 타격음을 함께 갱신한다.
    public void ApplySkin(SquishSkinSO skin)
    {
        if (skin == null || skin.sprite == null)
        {
            Debug.LogWarning("[Squish] 스프라이트가 없는 스킨은 적용할 수 없습니다.", this);
            return;
        }

        CurrentSkin = skin;
        if (mesh != null) mesh.ApplySkin(skin);
        if (impactAudio != null) impactAudio.ApplySkin(skin);
    }

    public void SetSpawnPosition(Vector2 spawnPosition) => _spawnPosition = spawnPosition;

    public void RespawnAt()
    {
        if (body == null || !body.IsReady) return;

        _released = false;
        _dropRequested = false;

        transform.position = _spawnPosition;
        craneRig.PrepareEnter();

        // 전 뼈를 얼려 정확한 rest 포즈로 스냅한다(ResetPoseFromLeadingEdge가 Physics2D.SyncTransforms로 물리에 즉시 반영).
        body.SetKinematic(true);
        body.ResetPoseFromLeadingEdge(GetPinPoint(), HangingAngleDeg);
        mesh.ResetShape();
        if (impactAudio != null) impactAudio.ResetCooldown();
        settleDetector.ResetState();

        // 다시 Dynamic으로 살리고 잔여 속도를 청소한 뒤, 크레인의 물리 홀더에 붙잡는다.
        // 잡는 방식(힌지 매달림 vs 앞 N개 강체 그랩)은 SquishBody가 grabbedBoneCount로 결정한다.
        body.SetKinematic(false);
        body.ClearVelocities();
        body.Grab(craneRig.Body, GetPinPoint(), HangingAngleDeg);
    }

    public void Enter() => craneRig.PlayEnter(null);

    public void Drop()
    {
        if (!CanDrop) return;

        _dropRequested = true;
        DropInternal();
        craneRig.PlayRelease(craneRig.PlayExit);
    }

    public void Freeze()
    {
        body.Release();
        body.SetKinematic(true);
    }

    public void BeginJudging() => settleDetector.BeginJudging();

    public void SuspendJudging() => settleDetector.SuspendJudging();

    public void SetJudgingPaused(bool paused) => settleDetector.SetJudgingPaused(paused);

    public void ForceSettle(GameObject extraObject = null) => settleDetector.ForceSettle(extraObject);

    public void HoldStageContact(StageObject stage) => settleDetector.HoldContact(stage);

    public void ReleaseStageContact(StageObject stage) => settleDetector.ReleaseContact(stage);

    public void UpdateCameraBounds(Camera cam) => settleDetector.UpdateCameraBounds(cam);

    private void DropInternal()
    {
        _released = true;
        _dropRequested = false;
        body.Release();
        OnDropped?.Invoke();
    }

    private Vector2 GetPinPoint()
    {
        return pivotAnchor != null ? (Vector2)pivotAnchor.position : (Vector2)transform.position;
    }

    private void HandleSettled(Bounds bounds) => OnSettled?.Invoke(bounds);
    private void HandleFell() => OnFell?.Invoke();
    private void HandleSettlingChanged(bool settling) => OnSettlingChanged?.Invoke(settling);

    private void OnDrawGizmos()
    {
        Vector3 p = pivotAnchor != null ? pivotAnchor.position : transform.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(p, 0.05f);
        Gizmos.DrawLine(p + Vector3.up * 0.1f, p + Vector3.down * 0.1f);
        Gizmos.DrawLine(p + Vector3.left * 0.1f, p + Vector3.right * 0.1f);
    }
}
