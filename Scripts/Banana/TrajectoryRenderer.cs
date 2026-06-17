using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class TrajectoryRenderer : MonoBehaviour
{
    [Header("궤적")]
    [SerializeField] private int pointCount = 20;
    [SerializeField] private float timeStep = 0.05f;

    [Header("길이")]
    [Tooltip("드래그 최소/최대 시 궤적 길이")]
    [SerializeField] private float minLength = 0.5f;
    [SerializeField] private float maxLength = 2.0f;

    private LineRenderer _line;

    private void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.positionCount = 0;
        _line.useWorldSpace = true;
    }

    /// <param name="dragRatio">드래그 비율 (0~1), 최대 드래그 대비 현재 거리</param>
    public void ShowTrajectory(Vector2 startPos, Vector2 velocity, float dragRatio)
    {
        Vector2 gravity = Physics2D.gravity;
        // 드래그 비율에 비례하여 궤적 길이 결정
        float lengthLimit = Mathf.Lerp(minLength, maxLength, dragRatio);

        var points = new Vector3[pointCount];
        int usedCount = 1;
        points[0] = startPos;
        float totalLength = 0f;

        for (int i = 1; i < pointCount; i++)
        {
            float t = i * timeStep;
            Vector2 point = startPos + velocity * t + 0.5f * gravity * (t * t);
            float segmentLength = ((Vector2)point - (Vector2)points[usedCount - 1]).magnitude;
            totalLength += segmentLength;

            // 길이 한도 초과 시 마지막 지점을 보간하여 정확히 자름
            if (totalLength > lengthLimit)
            {
                Vector2 dir = ((Vector2)point - (Vector2)points[usedCount - 1]).normalized;
                float remaining = lengthLimit - (totalLength - segmentLength);
                points[usedCount] = (Vector2)points[usedCount - 1] + dir * remaining;
                usedCount++;
                break;
            }

            points[usedCount] = point;
            usedCount++;
        }

        _line.positionCount = usedCount;
        for (int i = 0; i < usedCount; i++)
            _line.SetPosition(i, points[i]);
    }

    public void Hide()
    {
        _line.positionCount = 0;
    }
}
