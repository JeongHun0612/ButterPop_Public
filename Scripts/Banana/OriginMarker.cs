using UnityEngine;

/// <summary>
/// 바나나 시작점에 원형 마커를 표시하여 드래그 거리를 시각적으로 전달한다.
/// 취소 범위 안에 들어오면 색상이 변경된다.
/// </summary>
public class OriginMarker : MonoBehaviour
{
    [Header("마커")]
    [SerializeField] private SpriteRenderer markerSprite;

    [Header("색상")]
    [SerializeField] private Color defaultColor = new Color(1f, 1f, 1f, 0.5f);
    [Tooltip("취소 범위 안에 들어왔을 때 색상")]
    [SerializeField] private Color nearColor = new Color(0.5f, 1f, 0.5f, 0.7f);

    public void Show(Vector2 origin, Vector2 bananaPos, float cancelDistance)
    {
        if (markerSprite == null) return;

        transform.position = (Vector3)origin;
        markerSprite.enabled = true;

        float dist = Vector2.Distance(origin, bananaPos);
        markerSprite.color = dist <= cancelDistance ? nearColor : defaultColor;
    }

    public void Hide()
    {
        if (markerSprite != null)
            markerSprite.enabled = false;
    }
}
