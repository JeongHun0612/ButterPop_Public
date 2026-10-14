using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

// 우주선(UFO) 스테이지 오브젝트.
// 하단 빔(UfoBeam)에 스퀴시가 닿으면 접시 아래로 빨아올려(어브덕션) 스테이지를 클리어한다.
// 스퀴시는 뼈 체인(독립 rigidbody)이라 루트 transform 이동이 아니라 SquishBody를 kinematic으로 얼려
// SnapPose로 매 틱 재배치한다.
public class UFO : MonoBehaviour, IStageContextReceiver
{
    [Header("어브덕션 목표")]
    [Tooltip("스퀴시가 빨려 올라가 멈출 지점. 빔과 우주선이 연결되는 부위에 배치하고, 우주선 하위에 두면 회전도 함께 따라간다. 비우면 이 오브젝트 위치를 사용")]
    [SerializeField] private Transform abductPoint;
    [Tooltip("abductPoint 기준으로 스퀴시가 취할 로컬 각도(도). 최종 각도 = abductPoint 각도 + 이 값. 예: 가로로 눕히는 오프셋")]
    [SerializeField] private float squishLocalAngle = 90f;
    [Tooltip("빔 가운데 X를 구할 빔 콜라이더. 이미지 피벗이 어긋나 있어도 콜라이더 실제 형태 기준으로 중앙을 계산한다. 비우면 abductPoint의 X를 사용")]
    [SerializeField] private Collider2D beamCollider;

    [Header("연출")]
    [Tooltip("빔 중심으로 수평 이동하며 목표 자세로 회전하는 데 걸리는 시간(초)")]
    [SerializeField] private float alignDuration = 0.35f;
    [Tooltip("정렬 후 빔 중심축을 따라 목표까지 곧장 상승하는 시간(초)")]
    [SerializeField] private float riseDuration = 0.6f;

    [Header("빔 깜빡임")]
    [Tooltip("상승 중 깜빡일 빔 스프라이트. 비우면 깜빡임 없이 상승만 한다")]
    [SerializeField] private SpriteRenderer beamRenderer;
    [Tooltip("깜빡일 때 낮아지는 최소 알파(0~1)")]
    [Range(0f, 1f)]
    [SerializeField] private float beamFlickerMinAlpha = 0.35f;
    [Tooltip("알파가 한 번 낮아지는 데 걸리는 시간(초). 짧을수록 빠르게 깜빡인다")]
    [SerializeField] private float beamFlickerInterval = 0.08f;

    private StageContext _context;
    private bool _abducting;

    public void ReceiveStageContext(StageContext context)
    {
        _context = context;
        _abducting = false;
    }

    public void Abduct()
    {
        if (_abducting) return;
        if (_context?.Squish == null) return;
        Debug.Assert(StageManager.Instance != null, "[UFO] StageManager가 없습니다");
        if (StageManager.Instance.CurrentState != GameState.Falling) return;

        _abducting = true;
        AbductAsync().Forget();
    }

