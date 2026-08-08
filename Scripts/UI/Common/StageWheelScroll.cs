using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 세로 휠 피커 스냅. 플릭 관성 후 가장 가까운 아이템을 정중앙에 스냅한다.
/// Content는 anchor·pivot이 top-center여야 하며, 셀 배치는 GetItemY(index)로 외부에서 한다.
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class StageWheelScroll : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [Header("스냅")]
    [SerializeField] private float itemSpacing = 40f;   // 아이템 사이 여백(중심-중심 간격 = 셀높이 + 이 값)
    [SerializeField] private float snapDuration = 0.25f;
    [SerializeField] private Ease snapEase = Ease.OutCubic;

    [Tooltip("관성 속도가 이 값 이하로 떨어지면 스냅을 시작한다")]
    [SerializeField] private float settleVelocity = 60f;

    private const float SnapEpsilon = 0.5f;

    private ScrollRect _scroll;
    private RectTransform _content;
    private RectTransform _viewport;

    private float _pitch;      // 아이템 중심 간 간격
    private float _padTop;     // 첫/마지막 아이템이 중앙에 오도록 하는 상하 여백
    private float _cellHeight;
    private int _count;
    private int _currentIndex;

    private bool _dragging;
    private bool _snapping;
    private Tweener _snapTween;

    public int CurrentIndex => _currentIndex;
    public int Count => _count;

    public event Action<int> OnIndexChanged;

    private void Awake()
    {
        _scroll = GetComponent<ScrollRect>();
        _content = _scroll.content;
        _viewport = _scroll.viewport != null ? _scroll.viewport : (RectTransform)_scroll.transform;
    }

    private void Update()
    {
        if (_count == 0 || _dragging || _snapping) return;
        if (Mathf.Abs(_scroll.velocity.y) > settleVelocity) return;

        int nearest = NearestIndex();
        if (Mathf.Abs(GetContentY() - nearest * _pitch) > SnapEpsilon)
            MoveTo(nearest);
        else
            UpdateIndex(nearest);
    }

    public void Configure(int count, float cellHeight)
    {
        _content.anchorMin = new Vector2(0f, 1f);
        _content.anchorMax = new Vector2(1f, 1f);
        _content.pivot = new Vector2(0.5f, 1f);

        _count = Mathf.Max(0, count);
        _cellHeight = cellHeight;
        _pitch = cellHeight + itemSpacing;

        float viewportHeight = _viewport.rect.height;
        _padTop = (viewportHeight - cellHeight) * 0.5f;

        float contentHeight = viewportHeight + Mathf.Max(0, _count - 1) * _pitch;
        _content.sizeDelta = new Vector2(_content.sizeDelta.x, contentHeight);
    }

    public float CalculateViewportHeight(int visibleRange, float cellHeight)
    {
        float pitch = cellHeight + itemSpacing;
        return 2f * visibleRange * pitch + cellHeight;
    }

    public float GetItemY(int index) => -(_padTop + _cellHeight * 0.5f + index * _pitch);

    /// <summary>정중앙에서의 거리(아이템 단위). 포커스 연출용.</summary>
    public float GetFocusDistance(int index) => Mathf.Abs(GetContentY() - index * _pitch) / _pitch;

    public void MoveTo(int index, bool instant = false)
    {
        if (_count == 0) return;

        index = Mathf.Clamp(index, 0, _count - 1);
        float target = index * _pitch;

        _snapTween?.Kill();
        _scroll.velocity = Vector2.zero;

        if (instant)
        {
            SetContentY(target);
            _snapping = false;
            UpdateIndex(index);
            return;
        }

        _snapping = true;
        _snapTween = DOTween
            .To(GetContentY, SetContentY, target, snapDuration)
            .SetEase(snapEase)
            .OnUpdate(() => _scroll.velocity = Vector2.zero)
            .OnComplete(() =>
            {
                _snapping = false;
                UpdateIndex(index);
            });
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _snapTween?.Kill();
        _snapping = false;
        _dragging = true;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _dragging = false;
    }

    private int NearestIndex() =>
        Mathf.Clamp(Mathf.RoundToInt(GetContentY() / _pitch), 0, _count - 1);

    private void UpdateIndex(int index)
    {
        if (index == _currentIndex) return;

        _currentIndex = index;
        OnIndexChanged?.Invoke(index);
    }

    private float GetContentY() => _content.anchoredPosition.y;

    private void SetContentY(float y)
    {
        var pos = _content.anchoredPosition;
        pos.y = y;
        _content.anchoredPosition = pos;
    }
}
