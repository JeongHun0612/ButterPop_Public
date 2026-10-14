using Cysharp.Threading.Tasks;

// SDK 호출을 분리해 실패·지연 응답도 검증한다.
public interface IAppsInTossLeaderboardApi
{
    UniTask<bool> IsSupportedAsync();
    UniTask<string> SubmitScoreAsync(string score);
    UniTask OpenAsync();
}
