using Cysharp.Threading.Tasks;
using UnityEngine;

// 에디터·PC용 스텁. 제출 값을 로그로만 확인한다.
public class StubLeaderboardService : ILeaderboardService
{
    public bool IsAvailable => true;

    public UniTask InitializeAsync()
    {
        Debug.Log("[StubLeaderboardService] 초기화(스텁)");
        return UniTask.CompletedTask;
    }

    public void SubmitHighestStage(int stageIndex) =>
        Debug.Log($"[StubLeaderboardService] 제출(스텁): 스테이지 {stageIndex + 1}");

    public void ShowLeaderboard() => Debug.Log("[StubLeaderboardService] 랭킹 표시(스텁)");
}
