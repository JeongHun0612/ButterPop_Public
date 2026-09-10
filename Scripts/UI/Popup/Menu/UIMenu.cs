using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class UIMenu : UIPopup
{
    [Header("패널")]
    [SerializeField] private MenuStorePanel storePanel;
    [SerializeField] private MenuStageSelectPanel stagePanel;
    [SerializeField] private MenuCustomPanel customPanel;

    [Header("공통")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button leaderboardButton;

    // OnHide에서 확정한 장착 변경 여부. 닫힌 뒤 스테이지를 다시 지을지 판단하는 데 쓴다.
    private bool _skinChanged;

    public override void Initialize()
    {
        base.Initialize();

        if (storePanel != null) storePanel.Initialize();
        if (stagePanel != null) stagePanel.Initialize(OnClickBack);
        if (customPanel != null) customPanel.Initialize();

        if (closeButton != null) closeButton.OnClickWithSound(OnClickBack);
        if (leaderboardButton != null) leaderboardButton.OnClickWithSound(OnClickLeaderboard);
    }

    protected override UniTask OnShow()
    {
        if (storePanel != null) storePanel.Refresh();
        if (stagePanel != null) stagePanel.Refresh();
        if (customPanel != null) customPanel.Refresh();

        // 로그인이 끝나야 순위 화면을 띄울 수 있다. 메뉴를 열 때마다 다시 판단한다.
        if (leaderboardButton != null)
            leaderboardButton.gameObject.SetActive(
                Services.TryGet<ILeaderboardService>(out var leaderboard) && leaderboard.IsAvailable);

        return UniTask.CompletedTask;
    }

    // 닫기 버튼·딤·스테이지 셀 클릭 등 모든 닫기 경로가 Hide를 거치므로 여기서 커스텀 탭 선택을 확정한다.
    protected override UniTask OnHide()
    {
        _skinChanged = customPanel != null && customPanel.Commit();
        return UniTask.CompletedTask;
    }

    public override void OnClickBack() => CloseAsync().Forget();

    // 스테이지 확정은 메뉴가 완전히 닫힌 뒤에 한다. 닫히는 도중에 스테이지가 갈리면 연출이 겹친다.
    private async UniTaskVoid CloseAsync()
    {
        var uiManager = UIManager.Instance;
        Debug.Assert(uiManager != null, "[UIMenu] UIManager가 없습니다");

        await uiManager.HidePopup(this);

        bool stageLoaded = stagePanel != null && stagePanel.ConfirmCurrent();

        // 스테이지가 그대로라도 스킨이 바뀌었으면 다시 지어야 새 스킨이 반영된다.
        if (!stageLoaded && _skinChanged && StageManager.TryGetInstance(out var stageManager))
            stageManager.LoadStage(stageManager.CurrentStageIndex);
    }

    // 순위 화면은 Play Games가 게임 위에 직접 그리므로 메뉴를 닫지 않는다.
    private void OnClickLeaderboard()
    {
        if (Services.TryGet<ILeaderboardService>(out var leaderboard)) leaderboard.ShowLeaderboard();
    }
}
