using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public interface IAnalyticsService
{
    UniTask InitializeAsync();

    // 파라미터 값은 string·int·long·float·double·bool만 허용한다.
    void LogEvent(string name, IReadOnlyDictionary<string, object> parameters = null);
}
