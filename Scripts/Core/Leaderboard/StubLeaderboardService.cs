using Cysharp.Threading.Tasks;
using UnityEngine;

// 플랫폼 랭킹이 연결되지 않은 환경용 스텁. 제출 값을 로그로만 확인한다.
// 진입 버튼 노출 여부는 생성 시 정한다(에디터는 UI 확인을 위해 노출).
public class StubLeaderboardService : ILeaderboardService
{
    private readonly bool _isAvailable;

    public StubLeaderboardService(bool isAvailable = true) => _isAvailable = isAvailable;

    public bool IsAvailable => _isAvailable;

    public UniTask InitializeAsync()
    {
        Debug.Log("[StubLeaderboardService] 초기화(스텁)");
        return UniTask.CompletedTask;
    }

    public void SubmitHighestStage(int stageIndex) =>
        Debug.Log($"[StubLeaderboardService] 제출(스텁): 스테이지 {stageIndex + 1}");

    public void ShowLeaderboard() => Debug.Log("[StubLeaderboardService] 랭킹 표시(스텁)");
}
