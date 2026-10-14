#if APPS_IN_TOSS
using System;
using System.Collections.Generic;
using AppsInToss;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 이벤트를 앱인토스 로그 시스템에 남긴다. 콘솔 '분석 > 이벤트'에서 이벤트 이름별로 집계된다.
public sealed class AppsInTossAnalyticsProvider : IAnalyticsProvider
{
    private const string LogType = "event";

    // 응답이 오지 않으면 SDK 콜백이 계속 남으므로 시간 제한을 둔다.
    private const int TimeoutMs = 10000;

    // SDK에 초기화 단계가 없다. 지원하지 않는 토스앱 버전에서는 SDK가 로그를 에러 없이 버린다.
    public UniTask<bool> InitializeAsync()
    {
        Debug.Log("[AppsInTossAnalyticsProvider] 초기화 완료");
        return UniTask.FromResult(true);
    }

    public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters) =>
        SendAsync(ToConsoleEventName(name), parameters).Forget();

    // 콘솔은 이벤트 하나의 파라미터 값 분포를 보여 주므로, 스테이지마다 나뉜 이름(stage_clear_07)을 합치고 stage_number로 구분한다.
    private static string ToConsoleEventName(string name)
    {
        if (name.StartsWith(AnalyticsService.StageClearSkipEvent + "_", StringComparison.Ordinal)) return AnalyticsService.StageClearSkipEvent;
        if (name.StartsWith(AnalyticsService.StageClearEvent + "_", StringComparison.Ordinal)) return AnalyticsService.StageClearEvent;
        return name;
    }

    private static async UniTaskVoid SendAsync(string name, IReadOnlyDictionary<string, object> parameters)
    {
        try
        {
            await AIT.EventLog(new EventLogParams
            {
                Log_name = name,
                Log_type = LogType,
                Params = parameters == null ? new Dictionary<string, object>() : new Dictionary<string, object>(parameters),
            }, TimeoutMs);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AppsInTossAnalyticsProvider] 이벤트 전송 실패 ({name}): {e.Message}");
        }
    }
}
#endif
