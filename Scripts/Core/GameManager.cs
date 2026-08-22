using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameManager : MonoSingleton<GameManager>
{
    protected override void Awake()
    {
        base.Awake();
        Application.targetFrameRate = 60;
    }

    private async void Start()
    {
        await InitializeAsync();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) FlushSaveAsync().Forget();
    }

    private async UniTask InitializeAsync()
    {
        var uiManager = UIManager.Instance;
        Debug.Assert(uiManager != null, "[GameManager] UIManager가 없습니다");

        uiManager.ShowInitialLoading().Forget();

        await ServiceBootstrap.Initialize();
        await uiManager.InitializeAsync();

        var stageManager = StageManager.Instance;
        Debug.Assert(stageManager != null, "[GameManager] StageManager가 없습니다");
        stageManager.LoadSavedStage();

        await uiManager.ShowScene<UIInGameHUD>();

        // 서비스 준비가 끝난 뒤 광고 코디네이터를 초기화한다(배너 노출·이벤트 구독).
        if (AdCoordinator.TryGetInstance(out var adCoordinator))
            adCoordinator.Initialize();

        uiManager.HideInitialLoading().Forget();
    }

    // 진행 중이거나 대기 중인 저장을 완료까지 기다려 유실을 막는다(flush).
    private async UniTaskVoid FlushSaveAsync()
    {
        if (Services.TryGet<ISaveService>(out var save))
            await save.SaveAsync();
    }
}
