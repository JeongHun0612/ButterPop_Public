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

        uiManager.HideInitialLoading().Forget();
    }

    // 진행 중이거나 대기 중인 저장을 완료까지 기다려 유실을 막는다(flush).
    private async UniTaskVoid FlushSaveAsync()
    {
        if (Services.TryGet<ISaveService>(out var save))
            await save.SaveAsync();
    }
}
