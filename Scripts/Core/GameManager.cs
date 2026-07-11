using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameManager : MonoSingleton<GameManager>
{
    [SerializeField] private int startStageIndex = 0;

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

        await UIManager.Instance.InitializeAsync();
        StageManager.Instance.LoadStage(startStageIndex);

        await UIManager.Instance.ShowScene<UIInGameHUD>();

        UIManager.Instance.HideInitialLoading().Forget();
    }
}
