#if GPGS_SDK && UNITY_ANDROID
using Cysharp.Threading.Tasks;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine;

// Play Games 리더보드. 로그인부터 제출까지 이 클래스가 담당한다.
public class GpgsLeaderboardService : ILeaderboardService
{
    private bool _authenticated;
    private long _pendingScore = -1;   // 로그인 완료 전에 들어온 제출

    public bool IsAvailable => _authenticated;

    // 부팅을 막지 않도록 호출부에서 떼어 실행한다. 응답이 없으면 이 태스크는 끝나지 않는다.
    public async UniTask InitializeAsync()
    {
        var auth = new UniTaskCompletionSource<bool>();
        PlayGamesPlatform.Instance.Authenticate(status => auth.TrySetResult(status == SignInStatus.Success));
        _authenticated = await auth.Task;

        if (!_authenticated)
        {
            Debug.LogWarning("[GpgsLeaderboardService] 로그인 실패. 이번 실행은 랭킹을 올리지 않습니다");
            return;
        }

        if (_pendingScore >= 0) Report(_pendingScore);
    }

    public void SubmitHighestStage(int stageIndex)
    {
        if (stageIndex < 0) return;

        long score = stageIndex + 1;   // 유저에게 보이는 스테이지 번호로 올린다
        // SDK가 로그인 전 제출을 거부하므로 들고 있다가 로그인 직후 올린다.
        // 진행도는 오르기만 하므로 나중 값으로 덮어써도 손실이 없다.
        if (!_authenticated)
        {
            _pendingScore = score;
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
        _pendingScore = -1;
        PlayGamesPlatform.Instance.ReportScore(score, LeaderboardIds.HighestStage, success =>
        {
            if (!success) Debug.LogWarning($"[GpgsLeaderboardService] 제출 실패: 스테이지 {score}");
        });
    }
}
#endif
