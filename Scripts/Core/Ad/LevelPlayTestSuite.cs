// LevelPlay 통합 테스트 스위트를 띄워 네트워크별 어댑터 연동과 광고 로드를 기기에서 점검한다.
// 디버그 빌드는 게임 광고를 Stub으로 대체해 LevelPlay를 초기화하지 않으므로 여기서 테스트 스위트 전용으로 초기화한다.
// 빌드 프로필에 BUTTERPOP_AD_TEST_SUITE 디파인을 넣었을 때만 컴파일된다. 에디터 SDK는 테스트 스위트를 지원하지 않아 제외한다.
#if LEVELPLAY_SDK && (UNITY_ANDROID || UNITY_IOS) && BUTTERPOP_AD_TEST_SUITE && !UNITY_EDITOR
using Unity.Services.LevelPlay;
using UnityEngine;

public static class LevelPlayTestSuite
{
    public static void Launch()
    {
        // 릴리즈 빌드는 LevelPlayProvider가 초기화를 맡고, 디버그 화면이 사용자에게 노출되면 안 된다.
        if (BuildFlags.IsRelease)
        {
            Debug.LogWarning("[LevelPlayTestSuite] 릴리즈 빌드라 테스트 스위트를 띄우지 않음");
            return;
        }

        LevelPlay.OnInitSuccess += OnInitSuccess;
        LevelPlay.OnInitFailed += OnInitFailed;

        // Init 전에 설정해야 테스트 스위트가 활성화된다.
        LevelPlay.SetMetaData("is_test_suite", "enable");
        LevelPlay.Init(LevelPlayProvider.AppKey);
    }

    private static void Unsubscribe()
    {
        LevelPlay.OnInitSuccess -= OnInitSuccess;
        LevelPlay.OnInitFailed -= OnInitFailed;
    }

    private static void OnInitSuccess(LevelPlayConfiguration _)
    {
        Unsubscribe();
        Debug.Log("[LevelPlayTestSuite] 초기화 성공, 테스트 스위트 실행");
        LevelPlay.LaunchTestSuite();
    }

    private static void OnInitFailed(LevelPlayInitError error)
    {
        Unsubscribe();
        Debug.LogError($"[LevelPlayTestSuite] 초기화 실패: {error}");
    }
}
#endif
