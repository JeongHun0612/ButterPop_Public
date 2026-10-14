#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

// SquishBody 에디터 미리보기 기즈모. rest 샘플 기준으로 전체 형태·뼈 콜라이더 배치를 씬 뷰에 표시
public partial class SquishBody
{
    private void OnDrawGizmos()
    {
        if (length <= 0f || width <= 0f) return;

        Vector3 axis = holderAnchor != null ? Vector3.down : transform.right;
        Vector3 side = holderAnchor != null ? Vector3.right : transform.up;
        float halfLength = length * 0.5f;
        float halfWidth = width * 0.5f;
        Vector3 center = holderAnchor != null
            ? holderAnchor.position + axis * halfLength
            : transform.position;

        // 전체 형태(직사각형), 옅게
        DrawGizmoRect(center, axis, side, halfLength, halfWidth,
            new Color(1f, 0.95f, 0.25f, 0.06f),
            new Color(1f, 0.95f, 0.25f, 0.4f));

        // 뼈 콜라이더 배치 미리보기
        SpineSample[] rest = BuildRestSamples();
        float r = Mathf.Max(halfWidth, 0.05f);
        Color outline = new(0.25f, 0.9f, 1f, 0.9f);
        Color fill = new(0.25f, 0.9f, 1f, 0.12f);

        for (int i = 0; i < rest.Length; i++)
        {
            Vector3 p = center + axis * rest[i].position.x + side * rest[i].position.y;

            if (colliderShape == ColliderShape.Box)
            {
                DrawGizmoRect(p, axis, side, r, r, fill, outline);
            }
            else
            {
                Handles.color = fill;
                Handles.DrawSolidDisc(p, Vector3.forward, r);
                Handles.color = outline;
                Handles.DrawWireDisc(p, Vector3.forward, r);
            }
        }
    }

    private static void DrawGizmoRect(Vector3 center, Vector3 axis, Vector3 side,
        float halfAlong, float halfAcross, Color fill, Color outline)
    {
        Vector3 a = center - axis * halfAlong - side * halfAcross;
        Vector3 b = center + axis * halfAlong - side * halfAcross;
        Vector3 c = center + axis * halfAlong + side * halfAcross;
        Vector3 d = center - axis * halfAlong + side * halfAcross;

        Handles.DrawSolidRectangleWithOutline(new[] { a, b, c, d }, fill, outline);
    }
}
#endif
