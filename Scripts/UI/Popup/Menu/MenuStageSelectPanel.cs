using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class MenuStageSelectPanel : MonoBehaviour
{
    [Header("휠 스크롤")]
    [SerializeField] private StageWheelScroll wheel;
    [SerializeField] private RectTransform content;   // 셀들의 부모 (top-center 앵커)
    [SerializeField] private StageSelectCell cellPrefab;

    [Header("포커스 연출")]
    [SerializeField] private float centerScale = 1.6f;   // 정중앙 셀 배율
    [SerializeField] private float sideScale = 1f;       // 나머지 셀 배율
    [Tooltip("중앙 기준 위/아래로 보이는 셀 수 (그 바깥은 숨김)")]
    [SerializeField] private int visibleRange = 2;

    [Header("옵션")]
    [Tooltip("미해금 스테이지를 ???로 표시하고, 선택 시 현재 스테이지를 리로드한다")]
    [SerializeField] private bool lockUnrevealed = true;

    private readonly List<StageSelectCell> _cellPool = new();
    private int _activeCellCount;
    private bool _layoutReady;
    private Action _requestClose;
    private string[] _localizedNames;   // 런타임 언어 변경이 없어 1회만 채우는 스테이지 이름 캐시

    private void LateUpdate()
    {
        if (_activeCellCount == 0) return;

        int nearest = 0;
        float minDist = float.MaxValue;

        for (int i = 0; i < _activeCellCount; i++)
        {
            float dist = wheel.GetFocusDistance(i);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = i;
            }
        }

        for (int i = 0; i < _activeCellCount; i++)
        {
            float dist = wheel.GetFocusDistance(i);
            _cellPool[i].ApplyFocus(Mathf.Lerp(centerScale, sideScale, Mathf.Clamp01(dist)));
            _cellPool[i].SetVisible(dist <= visibleRange);
            _cellPool[i].SetSelected(i == nearest);
        }
    }

    /// <param name="requestClose">셀 클릭 시 메뉴에 닫기를 요청하는 콜백. 스테이지 확정은 메뉴가 주도한다.</param>
    public void Initialize(Action requestClose)
    {
        _requestClose = requestClose;
        var stageManager = StageManager.Instance;
        Debug.Assert(stageManager != null, "[MenuStageSelectPanel] StageManager가 없습니다");

        int count = stageManager.StageCount;
        if (count == 0) return;

        for (int i = 0; i < count; i++)
        {
            var cell = Instantiate(cellPrefab, content);
            cell.Rect.anchorMin = new Vector2(0f, 1f);
            cell.Rect.anchorMax = new Vector2(1f, 1f);
            cell.Rect.pivot = new Vector2(0.5f, 0.5f);
            cell.SetClickCallback(OnCellClicked);
            _cellPool.Add(cell);
        }

        _activeCellCount = count;

        PreloadNamesAsync(stageManager).Forget();
    }

    public void Refresh()
    {
        if (_activeCellCount == 0) return;

        // 레이아웃 측정은 패널이 활성화되어 뷰포트 크기가 확정된 뒤(최초 Refresh) 1회 수행한다.
        if (!_layoutReady)
        {
            BuildLayout();
            _layoutReady = true;
        }

        var stageManager = StageManager.Instance;
        Debug.Assert(stageManager != null, "[MenuStageSelectPanel] StageManager가 없습니다");

        // 해금 여부는 진행도를 따라 달라지므로 열 때마다 다시 판정한다.
        for (int i = 0; i < _activeCellCount; i++)
        {
            bool revealed = !lockUnrevealed || i <= stageManager.GetHighestClearedIndex() + 1;
            _cellPool[i].Bind(i, BuildLabel(i, revealed, GetCachedName(stageManager, i)));
        }

        int start = Mathf.Clamp(stageManager.CurrentStageIndex, 0, _activeCellCount - 1);
        wheel.MoveTo(start, instant: true);

        LateUpdate();
    }

    /// <summary>휠 중앙 스테이지가 현재와 다르면 로드하고 true를 반환한다. 미해금 스테이지를 골랐으면 현재 스테이지로 간주한다.</summary>
    public bool ConfirmCurrent()
    {
        if (_activeCellCount == 0) return false;

        var stageManager = StageManager.Instance;
        Debug.Assert(stageManager != null, "[MenuStageSelectPanel] StageManager가 없습니다");

        int index = wheel.CurrentIndex;
        bool playable = !lockUnrevealed || index <= stageManager.GetHighestClearedIndex() + 1;
        int loadIndex = playable ? index : stageManager.CurrentStageIndex;

        // 같은 스테이지면 다시 짓지 않는다. 재로드는 시도 횟수까지 초기화한다.
        if (loadIndex == stageManager.CurrentStageIndex) return false;

        stageManager.LoadStage(loadIndex);
        return true;
    }

    // 로컬라이즈 테이블은 통째로 로드되므로 미해금 스테이지 이름까지 한 번에 받아둔다.
    private async UniTaskVoid PreloadNamesAsync(StageManager stageManager)
    {
        var nameTasks = new UniTask<string>[_activeCellCount];

        for (int i = 0; i < _activeCellCount; i++)
        {
            var data = stageManager.GetStageData(i);
            nameTasks[i] = data != null ? GetLocalizedNameAsync(data) : UniTask.FromResult(string.Empty);
        }

        _localizedNames = await UniTask.WhenAll(nameTasks);

        // 로드가 메뉴 오픈보다 늦었으면 라벨을 다시 채운다. 그 사이 패널이 파괴됐을 수 있다.
        if (this != null && isActiveAndEnabled) Refresh();
    }

    // 뷰포트 크기가 확정된 활성 상태에서 호출해야 셀이 정확히 뷰포트 중앙에 정렬된다.
    private void BuildLayout()
    {
        Canvas.ForceUpdateCanvases();

        float cellHeight = _cellPool[0].Rect.rect.height;
        wheel.Configure(_activeCellCount, cellHeight);

        for (int i = 0; i < _activeCellCount; i++)
            _cellPool[i].Rect.anchoredPosition = new Vector2(0f, wheel.GetItemY(i));
    }

    // 캐시가 아직 없으면 stageId로 대신한다.
    private string GetCachedName(StageManager stageManager, int index)
    {
        if (_localizedNames != null) return _localizedNames[index];

        var data = stageManager.GetStageData(index);
        return data != null ? data.stageId : string.Empty;
    }

    private static string BuildLabel(int index, bool revealed, string localizedName)
    {
        if (!revealed) return $"#{index + 1} ???";

        return $"#{index + 1} {localizedName}".TrimEnd();
    }

    private static async UniTask<string> GetLocalizedNameAsync(StageDataSO data)
    {
        if (data.LocalizedName == null || data.LocalizedName.IsEmpty)
            return data.stageId;

        return await data.LocalizedName.GetLocalizedStringAsync();
    }

    // 클릭한 셀로 휠을 옮겨 의사를 남기고 닫기를 요청한다. 실제 로드는 메뉴가 닫힌 뒤 ConfirmCurrent에서 처리된다.
    private void OnCellClicked(int index)
    {
        if (SoundManager.TryGetInstance(out var sound))
            sound.PlaySfx(SfxType.ButtonClick);

        wheel.MoveTo(index, instant: true);
        _requestClose?.Invoke();
    }
}
