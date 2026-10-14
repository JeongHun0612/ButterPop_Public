using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class UIMenu : UIPopup
{
    private const float LeaderboardOpenCooldownSeconds = 1f;

    [Header("패널")]
    [SerializeField] private MenuStorePanel storePanel;
    [SerializeField] private MenuStageSelectPanel stagePanel;
    [SerializeField] private MenuCustomPanel customPanel;

    [Header("공통")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button leaderboardButton;

    [Header("음소거 토글")]
    [Tooltip("버튼의 Target Graphic 스프라이트를 갈아 끼워 상태를 표시한다")]
    [SerializeField] private Button muteToggleButton;
    [SerializeField] private Sprite muteOnSprite;
    [SerializeField] private Sprite muteOffSprite;

    // OnHide에서 확정한 장착 변경 여부. 닫힌 뒤 스테이지를 다시 지을지 판단하는 데 쓴다.
    private bool _skinChanged;
    private ILeaderboardService _leaderboard;
    private float _lastLeaderboardOpenTime = float.NegativeInfinity;

    public override void Initialize()
    {
        base.Initialize();

        if (storePanel != null) storePanel.Initialize();
        if (stagePanel != null) stagePanel.Initialize(OnClickBack);
        if (customPanel != null) customPanel.Initialize();

        if (closeButton != null) closeButton.OnClickWithSound(OnClickBack);
        if (leaderboardButton != null) leaderboardButton.OnClickWithSound(OnClickLeaderboard);
        if (muteToggleButton != null) muteToggleButton.onClick.AddListener(OnClickMuteToggle);

        CacheLeaderboard();
    }

    public override void OnBeforeShow()
    {
        CacheLeaderboard();
        RefreshLeaderboardButton();
    }

    protected override UniTask OnShow()
    {
        if (storePanel != null) storePanel.Refresh();
        if (stagePanel != null) stagePanel.Refresh();
        if (customPanel != null) customPanel.Refresh();

        RefreshMuteIcon();

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

    private void RefreshMuteIcon()
    {
        if (muteToggleButton == null || muteToggleButton.image == null) return;
        if (!SoundManager.TryGetInstance(out var sound)) return;

        var sprite = sound.IsMuted ? muteOnSprite : muteOffSprite;
        if (sprite != null) muteToggleButton.image.sprite = sprite;
    }

    private void CacheLeaderboard()
    {
        if (_leaderboard == null) Services.TryGet(out _leaderboard);
    }

    private void RefreshLeaderboardButton()
    {
        if (leaderboardButton == null) return;
        leaderboardButton.gameObject.SetActive(_leaderboard != null && _leaderboard.IsAvailable);
    }

    // 메뉴를 닫지 않아 플랫폼 순위 화면이 떠 있는 동안에도 판정 정지가 이어진다.
    private void OnClickLeaderboard()
    {
        if (_leaderboard == null) return;

        // 순위 화면이 게임을 덮기 전의 연타로 화면이 겹쳐 열리지 않게 한다.
        float now = Time.realtimeSinceStartup;
        if (now - _lastLeaderboardOpenTime < LeaderboardOpenCooldownSeconds) return;
        _lastLeaderboardOpenTime = now;

        _leaderboard.ShowLeaderboard();
    }

    private void OnClickMuteToggle()
    {
        if (!SoundManager.TryGetInstance(out var sound)) return;

        sound.ToggleMute();
        RefreshMuteIcon();

        // 토글 뒤에 재생한다.
        sound.PlaySfx(SfxType.ButtonClick);
    }
}
