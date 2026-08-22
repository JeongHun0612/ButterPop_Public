using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스킨 커스텀 팝업.
/// ScrollRect + HorizontalLayout 기반 슬라이드 페이지네이션.
/// 탭 전환 시 페이지를 재구성하고, 스와이프/버튼으로 넘긴다.
/// </summary>
public class UICustom : UIPopup
{
    [Header("데이터")]
    [SerializeField] private SkinDatabaseSO database;

    [Header("탭")]
    [SerializeField] private TabButton[] tabButtons;
    [SerializeField] private TextMeshProUGUI ownedCountText;   // "Owned 8/20"

    [Header("그리드")]
    [SerializeField] private PagedScrollRect pagedScroll;
    [SerializeField] private RectTransform content;   // 페이지들의 부모 (HorizontalLayoutGroup)
    [SerializeField] private SkinPage pagePrefab;     // 페이지 프리팹 (부족할 때 생성용)
    [SerializeField] private GameObject emptyPlaceholder;   // "콘텐츠 준비중" 안내 (스킨 데이터가 없을 때)

    [Header("페이지")]
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TextMeshProUGUI pageText;

    [Header("미리보기")]
    [SerializeField] private Image previewIcon;
    [SerializeField] private TextMeshProUGUI previewName;

    [Header("하단 버튼")]
    [SerializeField] private Button saveButton;
    [SerializeField] private Button gachaButton;
    [SerializeField] private Button closeButton;

    private ISkinService _service;
    private readonly List<SkinPage> _pagePool = new();

    private int _activePageCount;

    private SkinTabSO _currentTab;
    private SkinDataSO _selected;

    public override void Initialize()
    {
        base.Initialize();

        _service = Services.Get<ISkinService>();

        foreach (var tab in tabButtons)
        {
            var captured = tab.category;
            tab.button.OnClickWithSound(() => SelectTab(captured));
        }

        if (prevButton != null) prevButton.OnClickWithSound(() => pagedScroll.MovePrev());
        if (nextButton != null) nextButton.OnClickWithSound(() => pagedScroll.MoveNext());

        pagedScroll.OnPageChanged += OnPageChanged;

        if (saveButton != null) saveButton.OnClickWithSound(OnSave);
        if (gachaButton != null) gachaButton.OnClickWithSound(OnGacha);
        if (closeButton != null) closeButton.OnClickWithSound(OnClickBack);

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

    protected override UniTask OnShow()
    {
        var first = tabButtons.Length > 0 ? tabButtons[0].category : SkinCategory.Squish;
        SelectTab(first);
        return UniTask.CompletedTask;
    }

    protected override UniTask OnHide()
    {
        return UniTask.CompletedTask;
    }

    // ---------------- 탭 ----------------

    private void SelectTab(SkinCategory category)
    {
        if (_currentTab != null && _currentTab.category == category) return;

        _currentTab = database.GetTab(category);

        UpdateTabVisual(category);
        RefreshOwnedCount();

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
        ShowPreviewPanel(_selected);
    }

    private void SetContentVisible(bool hasSkins)
    {
        if (pagedScroll != null) pagedScroll.gameObject.SetActive(hasSkins);
        if (emptyPlaceholder != null) emptyPlaceholder.SetActive(!hasSkins);
    }

    private void UpdateTabVisual(SkinCategory selected)
    {
        foreach (var tab in tabButtons)
            tab.SetSelected(tab.category == selected);
    }

    private SkinDataSO FindEquipped()
    {
        foreach (var skin in _currentTab.skins)
            if (_service.IsEquipped(_currentTab.category, skin))
                return skin;
        return null;
    }

    private void RefreshOwnedCount()
    {
        if (ownedCountText == null) return;

        int owned = 0;
        if (_currentTab != null)
            foreach (var skin in _currentTab.skins)
                if (_service.IsOwned(skin)) owned++;

        int maxCount = _currentTab?.Count ?? 0;
        ownedCountText.text = $"보유 {owned}/{maxCount}";
    }

    // ---------------- 페이지 구성 ----------------

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

    // ---------------- 페이지 네비게이션 ----------------

    private void OnPageChanged(int page)
    {
        UpdatePageUI();
    }

    private void UpdatePageUI()
    {
        int current = pagedScroll.CurrentPage;
        int total = pagedScroll.PageCount;

        if (pageText != null) pageText.text = $"{current + 1} / {total}";
        if (prevButton != null) prevButton.interactable = current > 0;
        if (nextButton != null) nextButton.interactable = current < total - 1;
    }

    // ---------------- 선택 / 미리보기 ----------------

    private void OnCellSelected(SkinDataSO data)
    {
        if (_selected == data) return;

        _selected = data;

        ShowPreviewPanel(data);

        for (int p = 0; p < _activePageCount; p++)
            _pagePool[p].SetSelected(data);
    }

    private void ShowPreviewPanel(SkinDataSO data)
    {
        if (previewIcon != null) previewIcon.sprite = data.icon;
        if (previewName != null) previewName.text = data.displayName;
    }

    // ---------------- 하단 버튼 ----------------

    private void OnSave()
    {
        if (_selected != null)
            _service.Equip(_currentTab.category, _selected);

        BuildPages();
    }

    private void OnGacha()
    {
        // TODO(가챠 연동): 뽑기 로직 후 소유 상태 갱신
    }

    [Serializable]
    private class TabButton
    {
        public SkinCategory category;
        public Button button;
        public GameObject selectedMark;

        public void SetSelected(bool selected)
        {
            if (selectedMark != null) selectedMark.SetActive(selected);
        }
    }
}

