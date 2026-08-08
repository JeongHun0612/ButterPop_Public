using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 스테이지 선택 팝업. 휠 스크롤로 스테이지를 고르고 닫으면 해당 스테이지로 전환한다.
/// </summary>
public class UIStageSelect : UIPopup
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
    [SerializeField] private int visibleRange = 3;

    [Header("옵션")]
    [Tooltip("미해금 스테이지를 ???로 표시하고, 선택 시 현재 스테이지를 리로드한다")]
    [SerializeField] private bool lockUnrevealed = true;

    private readonly List<StageSelectCell> _cellPool = new();
    private int _activeCellCount;


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

    public override void Initialize()
    {
        base.Initialize();

        var stageManager = StageManager.Instance;
        Debug.Assert(stageManager != null, "[UIStageSelect] StageManager가 없습니다");

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

        Canvas.ForceUpdateCanvases();
        float cellHeight = _cellPool[0].Rect.rect.height;
        float cellWidth = _cellPool[0].Rect.rect.width;

        var scrollRT = (RectTransform)wheel.transform;
        scrollRT.anchorMin = new Vector2(0.5f, 0.5f);
        scrollRT.anchorMax = new Vector2(0.5f, 0.5f);
        scrollRT.pivot = new Vector2(0.5f, 0.5f);
        scrollRT.anchoredPosition = Vector2.zero;
        scrollRT.sizeDelta = new Vector2(cellWidth * centerScale, wheel.CalculateViewportHeight(visibleRange, cellHeight));

        Canvas.ForceUpdateCanvases();
        wheel.Configure(count, cellHeight);

        for (int i = 0; i < count; i++)
            _cellPool[i].Rect.anchoredPosition = new Vector2(0f, wheel.GetItemY(i));
    }

    protected override UniTask OnShow()
    {
        Refresh();
        return UniTask.CompletedTask;
    }

    public override void OnClickBack()
    {
        ConfirmAndCloseAsync(wheel.CurrentIndex).Forget();
    }

    private void OnCellClicked(int index)
    {
        if (SoundManager.TryGetInstance(out var sound))
            sound.PlaySfx(SfxType.ButtonClick);

        ConfirmAndCloseAsync(index).Forget();
    }

    private void Refresh()
    {
        var stageManager = StageManager.Instance;
        Debug.Assert(stageManager != null, "[UIStageSelect] StageManager가 없습니다");

        for (int i = 0; i < _activeCellCount; i++)
            _cellPool[i].Bind(i, BuildLabel(i, stageManager));

        int start = Mathf.Clamp(stageManager.CurrentStageIndex, 0, _activeCellCount - 1);
        wheel.MoveTo(start, instant: true);

        LateUpdate();
    }

    private string BuildLabel(int index, StageManager stageManager)
    {
        bool revealed = !lockUnrevealed || index <= stageManager.GetHighestClearedIndex() + 1;

        if (!revealed) return $"#{index + 1} ???";

        var data = stageManager.GetStageData(index);
        string name = data != null ? data.stageName : string.Empty;
        return $"#{index + 1} {name}".TrimEnd();
    }

    private async UniTaskVoid ConfirmAndCloseAsync(int index)
    {
        var stageManager = StageManager.Instance;
        Debug.Assert(stageManager != null, "[UIStageSelect] StageManager가 없습니다");

        bool playable = !lockUnrevealed || index <= stageManager.GetHighestClearedIndex() + 1;
        int loadIndex = playable ? index : stageManager.CurrentStageIndex;

        await UIManager.Instance.HidePopup();

        stageManager.LoadStage(loadIndex);
    }
}
