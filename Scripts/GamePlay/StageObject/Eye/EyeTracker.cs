using UnityEngine;

public class EyeTracker : MonoBehaviour, IStageContextReceiver
{
    [SerializeField] private Transform pupil;

    [Tooltip("검은자가 타겟을 따라가는 속도")]
    [SerializeField] private float followSpeed = 8f;

    [Tooltip("좌측 기준 위아래로 움직일 수 있는 총 각도 (예: 30 → 위로 15도, 아래로 15도)")]
    [Range(0f, 180f)]
    [SerializeField] private float viewAngle = 30f;

    private Vector3 _pupilOrigin;
    private float _maxOffset;
    private Transform _target;
    private SquishBody _targetBody;

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

    private void Awake()
    {
        if (pupil == null) return;

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
        if (pupil == null) return;

        if (!TryGetTargetPosition(out var targetPos))
        {
            pupil.localPosition = Vector3.Lerp(pupil.localPosition, _pupilOrigin, Time.deltaTime * followSpeed);
            return;
        }

        Vector3 dirWorld = targetPos - transform.position;
        Vector3 dirLocal = transform.InverseTransformDirection(dirWorld).normalized;

        float angle = Mathf.Atan2(dirLocal.y, dirLocal.x) * Mathf.Rad2Deg;

        float halfAngle = viewAngle * 0.5f;
        float delta = Mathf.DeltaAngle(180f, angle);
        angle = 180f + Mathf.Clamp(delta, -halfAngle, halfAngle);

        float rad = angle * Mathf.Deg2Rad;
        Vector2 clampedDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        Vector3 target = _pupilOrigin + (Vector3)clampedDir * _maxOffset;
        pupil.localPosition = Vector3.Lerp(pupil.localPosition, target, Time.deltaTime * followSpeed);
    }
}
