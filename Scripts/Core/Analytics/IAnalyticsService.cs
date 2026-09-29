using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public interface IAnalyticsService
{
    UniTask InitializeAsync();

    // 파라미터 값은 string·int·long·float·double·bool만 허용한다.
    void LogEvent(string name, IReadOnlyDictionary<string, object> parameters = null);

    // 광고 1회 노출의 수익. GA4 광고 수익으로 집계된다.
    void LogAdImpression(AdImpression impression);

    // 스테이지 최초 클리어. stageIndex는 0부터, attemptCount는 클리어한 시도를 포함한다.
    void LogStageClear(int stageIndex, int attemptCount, bool rewardWatched);
}
