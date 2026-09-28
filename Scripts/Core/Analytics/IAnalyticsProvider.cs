using System.Collections.Generic;
using Cysharp.Threading.Tasks;

// 애널리틱스 SDK 어댑터. SDK 교체는 이 구현만 바꾼다.
public interface IAnalyticsProvider
{
    // 수집 가능 상태가 되면 true를 반환한다.
    UniTask<bool> InitializeAsync();

    void LogEvent(string name, IReadOnlyDictionary<string, object> parameters);
}
