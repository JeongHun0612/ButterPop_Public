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
#if APPS_IN_TOSS && UNITY_WEBGL && !UNITY_EDITOR
        // 첫 화면부터 노치·토스 X 버튼을 피하도록 서비스 준비를 기다리지 않고 연결한다.
        AppsInTossSafeArea.Initialize();
#endif
        var uiManager = UIManager.Instance;
        Debug.Assert(uiManager != null, "[GameManager] UIManager가 없습니다");

        uiManager.ShowInitialLoading().Forget();

        await ServiceBootstrap.Initialize();

        // 세이브가 준비된 직후 음소거를 복원한다.
        if (SoundManager.TryGetInstance(out var sound))
            sound.Initialize();

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
    // OS가 앱을 정리하기 직전 마지막 기회이므로 실패는 반드시 남긴다.
    private async UniTaskVoid FlushSaveAsync()
    {
        if (!Services.TryGet<ISaveService>(out var save)) return;

        if (!await save.SaveAsync())
            Debug.LogError("[GameManager] 일시정지 flush 저장 실패 — 이번 세션 진행도가 유실될 수 있습니다");
    }
}
