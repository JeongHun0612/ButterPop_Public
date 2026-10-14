using System;
using System.Collections.Generic;
using UnityEngine;

// 스퀴시 오브젝트의 안착/낙하 판정기. SquishBody 뼈 체인 기반.
// - 충돌은 뼈마다 분산되므로 SquishBody의 Enter/Exit 이벤트를 구독해 접촉을 refcount로 추적한다.
// - 속도는 SquishBody.MaxBoneSpeed, 위치·경계는 WorldBounds를 사용한다(루트 transform은 뼈를 따라가지 않음).
[RequireComponent(typeof(SquishBody))]
public class SquishSettleDetector : MonoBehaviour
{
    [Header("판정")]
    [SerializeField] private float settleSpeedThreshold = 0.3f;
    [SerializeField] private float settleTimeRequired = 0.5f;
    [SerializeField] private float outOfBoundsMargin = 2f;

    private readonly Dictionary<StageObject, int> _stageContacts = new();

    // 기믹이 직접 선언한 접촉. 조인트로 용접된 뼈는 물리 접촉이 끊겨도 얹힌 상태다.
    private readonly HashSet<StageObject> _heldContacts = new();

    private SquishBody _body;
    private int _armContacts;

    // 화면 밖 판정 경계(월드 좌표). 아직 계산되지 않았다면 어떤 위치도 밖으로 보지 않는다.
    // 0으로 두면 경계 계산 전에 원점 밖 스퀴시가 즉시 실패 처리된다.
    private float _fallYThreshold = float.NegativeInfinity;
    private float _fallXMin = float.NegativeInfinity;
    private float _fallXMax = float.PositiveInfinity;

    private float _settleTimer;
    private bool _judging;
    private bool _paused;     // 팝업 등으로 잠시 멈춘 상태. _judging은 유지된다.
    private bool _settling;   // 안착 조건 충족 중(안착 타이머 누적 중)

    public event Action<Bounds> OnSettled;
    public event Action OnFell;
    public event Action<bool> OnSettlingChanged;

    public bool IsSettling => _settling;

    private bool IsOnStage => _stageContacts.Count > 0 || _heldContacts.Count > 0;

    private void Awake()
    {
        _body = GetComponent<SquishBody>();
        _body.OnSegmentCollisionEnter += OnSegmentEnter;
        _body.OnSegmentCollisionExit += OnSegmentExit;
    }

    private void OnDestroy()
    {
        if (_body == null) return;
        _body.OnSegmentCollisionEnter -= OnSegmentEnter;
        _body.OnSegmentCollisionExit -= OnSegmentExit;
    }

    private void Update()
    {
        if (!_judging || _paused)
            return;

        var center = _body.WorldBounds.center;
        if (center.y < _fallYThreshold || center.x < _fallXMin || center.x > _fallXMax)
        {
            OnFailed();
            return;
        }

        SetSettling(IsOnStage && _armContacts == 0 && _body.MaxBoneSpeed < settleSpeedThreshold);

        if (_settling)
        {
            _settleTimer += Time.deltaTime;
            if (_settleTimer >= settleTimeRequired)
            {
                OnSuccess();
            }
        }
        else
        {
            _settleTimer = 0f;
        }
    }

    public void BeginJudging()
    {
        ResetState();
        _judging = true;
    }

    public void SuspendJudging() => _judging = false;

    // 팝업이 떠 있는 동안 판정만 멈춘다(물리는 계속 돈다).
    // 멈춘 사이 스퀴시가 움직였을 수 있으므로 재개 시 안착을 처음부터 다시 확인한다.
    public void SetJudgingPaused(bool paused)
    {
        if (_paused == paused) return;

        _paused = paused;
        _settleTimer = 0f;
        if (paused) SetSettling(false);
    }

    // 스퀴시를 조인트로 붙잡는 기믹이 얹혀 있음을 직접 선언한다.
    public void HoldContact(StageObject stage)
    {
        if (stage == null) return;

        _heldContacts.Add(stage);
    }

