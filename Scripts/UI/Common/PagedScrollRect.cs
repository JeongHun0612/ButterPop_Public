using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ScrollRect 위에 페이지 스냅 기능을 얹는다.
/// 스와이프/드래그로 넘기거나, MoveTo(page)로 이동할 수 있다.
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class PagedScrollRect : MonoBehaviour, IEndDragHandler, IBeginDragHandler
{
    [SerializeField] private float snapDuration = 0.25f;
    [SerializeField] private Ease snapEase = Ease.OutCubic;
    [SerializeField] private float swipeThreshold = 0.1f; // 뷰포트 대비 비율

    private ScrollRect _scrollRect;
    private int _pageCount;
    private int _currentPage;
    private Tweener _snapTween;

    public int CurrentPage => _currentPage;
    public int PageCount => _pageCount;

    public event Action<int> OnPageChanged;

    private void Awake()
    {
        _scrollRect = GetComponent<ScrollRect>();
    }

    public void SetPageCount(int count)
    {
        _pageCount = Mathf.Max(1, count);
        _currentPage = 0;
        SnapToPage(0, true);
    }

    public void MoveTo(int page, bool instant = false)
    {
        page = Mathf.Clamp(page, 0, _pageCount - 1);
        if (page == _currentPage && !instant) return;
        SnapToPage(page, instant);
    }

    public void MoveNext() => MoveTo(_currentPage + 1);
    public void MovePrev() => MoveTo(_currentPage - 1);

    public void OnBeginDrag(PointerEventData eventData)
    {
        _snapTween?.Kill();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_pageCount <= 1)
        {
            SnapToPage(0, false);
            return;
        }

        float pos = _scrollRect.horizontalNormalizedPosition;
        float pageSize = 1f / (_pageCount - 1);
        float currentPagePos = _currentPage * pageSize;
        float delta = pos - currentPagePos;

        int targetPage = _currentPage;
        if (delta > swipeThreshold * pageSize)
            targetPage = _currentPage + 1;
        else if (delta < -swipeThreshold * pageSize)
            targetPage = _currentPage - 1;

        SnapToPage(Mathf.Clamp(targetPage, 0, _pageCount - 1), false);
    }

    private void SnapToPage(int page, bool instant)
    {
        _currentPage = page;

        float target = _pageCount <= 1 ? 0f : (float)page / (_pageCount - 1);

        _snapTween?.Kill();

        if (instant)
        {
            _scrollRect.horizontalNormalizedPosition = target;
        }
        else
        {
            _snapTween = DOTween
                .To(() => _scrollRect.horizontalNormalizedPosition,
                    x => _scrollRect.horizontalNormalizedPosition = x,
                    target, snapDuration)
                .SetEase(snapEase);
        }

        OnPageChanged?.Invoke(_currentPage);
    }
}
