using Cysharp.Threading.Tasks;

// 진행도를 플랫폼 랭킹에 올린다. 순위 조회·표시는 플랫폼이 제공하는 UI에 맡긴다.
public interface ILeaderboardService
{
    bool IsAvailable { get; }                  // 로그인 완료 여부. 진입 버튼 노출 판단에 쓴다

    UniTask InitializeAsync();                 // 로그인. 실패해도 게임은 그대로 진행한다
    void SubmitHighestStage(int stageIndex);   // 0-based 인덱스를 받아 표시용 스테이지 번호로 올린다
    void ShowLeaderboard();
}
