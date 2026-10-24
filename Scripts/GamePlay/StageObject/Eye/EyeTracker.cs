using UnityEngine;

// 눈동자가 스퀴시를 따라 움직이는 눈. 왼쪽을 기준으로 위아래 viewAngle 범위 안에서만 따라본다.
// flipX로 뒤집힌 눈은 기준 방향도 함께 뒤집혀 오른쪽을 기준으로 따라본다.
public class EyeTracker : MonoBehaviour, IStageContextReceiver
{
    [Header("눈동자")]
    [Tooltip("스퀴시를 따라 움직일 검은자 Transform")]
    [SerializeField] private Transform pupil;

    [Header("추적")]
    [Tooltip("검은자가 타겟을 따라가는 속도")]
    [SerializeField] private float followSpeed = 8f;
    [Tooltip("좌측 기준 위아래로 움직일 수 있는 총 각도 (예: 30 → 위로 15도, 아래로 15도)")]
    [Range(0f, 180f)]
    [SerializeField] private float viewAngle = 30f;

    private Vector3 _pupilOrigin;
    private float _maxOffset;
    private Transform _target;
    private SquishBody _targetBody;

    private void Awake()
    {
        if (pupil == null)
        {
            Debug.LogError("[EyeTracker] pupil 참조가 비어 있습니다", this);
            enabled = false;
            return;
        }

        _pupilOrigin = pupil.localPosition;

        var eyeRenderer = GetComponent<SpriteRenderer>();
        var pupilRenderer = pupil.GetComponent<SpriteRenderer>();

        if (eyeRenderer != null && pupilRenderer != null)
        {
            float eyeRadius = eyeRenderer.sprite.bounds.extents.x;
            float pupilRadius = pupilRenderer.sprite.bounds.extents.x * pupil.localScale.x;
            _maxOffset = Mathf.Max(eyeRadius - pupilRadius, 0f);
        }
    }

    private void LateUpdate()
    {
        if (!TryGetTargetPosition(out var targetPos))
        {
            pupil.localPosition = Vector3.Lerp(pupil.localPosition, _pupilOrigin, Time.deltaTime * followSpeed);
            return;
        }

        // 스케일까지 반영해 로컬로 옮겨야 flipX(음수 스케일)에서도 눈동자가 타겟 쪽으로 간다.
        Vector3 dirLocal = transform.InverseTransformPoint(targetPos).normalized;

        float angle = Mathf.Atan2(dirLocal.y, dirLocal.x) * Mathf.Rad2Deg;

        float halfAngle = viewAngle * 0.5f;
        float delta = Mathf.DeltaAngle(180f, angle);
        angle = 180f + Mathf.Clamp(delta, -halfAngle, halfAngle);

        float rad = angle * Mathf.Deg2Rad;
        Vector2 clampedDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        Vector3 target = _pupilOrigin + (Vector3)clampedDir * _maxOffset;
        pupil.localPosition = Vector3.Lerp(pupil.localPosition, target, Time.deltaTime * followSpeed);
    }

    public void ReceiveStageContext(StageContext context)
    {
        // 스퀴시는 뼈 체인이라 루트 transform이 움직이지 않는다 → WorldBounds 중심을 추적한다.
        _targetBody = context?.Squish != null ? context.Squish.Body : null;
        _target = null;
    }

    public void SetTarget(Transform target)
    {
        _target = target;
        _targetBody = null;
    }

    private bool TryGetTargetPosition(out Vector3 position)
    {
        if (_targetBody != null)
        {
            position = _targetBody.WorldBounds.center;
            return true;
        }
        if (_target != null)
        {
            position = _target.position;
            return true;
        }
        position = default;
        return false;
    }
}
