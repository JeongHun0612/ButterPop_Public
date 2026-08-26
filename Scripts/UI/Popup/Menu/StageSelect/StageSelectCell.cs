using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>스테이지 선택 휠의 셀 1칸. 표시와 포커스 연출만 담당한다.</summary>
[RequireComponent(typeof(CanvasGroup))]
public class StageSelectCell : MonoBehaviour, IPointerClickHandler
{
    [Header("표시")]
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Image background;

    private RectTransform _rect;
    private CanvasGroup _canvasGroup;
    private int _index;
    private Action<int> _onClick;

    public int Index => _index;
    public RectTransform Rect => _rect != null ? _rect : (_rect = (RectTransform)transform);

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Bind(int index, string text)
    {
        _index = index;
        if (label != null) label.text = text;
    }

    public void SetClickCallback(Action<int> onClick) => _onClick = onClick;

    public void SetSelected(bool selected)
    {
        if (background != null) background.enabled = selected;
    }

    public void ApplyFocus(float scale, float alpha)
    {
        Rect.localScale = new Vector3(scale, scale, 1f);

        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        _canvasGroup.alpha = alpha;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _onClick?.Invoke(_index);
    }
}
