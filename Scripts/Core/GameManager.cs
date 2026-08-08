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

    private async UniTask InitializeAsync()
    {
        UIManager.Instance.ShowInitialLoading().Forget();

        await ServiceBootstrap.Initialize();
        await UIManager.Instance.InitializeAsync();

        StageManager.Instance.LoadSavedStage();
        await UIManager.Instance.ShowScene<UIInGameHUD>();

        UIManager.Instance.HideInitialLoading().Forget();
    }
}
