#if APPS_IN_TOSS
using AppsInToss;
using Cysharp.Threading.Tasks;

public sealed class AppsInTossLeaderboardApi : IAppsInTossLeaderboardApi
{
    private const int TimeoutMs = 10000;
    private const string MinimumVersion = "5.221.0";

    public async UniTask<bool> IsSupportedAsync() =>
        await AIT.IsMinVersionSupported(new IsMinVersionSupportedMinVersions
        {
            Android = MinimumVersion,
            Ios = MinimumVersion
        }, TimeoutMs);

    public async UniTask<string> SubmitScoreAsync(string score)
    {
        var response = await AIT.SubmitGameCenterLeaderBoardScore(
            new SubmitGameCenterLeaderBoardScoreParams { Score = score }, TimeoutMs);
        return response?.StatusCode;
    }

    public async UniTask OpenAsync() => await AIT.OpenGameCenterLeaderboard(TimeoutMs);
}
#endif
