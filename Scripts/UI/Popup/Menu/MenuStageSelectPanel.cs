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
    [Tooltip("중앙에서 한 칸 멀어질 때 줄어드는 불투명도")]
    [SerializeField] private float alphaStep = 0.2f;
    [Tooltip("중앙 기준 위/아래로 보이는 셀 수 (그 바깥은 숨김)")]
    [SerializeField] private int visibleRange = 2;

    [Header("옵션")]
    [Tooltip("미해금 스테이지를 ???로 표시하고, 선택 시 현재 스테이지를 리로드한다")]
    [SerializeField] private bool lockUnrevealed = true;

    private readonly List<StageSelectCell> _cellPool = new();
    private int _activeCellCount;
    private bool _layoutReady;
    private Action _requestClose;
    private int _refreshVersion;

    private void LateUpdate()
    {
        if (_activeCellCount == 0) return;

        int nearest = 0;
        float minDist = float.MaxValue;

        for (int i = 0; i < _activeCellCount; i++)
        {
            float dist = wheel.GetFocusDistance(i);

            _cellPool[i].ApplyFocus(
                Mathf.Lerp(centerScale, sideScale, Mathf.Clamp01(dist)),
                dist > visibleRange ? 0f : Mathf.Clamp01(1f - alphaStep * dist));
            _cellPool[i].SetSelected(false);

            if (dist < minDist)
            {
                minDist = dist;
                nearest = i;
            }
        }

        _cellPool[nearest].SetSelected(true);
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
    }

    public void Refresh()
    {
        RefreshAsync().Forget();
    }

    private async UniTaskVoid RefreshAsync()
    {
        if (_activeCellCount == 0) return;
        int refreshVersion = ++_refreshVersion;

        // 레이아웃 측정은 패널이 활성화되어 뷰포트 크기가 확정된 뒤(최초 Refresh) 1회 수행한다.
        if (!_layoutReady)
        {
            BuildLayout();
            _layoutReady = true;
        }

        var stageManager = StageManager.Instance;
        Debug.Assert(stageManager != null, "[MenuStageSelectPanel] StageManager가 없습니다");

        var revealed = new bool[_activeCellCount];
        var nameTasks = new UniTask<string>[_activeCellCount];

        for (int i = 0; i < _activeCellCount; i++)
        {
            revealed[i] = !lockUnrevealed || i <= stageManager.GetHighestClearedIndex() + 1;
            var data = stageManager.GetStageData(i);
            nameTasks[i] = revealed[i] && data != null
                ? GetLocalizedNameAsync(data)
                : UniTask.FromResult(string.Empty);
        }

        string[] names = await UniTask.WhenAll(nameTasks);
        if (refreshVersion != _refreshVersion) return;

        for (int i = 0; i < _activeCellCount; i++)
            _cellPool[i].Bind(i, BuildLabel(i, revealed[i], names[i]));

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

    // 뷰포트 크기가 확정된 활성 상태에서 호출해야 셀이 정확히 뷰포트 중앙에 정렬된다.
    private void BuildLayout()
    {
        Canvas.ForceUpdateCanvases();

        float cellHeight = _cellPool[0].Rect.rect.height;
        wheel.Configure(_activeCellCount, cellHeight);

        for (int i = 0; i < _activeCellCount; i++)
            _cellPool[i].Rect.anchoredPosition = new Vector2(0f, wheel.GetItemY(i));
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
