using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 에디터·디버그 빌드용 스텁. 이벤트를 수집하지 않고 로그로만 남긴다.
public class StubAnalyticsProvider : IAnalyticsProvider
{
    public UniTask<bool> InitializeAsync()
    {
        Debug.Log("[StubAnalyticsProvider] 초기화(스텁)");
        return UniTask.FromResult(true);
    }

    public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters)
    {
        string args = parameters == null ? "" : string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value}"));
        Debug.Log($"[StubAnalyticsProvider] {name} ({args})");
    }
}
