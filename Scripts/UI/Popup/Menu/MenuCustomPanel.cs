using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class MenuCustomPanel : MonoBehaviour
{
    [Header("데이터")]
    [SerializeField] private SkinDatabaseSO database;
    [Tooltip("탭이 없으므로 표시할 스킨 카테고리를 고정으로 지정한다")]
    [SerializeField] private SkinCategory category = SkinCategory.Squish;

    [Header("그리드")]
    [SerializeField] private PagedScrollRect pagedScroll;
    [SerializeField] private RectTransform content;   // 페이지들의 부모 (HorizontalLayoutGroup)
    [SerializeField] private SkinPage pagePrefab;     // 페이지 프리팹 (부족할 때 생성용)

    [Header("페이지")]
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TextMeshProUGUI pageText;

    private ISkinService _service;
    private readonly List<SkinPage> _pagePool = new();
    private ScrollRect _scrollRect;
    private float _pageWidth;
    private bool _isResizingPages;
    private int _activePageCount;
    private SkinTabSO _currentTab;
    private SkinDataSO _selected;

    private void OnEnable()
    {
        if (Application.isPlaying) return;

        ResizePagesToViewport();
    }

    private void OnRectTransformDimensionsChange()
    {
        ResizePagesToViewport();
    }

    public void Initialize()
    {
        _service = Services.Get<ISkinService>();
        if (pagedScroll != null) _scrollRect = pagedScroll.GetComponent<ScrollRect>();

        if (prevButton != null) prevButton.OnClickWithSound(() => pagedScroll.MovePrev());
        if (nextButton != null) nextButton.OnClickWithSound(() => pagedScroll.MoveNext());

        pagedScroll.OnPageChanged += OnPageChanged;

        // Content 하위에 미리 배치된 페이지들을 풀에 등록
        for (int i = 0; i < content.childCount; i++)
        {
            var page = content.GetChild(i).GetComponent<SkinPage>();
            if (page != null)
            {
                _pagePool.Add(page);
                page.gameObject.SetActive(false);
            }
        }
    }

    public void Refresh()
    {
        _currentTab = database.GetTab(category);

        // 해당 카테고리에 스킨 데이터가 없으면 안내 문구만 노출하고 그리드는 숨긴다.
        bool hasSkins = _currentTab != null && _currentTab.Count > 0;
        SetContentVisible(hasSkins);

        if (!hasSkins)
        {
            _selected = null;
            return;
        }

        _selected = _service.ResolveEquipped(_currentTab);

        ResizePagesToViewport();
        BuildPages();
    }

    // 메뉴가 닫힐 때 최종 선택을 실제 장착으로 확정한다. 장착이 실제로 바뀌었으면 true.
    public bool Commit()
    {
        if (_currentTab == null || _selected == null) return false;
        if (_service.IsEquipped(_currentTab.category, _selected)) return false;
        if (!_service.IsOwned(_currentTab, _selected)) return false;

        _service.Equip(_currentTab.category, _selected);
        return true;
    }

    private void SetContentVisible(bool hasSkins)
    {
        if (pagedScroll != null) pagedScroll.gameObject.SetActive(hasSkins);
    }

    private void BuildPages()
    {
        var skins = _currentTab.skins;
        int perPage = pagePrefab.Capacity;
        if (perPage <= 0)
        {
            Debug.LogError("[MenuCustomPanel] SkinPage에 표시 가능한 셀이 없습니다");
            return;
        }

        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)skins.Count / perPage));

        // 페이지 풀링: 부족하면 생성, 남으면 비활성화. 각 페이지가 자기 셀을 세팅
        for (int p = 0; p < totalPages; p++)
        {
            var page = GetPage(p);
            page.gameObject.SetActive(true);
            page.Bind(skins, p * perPage, _service, _currentTab, _selected, OnCellSelected);
        }

        for (int p = totalPages; p < _activePageCount; p++)
            _pagePool[p].gameObject.SetActive(false);

        _activePageCount = totalPages;

        pagedScroll.SetPageCount(totalPages);
        UpdatePageUI();
    }

    private SkinPage GetPage(int index)
    {
        while (_pagePool.Count <= index)
        {
            var page = Instantiate(pagePrefab, content);
            ApplyPageWidth(page);
            _pagePool.Add(page);
        }

        return _pagePool[index];
    }

    private void ResizePagesToViewport()
    {
        if (_isResizingPages || pagedScroll == null || content == null) return;

        if (_scrollRect == null) _scrollRect = pagedScroll.GetComponent<ScrollRect>();
        if (_scrollRect == null || _scrollRect.viewport == null) return;

        _isResizingPages = true;
        Canvas.ForceUpdateCanvases();

        float viewportWidth = _scrollRect.viewport.rect.width;
        if (viewportWidth <= 0)
        {
            _isResizingPages = false;
            return;
        }

        _pageWidth = viewportWidth;

        for (int i = 0; i < content.childCount; i++)
        {
            var page = content.GetChild(i).GetComponent<SkinPage>();
            ApplyPageWidth(page);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        if (_activePageCount > 0)
            pagedScroll.MoveTo(pagedScroll.CurrentPage, true);

        _isResizingPages = false;
    }

    private void ApplyPageWidth(SkinPage page)
    {
        if (page == null || _pageWidth <= 0) return;

        page.SetWidth(_pageWidth);
    }

    private void UpdatePageUI()
    {
        int current = pagedScroll.CurrentPage;
        int total = pagedScroll.PageCount;
        bool hasMultiplePages = total > 1;

        if (pageText != null)
        {
            pageText.gameObject.SetActive(hasMultiplePages);
            pageText.text = $"{current + 1} / {total}";
        }

        if (prevButton != null)
        {
            prevButton.gameObject.SetActive(hasMultiplePages);
            prevButton.interactable = hasMultiplePages && current > 0;
        }

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(hasMultiplePages);
            nextButton.interactable = hasMultiplePages && current < total - 1;
        }

        pagedScroll.enabled = hasMultiplePages;

        if (_scrollRect != null)
        {
            _scrollRect.horizontal = hasMultiplePages;
            if (!hasMultiplePages) _scrollRect.velocity = Vector2.zero;
        }
    }

    private void OnPageChanged(int page)
    {
        UpdatePageUI();
    }

    private void OnCellSelected(SkinDataSO data)
    {
        if (_selected == data) return;

        _selected = data;

        // 클릭은 로컬 선택만 갱신한다. 실제 장착(저장)은 메뉴가 닫힐 때 Commit()에서 1회 처리.
        for (int p = 0; p < _activePageCount; p++)
            _pagePool[p].SetSelected(data);
    }
}
