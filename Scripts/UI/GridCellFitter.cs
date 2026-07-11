using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GridLayoutGroup))]
public class GridCellFitter : MonoBehaviour
{
    [SerializeField] private int columnCount = 3;
    [SerializeField] private int rowCount = 2;

    public void FitCellSize()
    {
        var grid = GetComponent<GridLayoutGroup>();
        var rectTransform = GetComponent<RectTransform>();

        if (grid == null || rectTransform == null) return;
        if (columnCount <= 0 || rowCount <= 0) return;

        Canvas.ForceUpdateCanvases();

        var rect = rectTransform.rect;
        if (rect.width <= 0 || rect.height <= 0) return;

        var padding = grid.padding;
        var spacing = grid.spacing;

        float availableWidth = rect.width - padding.left - padding.right - spacing.x * (columnCount - 1);
        float availableHeight = rect.height - padding.top - padding.bottom - spacing.y * (rowCount - 1);

        grid.cellSize = new Vector2(availableWidth / columnCount, availableHeight / rowCount);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columnCount;
    }
}
