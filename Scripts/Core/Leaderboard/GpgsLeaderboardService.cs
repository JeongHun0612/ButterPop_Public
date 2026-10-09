#if GPGS_SDK && UNITY_ANDROID
using Cysharp.Threading.Tasks;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine;

// Play Games 리더보드. 로그인부터 제출까지 이 클래스가 담당한다.
public class GpgsLeaderboardService : ILeaderboardService
{
    private readonly int _stageCount;
    private bool _authenticated;

    public bool IsAvailable => _authenticated;

    public GpgsLeaderboardService(int stageCount) => _stageCount = stageCount;

    // 부팅을 막지 않도록 호출부에서 떼어 실행한다. 응답이 없으면 이 태스크는 끝나지 않는다.
    public async UniTask InitializeAsync()
    {
        var auth = new UniTaskCompletionSource<bool>();
        PlayGamesPlatform.Instance.Authenticate(status => auth.TrySetResult(status == SignInStatus.Success));
        _authenticated = await auth.Task;

        if (!_authenticated)
            Debug.LogWarning("[GpgsLeaderboardService] 로그인 실패. 이번 실행은 랭킹을 올리지 않습니다");
    }

    public void SubmitHighestStage(int stageIndex)
    {
        // SDK가 로그인 전 제출을 거부한다.
        if (!_authenticated) return;
        if (!LeaderboardScore.TryFromStageIndex(stageIndex, _stageCount, out long score))
        {
            Debug.LogWarning($"[GpgsLeaderboardService] 제출 범위를 벗어난 스테이지: {stageIndex}");
            return;
        }

        Report(score);
    }

    public void ShowLeaderboard()
    {
        if (!_authenticated) return;
        PlayGamesPlatform.Instance.ShowLeaderboardUI(LeaderboardIds.HighestStage);
    }

    // 실패는 로그만 남긴다. 다음 클리어 때 더 높은 값으로 다시 제출된다.
    private void Report(long score)
    {
        PlayGamesPlatform.Instance.ReportScore(score, LeaderboardIds.HighestStage, success =>
        {
            if (!success) Debug.LogWarning($"[GpgsLeaderboardService] 제출 실패: 스테이지 {score}");
        });
    }
}
#endif
