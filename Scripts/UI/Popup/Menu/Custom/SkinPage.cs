using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 페이지 1장. 미리 배치된 셀 N개를 들고, 데이터 슬라이스를 받아 스스로 세팅한다.
/// </summary>
[ExecuteAlways]
public class SkinPage : MonoBehaviour
{
    [Header("레이아웃")]
    [SerializeField] private GridLayoutGroup grid;
    [SerializeField, Min(1)] private int columnCount = 3;
    [SerializeField, Min(1)] private int rowCount = 2;

    [Header("셀")]
    [SerializeField] private SkinCell[] cells;

    private RectTransform _rectTransform;
    private bool _isUpdatingLayout;

    public int Capacity => cells == null ? 0 : Mathf.Min(cells.Length, columnCount * rowCount);

    private void Awake()
    {
        CacheRectTransform();
    }

    private void OnEnable()
    {
        UpdateCellSize();
    }

    private void OnValidate()
    {
        columnCount = Mathf.Max(1, columnCount);
        rowCount = Mathf.Max(1, rowCount);

#if UNITY_EDITOR
        if (Application.isPlaying) return;

        UnityEditor.EditorApplication.delayCall -= RefreshEditorLayout;
        UnityEditor.EditorApplication.delayCall += RefreshEditorLayout;
#endif
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled) return;

        UpdateCellSize();
    }

    public void SetWidth(float width)
    {
        if (width <= 0) return;

        CacheRectTransform();
        if (_rectTransform == null) return;

        _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        UpdateCellSize();
    }

    /// <summary>
    /// skins의 startIndex부터 셀 개수만큼 바인딩한다. 데이터가 없는 셀은 숨긴다.
    /// </summary>
    public void Bind(
        IReadOnlyList<SkinDataSO> skins,
        int startIndex,
        ISkinService service,
        SkinTabSO tab,
        SkinDataSO selected,
        Action<SkinDataSO> onSelect)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            if (i >= Capacity)
            {
                cells[i].gameObject.SetActive(false);
                continue;
            }

            int dataIndex = startIndex + i;

            if (dataIndex < skins.Count)
            {
                var data = skins[dataIndex];
                cells[i].Bind(
                    data,
                    service.IsOwned(tab, data),
                    data == selected,
                    onSelect);
                cells[i].gameObject.SetActive(true);
            }
            else
            {
                cells[i].gameObject.SetActive(false);
            }
        }
    }

    public void SetSelected(SkinDataSO selected)
    {
        foreach (var cell in cells)
            if (cell.gameObject.activeSelf)
                cell.SetSelected(cell.Data == selected);
    }

    private void UpdateCellSize()
    {
        if (_isUpdatingLayout || grid == null || columnCount <= 0 || rowCount <= 0) return;

        _isUpdatingLayout = true;
        Canvas.ForceUpdateCanvases();

        var gridRect = grid.transform as RectTransform;
        if (gridRect == null)
        {
            _isUpdatingLayout = false;
            return;
        }

        var rect = gridRect.rect;

        if (rect.width > 0 && rect.height > 0)
        {
            var padding = grid.padding;
            var spacing = grid.spacing;

            float availableWidth = rect.width - padding.left - padding.right - spacing.x * (columnCount - 1);
            float availableHeight = rect.height - padding.top - padding.bottom - spacing.y * (rowCount - 1);

            grid.cellSize = new Vector2(availableWidth / columnCount, availableHeight / rowCount);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columnCount;
        }

        if (!Application.isPlaying && cells != null)
        {
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] != null)
                    cells[i].gameObject.SetActive(i < Capacity);
        }

        _isUpdatingLayout = false;
    }

#if UNITY_EDITOR
    private void RefreshEditorLayout()
    {
        if (this == null || Application.isPlaying) return;

        UpdateCellSize();
    }
#endif

    private void CacheRectTransform()
    {
        if (_rectTransform == null) _rectTransform = transform as RectTransform;
    }
}
