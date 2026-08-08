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
        UIManager.Instance.ShowInitialLoading().Forget();

        await ServiceBootstrap.Initialize();
        await UIManager.Instance.InitializeAsync();

        StageManager.Instance.LoadSavedStage();
        await UIManager.Instance.ShowScene<UIInGameHUD>();

        UIManager.Instance.HideInitialLoading().Forget();
    }

    // 진행 중이거나 대기 중인 저장을 완료까지 기다려 유실을 막는다(flush).
    private async UniTaskVoid FlushSaveAsync()
    {
        if (Services.TryGet<ISaveService>(out var save))
            await save.SaveAsync();
    }
}