    // 파괴된 오브젝트도 빠지도록 null 체크 없이 넘긴다.
    public void ReleaseContact(StageObject stage) => _heldContacts.Remove(stage);

    // 카메라 시야 + 여백을 벗어나면 낙하로 본다. 좌우도 카메라 중심 기준이라
    // cameraCenter.x가 0이 아닌 스테이지에서도 경계가 대칭으로 잡힌다.
    public void UpdateCameraBounds(Camera cam)
    {
        if (cam == null)
        {
            Debug.LogError("[SquishSettleDetector] 카메라가 없어 낙하 경계를 계산하지 못했습니다", this);
            return;
        }

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 camPos = cam.transform.position;

        _fallYThreshold = camPos.y - halfH - outOfBoundsMargin;
        _fallXMin = camPos.x - halfW - outOfBoundsMargin;
        _fallXMax = camPos.x + halfW + outOfBoundsMargin;
    }

    // _paused는 팝업 쪽이 소유하므로 여기서 건드리지 않는다. 팝업이 열린 채 스테이지가 갈려도 잠금이 유지돼야 한다.
    public void ResetState()
    {
        _judging = false;
        _settleTimer = 0f;
        _stageContacts.Clear();
        _heldContacts.Clear();
        _armContacts = 0;
        SetSettling(false);
    }

    public Bounds GetSettledBounds()
    {
        var bounds = _body.WorldBounds;

        foreach (var stage in _stageContacts.Keys)
        {
            if (stage == null) continue;
            bounds.Encapsulate(RendererUtils.GetWorldBounds(stage.gameObject));
        }

        foreach (var stage in _heldContacts)
        {
            if (stage == null) continue;
            bounds.Encapsulate(RendererUtils.GetWorldBounds(stage.gameObject));
        }

        return bounds;
    }

    public void ForceSettle(GameObject extraObject = null)
    {
        _judging = false;

        var bounds = _body.WorldBounds;
        if (extraObject != null)
            bounds.Encapsulate(RendererUtils.GetWorldBounds(extraObject));

        Debug.Log("<color=green>[Result] SUCCESS - 외부 기믹에 의한 클리어</color>");
        OnSettled?.Invoke(bounds);
    }

    private void SetSettling(bool value)
    {
        if (_settling == value) return;

        _settling = value;
        OnSettlingChanged?.Invoke(value);
    }

    private void OnSuccess()
    {
        _judging = false;
        SetSettling(false);
        Debug.Log("<color=green>[Result] SUCCESS - 스퀴시가 스테이지에 안착했습니다!</color>");
        OnSettled?.Invoke(GetSettledBounds());
    }

    private void OnFailed()
    {
        _judging = false;
        SetSettling(false);
        Debug.Log("<color=red>[Result] FAILED - 스퀴시가 떨어졌습니다!</color>");
        OnFell?.Invoke();
    }

    private void OnSegmentEnter(Collision2D collision)
    {
        var arm = collision.gameObject.GetComponentInParent<ArmController>();
        if (arm != null)
        {
            _armContacts++;
            return;
        }

        var stage = collision.gameObject.GetComponent<StageObject>();
        if (stage != null)
            _stageContacts[stage] = _stageContacts.TryGetValue(stage, out var c) ? c + 1 : 1;
    }

    private void OnSegmentExit(Collision2D collision)
    {
        if (collision.gameObject.GetComponentInParent<ArmController>() != null)
        {
            _armContacts = Mathf.Max(0, _armContacts - 1);
            return;
        }

        var stage = collision.gameObject.GetComponent<StageObject>();
        if (stage != null && _stageContacts.TryGetValue(stage, out var c))
        {
            if (c <= 1) _stageContacts.Remove(stage);
            else _stageContacts[stage] = c - 1;
        }
    }
}
