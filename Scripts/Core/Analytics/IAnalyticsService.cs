using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public interface IAnalyticsService
{
    UniTask InitializeAsync();

    // 파라미터 값은 string·int·long·float·double·bool만 허용한다.
    void LogEvent(string name, IReadOnlyDictionary<string, object> parameters = null);

    // 광고 1회 노출의 수익. GA4 광고 수익으로 집계된다.
    void LogAdImpression(AdImpression impression);

    // 스테이지 최초 클리어·스킵. stageIndex는 0부터이며 stage_clear_01 / stage_clear_skip_01 형태의 이벤트가 된다.
    void LogStageClear(int stageIndex, int attemptCount, bool rewardWatched, bool skipped);
}
