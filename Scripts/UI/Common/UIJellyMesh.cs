using UnityEngine;
using UnityEngine.UI;

// 단순(Simple) 이미지를 가로줄 격자로 쪼개 바닥을 기준으로 눌리고 늘어나게 한다.
// 아래쪽은 더 퍼지고 몸통은 볼록해지며, 윗부분은 별도 값을 받아 바닥보다 늦게 따라오게 할 수 있다.
[RequireComponent(typeof(Graphic))]
public class UIJellyMesh : BaseMeshEffect
{
    private const int Columns = 4;
    private const int Rows = 16;

    [Header("변형 분포")]
    [Tooltip("눌릴 때 바닥이 옆으로 퍼지는 배율. 1이면 눌린 만큼만 넓어진다")]
    [SerializeField] private float bottomSpread = 1.4f;
    [Tooltip("눌릴 때 꼭대기가 옆으로 퍼지는 배율")]
    [SerializeField] private float topSpread = 0.8f;
    [Tooltip("눌릴 때 몸통 가운데가 추가로 볼록해지는 배율")]
    [SerializeField] private float bulge = 0.4f;

    private float _bottomSquash;
    private float _topSquash;

    // 양수면 납작하고 넓게 눌리고, 음수면 길쭉하고 좁게 늘어난다. 0.2면 높이가 80%가 된다.
    public void SetSquash(float bottom, float top)
    {
        if (Mathf.Approximately(bottom, _bottomSquash) && Mathf.Approximately(top, _topSquash)) return;

        _bottomSquash = bottom;
        _topSquash = top;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount != 4) return;

        var vertex = new UIVertex();
        vh.PopulateUIVertex(ref vertex, 0);
        Color32 color = vertex.color;
        Vector3 min = vertex.position;
        Vector3 max = vertex.position;
        Vector2 uvMin = vertex.uv0;
        Vector2 uvMax = vertex.uv0;
        for (int i = 1; i < 4; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            min = Vector3.Min(min, vertex.position);
            max = Vector3.Max(max, vertex.position);
            uvMin = Vector2.Min(uvMin, vertex.uv0);
            uvMax = Vector2.Max(uvMax, vertex.uv0);
        }

        vh.Clear();

        float centerX = (min.x + max.x) * 0.5f;
        float height = max.y - min.y;
        for (int row = 0; row <= Rows; row++)
        {
            float v = (float)row / Rows;
            float squash = Mathf.Lerp(_bottomSquash, _topSquash, v);
            // 높이마다 눌림이 달라서, 각 줄은 그 아래 구간이 눌린 양만큼 내려온다.
            float compressed = _bottomSquash * v + (_topSquash - _bottomSquash) * v * v * 0.5f;
            float y = min.y + height * (v - compressed);
            float widthScale = 1f + squash * (Mathf.Lerp(bottomSpread, topSpread, v) + bulge * Mathf.Sin(v * Mathf.PI));

            for (int col = 0; col <= Columns; col++)
            {
                float u = (float)col / Columns;
                float x = centerX + (Mathf.Lerp(min.x, max.x, u) - centerX) * widthScale;
                var uv = new Vector4(Mathf.Lerp(uvMin.x, uvMax.x, u), Mathf.Lerp(uvMin.y, uvMax.y, v), 0f, 0f);
                vh.AddVert(new Vector3(x, y, min.z), color, uv);
            }
        }

        const int stride = Columns + 1;
        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Columns; col++)
            {
                int bottomLeft = row * stride + col;
                int topLeft = bottomLeft + stride;
                vh.AddTriangle(bottomLeft, topLeft, topLeft + 1);
                vh.AddTriangle(topLeft + 1, bottomLeft + 1, bottomLeft);
            }
        }
    }
}