    private async UniTaskVoid AbductAsync()
    {
        var squish = _context.Squish;
        var body = squish.Body;
        var cancellationToken = _context.StageCancellationToken;
        Tween flicker = null;

        squish.SuspendJudging();
        squish.Freeze();   // 뼈를 kinematic으로 고정한 뒤 스크립트로 옮긴다.

        Transform targetRef = abductPoint != null ? abductPoint : transform;

        // rest 중심(shape-local centroid). SnapPose 기준점(shape-origin)에서 이만큼 떨어져 몸체 중앙이 놓인다.
        Vector2 centroidRest = ComputeCentroidRest(body);

        Vector2 startCenter = body.WorldBounds.center;
        float startAngle = RecoverBaseAngle(body);
        float targetAngle = targetRef.eulerAngles.z + squishLocalAngle;

        Vector3 target = targetRef.position;

        // 진입 높이에서의 빔 단면 중앙 X. 위쪽 좁은 구간으로 들어와도 빔 안에 정렬되도록 그 Y에서 구한다.
        float centerX = beamCollider != null
            ? GetBeamCenterX(beamCollider, startCenter.y, target.x)
            : target.x;
        Vector2 alignCenter = new Vector2(centerX, startCenter.y);

        // 몸체 중앙이 원하는 위치·각도에 오도록 SnapPose 기준점(shape-origin)을 역산해 배치한다.
        void PlaceByCenter(Vector2 center, float angle)
        {
            float rad = angle * Mathf.Deg2Rad;
            Vector2 basePos = center - Rotate(centroidRest, rad);
            body.SnapPose(basePos, angle);
        }

        try
        {
            // 1단계(정렬): 빔 중심으로 수평 이동 + 목표 자세로 회전.
            bool canceled = await DOTween.To(() => 0f, t =>
                    PlaceByCenter(Vector2.Lerp(startCenter, alignCenter, t), Mathf.LerpAngle(startAngle, targetAngle, t)),
                    1f, alignDuration)
                .SetEase(Ease.InOutSine)
                .SetLink(gameObject)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken)
                .SuppressCancellationThrow();
            if (canceled || this == null) return;

            // 2단계(상승): 상승 동안만 빔 깜빡임(Yoyo) + 중심축 따라 등속 상승.
            flicker = beamRenderer != null
                ? beamRenderer.DOFade(beamFlickerMinAlpha, beamFlickerInterval).SetLoops(-1, LoopType.Yoyo).SetLink(beamRenderer.gameObject)
                : null;

            canceled = await DOTween.To(() => 0f, t => PlaceByCenter(Vector2.Lerp(alignCenter, (Vector2)target, t), targetAngle), 1f, riseDuration)
                .SetEase(Ease.Linear)
                .SetLink(gameObject)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken)
                .SuppressCancellationThrow();
            if (canceled || this == null || squish == null) return;

            // 스퀴시 + 우주선을 함께 담아 결과 캡처가 어브덕션 장면을 잡도록 한다.
            squish.ForceSettle(gameObject);
        }
        finally
        {
            if (flicker != null && flicker.IsActive())
                flicker.Kill();

            if (beamRenderer != null)
            {
                Color c = beamRenderer.color;
                c.a = 1f;
                beamRenderer.color = c;
            }

            if (this != null)
                _abducting = false;
        }
    }

    // rest 뼈 위치들의 평균(shape-local 중심).
    private static Vector2 ComputeCentroidRest(SquishBody body)
    {
        var rest = body.BoneRestSamples;
        if (rest == null || rest.Count == 0) return Vector2.zero;

        Vector2 sum = Vector2.zero;
        for (int i = 0; i < rest.Count; i++)
            sum += rest[i].position;
        return sum / rest.Count;
    }

    // 현재 뼈 포즈에서 SnapPose 기준 각도를 역산한다(0번 뼈 기준, rest 각도 0).
    private static float RecoverBaseAngle(SquishBody body)
    {
        var bones = body.BoneRigidbodies;
        if (bones.Count == 0) return 0f;
        return bones[0].rotation;
    }

    private static Vector2 Rotate(Vector2 v, float rad)
    {
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
        return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
    }

    // 주어진 높이(worldY)에서 빔 콜라이더 단면의 좌우 끝을 훑어 가운데 X를 구한다. 못 찾으면 fallbackX.
    private static float GetBeamCenterX(Collider2D col, float worldY, float fallbackX, int samples = 64)
    {
        Bounds b = col.bounds;
        float minX = float.NaN, maxX = float.NaN;

        for (int i = 0; i <= samples; i++)
        {
            float x = Mathf.Lerp(b.min.x, b.max.x, i / (float)samples);
            if (!col.OverlapPoint(new Vector2(x, worldY))) continue;

            if (float.IsNaN(minX)) minX = x;
            maxX = x;
        }

        return float.IsNaN(minX) ? fallbackX : (minX + maxX) * 0.5f;
    }
}
