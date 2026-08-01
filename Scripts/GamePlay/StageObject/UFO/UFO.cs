using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

// 우주선(UFO) 스테이지 오브젝트.
// 하단 빔(UfoBeam)에 바나나가 닿으면 바나나를 접시 아래로 빨아올려(어브덕션) 스테이지를 클리어한다.
public class UFO : MonoBehaviour, IStageContextReceiver
{
    [Header("어브덕션 목표")]
    [Tooltip("바나나가 빨려 올라가 멈출 지점. 빔과 우주선이 연결되는 부위에 배치하고, 우주선 하위에 두면 회전도 함께 따라간다. 비우면 이 오브젝트 위치를 사용")]
    [SerializeField] private Transform abductPoint;
    [Tooltip("abductPoint 기준으로 바나나가 취할 로컬 각도(도). 최종 각도 = abductPoint 각도 + 이 값. 예: 가로로 눕히는 오프셋")]
    [SerializeField] private float bananaLocalAngle = 90f;
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
        if (_context?.Banana == null || _context.SettleDetector == null) return;
        if (StageManager.Instance == null || StageManager.Instance.CurrentState != GameState.Falling) return;

        _abducting = true;
        AbductAsync().Forget();
    }

    private async UniTaskVoid AbductAsync()
    {
        var banana = _context.Banana;
        var detector = _context.SettleDetector;

        detector.SuspendJudging();
        banana.FreezeInPlace();

        var bananaTf = banana.transform;
        Transform targetRef = abductPoint != null ? abductPoint : transform;

        // 피벗(꼭지)이 아니라 몸체 중앙이 목표에 오도록 오프셋을 보정한다. 앵커 없으면 렌더러 중심.
        Vector3 startCenter = banana.CenterAnchor != null
            ? banana.CenterAnchor.position
            : RendererUtils.GetVisualCenter(banana.gameObject);
        Vector3 localCenterOffset = Quaternion.Inverse(bananaTf.rotation) * (startCenter - bananaTf.position);
        Vector3 target = targetRef.position;

        // 어떤 각도로 들어왔든 항상 같은 포즈로 정렬. Z축만 쓰므로 각도 덧셈으로 축약된다.
        float startAngle = bananaTf.eulerAngles.z;
        float targetAngle = targetRef.eulerAngles.z + bananaLocalAngle;

        // 진입 높이에서의 빔 단면 중앙 X. 위쪽 좁은 구간으로 들어와도 빔 안에 정렬되도록 그 Y에서 구한다.
        float centerX = beamCollider != null
            ? GetBeamCenterX(beamCollider, startCenter.y, target.x)
            : target.x;
        Vector3 alignCenter = new Vector3(centerX, startCenter.y, startCenter.z);

        bananaTf.DOKill();

        // 몸체 중앙이 원하는 위치·각도에 오도록 회전만큼 오프셋을 되돌려 피벗 위치를 역산한다.
        void PlaceByCenter(Vector3 center, float angle)
        {
            Quaternion rot = Quaternion.Euler(0f, 0f, angle);
            Vector3 pos = center - rot * localCenterOffset;
            pos.z = startCenter.z;
            bananaTf.SetPositionAndRotation(pos, rot);
        }

        // 1단계(정렬): 빔 중심으로 수평 이동 + 목표 자세로 회전.
        await DOTween.To(() => 0f, t =>
                PlaceByCenter(Vector3.Lerp(startCenter, alignCenter, t), Mathf.LerpAngle(startAngle, targetAngle, t)),
                1f, alignDuration)
            .SetEase(Ease.InOutSine).SetLink(bananaTf.gameObject).ToUniTask();

        // 2단계(상승): 상승 동안만 빔 깜빡임(Yoyo) + 중심축 따라 등속 상승.
        Tween flicker = beamRenderer != null
            ? beamRenderer.DOFade(beamFlickerMinAlpha, beamFlickerInterval).SetLoops(-1, LoopType.Yoyo).SetLink(beamRenderer.gameObject)
            : null;

        await DOTween.To(() => 0f, t => PlaceByCenter(Vector3.Lerp(alignCenter, target, t), targetAngle), 1f, riseDuration)
            .SetEase(Ease.Linear).SetLink(bananaTf.gameObject).ToUniTask();

        if (beamRenderer != null)
        {
            flicker.Kill();
            Color c = beamRenderer.color;
            c.a = 1f;
            beamRenderer.color = c;
        }

        // 어브덕션 중 스테이지가 재구성되면 참조가 사라질 수 있으니 방어.
        if (detector == null) return;

        // 바나나 + 우주선을 함께 담아 결과 캡처가 어브덕션 장면을 잡도록 한다.
        detector.ForceSettle(gameObject);

        _abducting = false;
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
