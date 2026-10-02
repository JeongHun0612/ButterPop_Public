// FIREBASE_SDK 디파인(Firebase SDK 임포트 후 수동 추가)이 있는 Android 빌드에서만 컴파일된다.
#if FIREBASE_SDK && UNITY_ANDROID
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Analytics;
using UnityEngine;

public class FirebaseAnalyticsProvider : IAnalyticsProvider
{
    // 기기의 Google Play 서비스 상태를 확인·복구한 뒤에야 SDK를 호출할 수 있다.
    public async UniTask<bool> InitializeAsync()
    {
        var status = await FirebaseApp.CheckAndFixDependenciesAsync().AsUniTask();
        if (status != DependencyStatus.Available)
        {
            Debug.LogError($"[FirebaseAnalyticsProvider] 의존성 확인 실패: {status}");
            return false;
        }

        FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
        Debug.Log("[FirebaseAnalyticsProvider] 초기화 성공");
        return true;
    }

    public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters)
    {
        if (parameters == null || parameters.Count == 0)
        {
            FirebaseAnalytics.LogEvent(name);
            return;
        }

        var converted = new List<Parameter>(parameters.Count);
        foreach (var pair in parameters)
        {
            var parameter = ToParameter(pair.Key, pair.Value);
            if (parameter != null) converted.Add(parameter);
        }

        FirebaseAnalytics.LogEvent(name, converted.ToArray());
    }

    // Firebase 파라미터는 string·long·double만 지원하므로 bool은 0/1로 바꾼다.
    private static Parameter ToParameter(string key, object value) => value switch
    {
        string s => new Parameter(key, s),
        int i => new Parameter(key, (long)i),
        long l => new Parameter(key, l),
        float f => new Parameter(key, (double)f),
        double d => new Parameter(key, d),
        bool b => new Parameter(key, b ? 1L : 0L),
        null => null,
        _ => new Parameter(key, value.ToString()),
    };
}
#endif
