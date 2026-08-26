using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 페이지 1장. 미리 배치된 셀 N개를 들고, 데이터 슬라이스를 받아 스스로 세팅한다.
/// </summary>
public class SkinPage : MonoBehaviour
{
    [SerializeField] private SkinCell[] cells;

    public int Capacity => cells.Length;

    /// <summary>
    /// skins의 startIndex부터 셀 개수만큼 바인딩한다. 데이터가 없는 셀은 숨긴다.
    /// </summary>
    public void Bind(
        IReadOnlyList<SkinDataSO> skins,
        int startIndex,
        ISkinService service,
        SkinCategory category,
        SkinDataSO selected,
        Action<SkinDataSO> onSelect)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            int dataIndex = startIndex + i;

            if (dataIndex < skins.Count)
            {
                var data = skins[dataIndex];
                cells[i].Bind(
                    data,
                    service.IsOwned(data),
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
}
