using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 저작 편의(에디터 기즈모 전용): 프리팹/씬에서 스퀴시 오브젝트의 실제 크기(SquishBody.length/width)를
// 손 등 참조 스프라이트와 나란히 실제 월드 크기로 그려, 재-굽기 없이도
// "손과 함께 어느 정도로 보일지"를 눈으로 확인하게 한다.
// - 런타임 동작 없음. 빌드에는 데이터만 남고 아무것도 하지 않는다(기즈모는 에디터 전용).
// - SquishBody 자신의 기즈모(노란 사각형 = length×width)와 함께 본다.
[RequireComponent(typeof(SquishBody))]
public class SquishSizeGuide : MonoBehaviour
{
    [Header("크기 비교 참조 (손 등)")]
    [Tooltip("실제 크기 비교용 참조 스프라이트(예: 손). 원본 bounds 크기(월드 단위)로 그린다.")]
    [SerializeField] private Sprite referenceSprite;
    [Tooltip("스퀴시 중심 기준으로 참조를 놓을 오프셋(월드 단위). 손이 아래에서 친다면 음수 y.")]
    [SerializeField] private Vector2 referenceOffset = new Vector2(0f, -3f);
    [Tooltip("크기 수치 라벨 표시")]
    [SerializeField] private bool showLabels = true;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var body = GetComponent<SquishBody>();
        if (body == null || body.Length <= 0f || body.Width <= 0f) return;

        // SquishBody 기즈모와 같은 기준점(SquishVisual)에 맞춘다.
        var mesh = GetComponent<SquishMesh>();
        Vector2 center = (mesh != null && mesh.VisualRenderer != null)
            ? (Vector2)mesh.VisualRenderer.transform.position
            : (Vector2)transform.position;

        if (showLabels)
            Handles.Label(center + new Vector2(0f, body.Width * 0.5f + 0.3f),
                $"Squish  {body.Length:0.##} × {body.Width:0.##}");

        if (referenceSprite == null) return;

        Vector2 refSize = referenceSprite.bounds.size;   // 월드 단위(스프라이트 rect / PPU)
        Vector2 refCenter = center + referenceOffset;

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireCube(refCenter, refSize);

        if (showLabels)
            Handles.Label(refCenter - new Vector2(0f, refSize.y * 0.5f + 0.3f),
                $"{referenceSprite.name}  {refSize.x:0.##} × {refSize.y:0.##}");
    }
#endif
}
