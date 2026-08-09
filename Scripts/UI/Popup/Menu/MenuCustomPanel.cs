using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuCustomPanel : MonoBehaviour
{
    [Header("데이터")]
    [SerializeField] private SkinDatabaseSO database;
    [Tooltip("탭이 없으므로 표시할 스킨 카테고리를 고정으로 지정한다")]
    [SerializeField] private SkinCategory category = SkinCategory.Banana;

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
    private int _activePageCount;
    private SkinTabSO _currentTab;
    private SkinDataSO _selected;

    public void Initialize()
    {
        _service = Services.Get<ISkinService>();

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

        _selected = FindEquipped() ?? _currentTab.skins[0];

        BuildPages();
    }

    private void SetContentVisible(bool hasSkins)
    {
        if (pagedScroll != null) pagedScroll.gameObject.SetActive(hasSkins);
    }

    private SkinDataSO FindEquipped()
    {
        foreach (var skin in _currentTab.skins)
            if (_service.IsEquipped(_currentTab.category, skin))
                return skin;
        return null;
    }

    private void BuildPages()
    {
        var skins = _currentTab.skins;
        int perPage = pagePrefab.Capacity;
        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)skins.Count / perPage));

        // 페이지 풀링: 부족하면 생성, 남으면 비활성화. 각 페이지가 자기 셀을 세팅
        for (int p = 0; p < totalPages; p++)
        {
            var page = GetPage(p);
            page.gameObject.SetActive(true);
            page.Bind(skins, p * perPage, _service, _currentTab.category, _selected, OnCellSelected);
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
            _pagePool.Add(Instantiate(pagePrefab, content));

        return _pagePool[index];
    }

    private void UpdatePageUI()
    {
        int current = pagedScroll.CurrentPage;
        int total = pagedScroll.PageCount;

        if (pageText != null) pageText.text = $"{current + 1} / {total}";
        if (prevButton != null) prevButton.interactable = current > 0;
        if (nextButton != null) nextButton.interactable = current < total - 1;
    }

    private void OnPageChanged(int page)
    {
        UpdatePageUI();
    }

    private void OnCellSelected(SkinDataSO data)
    {
        if (_selected == data) return;

        _selected = data;

        // 셀 클릭 즉시 장착
        _service.Equip(_currentTab.category, data);

        for (int p = 0; p < _activePageCount; p++)
            _pagePool[p].SetSelected(data);
    }
}
