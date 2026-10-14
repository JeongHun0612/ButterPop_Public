using Cysharp.Threading.Tasks;

// 진행도를 플랫폼 랭킹에 올린다. 순위 조회·표시는 플랫폼 UI에 맡긴다.
public interface ILeaderboardService
{
    bool IsAvailable { get; }

    UniTask InitializeAsync();
    void SubmitHighestStage(int stageIndex);
    void ShowLeaderboard();
}
