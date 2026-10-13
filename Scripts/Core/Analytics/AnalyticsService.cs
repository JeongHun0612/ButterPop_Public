using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

// SDK 초기화 전에 들어온 이벤트는 모아 두었다가 초기화가 끝나면 보낸다.
public class AnalyticsService : IAnalyticsService
{
    // 스테이지 이벤트 이름의 앞부분. 실제 이벤트 이름에는 stage_clear_01처럼 스테이지 번호가 붙는다.
    public const string StageClearEvent = "stage_clear";
    public const string StageClearSkipEvent = "stage_clear_skip";

    // 초기화가 끝나지 않을 때 대기 이벤트가 무한히 쌓이지 않도록 제한한다.
    private const int MaxPendingEvents = 100;

    private readonly IAnalyticsProvider _provider;
    private readonly Queue<(string name, IReadOnlyDictionary<string, object> parameters)> _pending = new();

    private bool _ready;
    private bool _disabled;   // 초기화 실패 시 이후 이벤트를 버린다

    public AnalyticsService(IAnalyticsProvider provider)
    {
        _provider = provider;
    }

    public async UniTask InitializeAsync()
    {
        bool ok;
        try
        {
            ok = await _provider.InitializeAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"[AnalyticsService] 초기화 실패: {e.Message}");
            ok = false;
        }

        if (!ok)
        {
            _disabled = true;
            _pending.Clear();
            return;
        }

        _ready = true;
        while (_pending.Count > 0)
        {
            var (name, parameters) = _pending.Dequeue();
            _provider.LogEvent(name, parameters);
        }
    }

    public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters = null)
    {
        if (_disabled) return;

        if (_ready)
        {
            _provider.LogEvent(name, parameters);
            return;
        }

        if (_pending.Count < MaxPendingEvents) _pending.Enqueue((name, parameters));
    }

    // GA4 표준 파라미터 이름을 써야 광고 수익으로 인식된다.
    public void LogAdImpression(AdImpression impression) =>
        LogEvent("ad_impression", new Dictionary<string, object>
        {
            ["ad_platform"] = impression.Platform,
            ["ad_source"] = impression.Network,
            ["ad_format"] = impression.Format,
            ["ad_unit_name"] = impression.UnitName,
            ["currency"] = "USD",
            ["value"] = impression.Revenue,
        });

    public void LogStageClear(int stageIndex, int attemptCount, bool rewardWatched, bool skipped)
    {
        // UI 표기(#1부터)와 맞춘다.
        int stageNumber = stageIndex + 1;
        string prefix = skipped ? StageClearSkipEvent : StageClearEvent;
        string eventName = $"{prefix}_{stageNumber:D2}";   // 예: stage_clear_01, stage_clear_skip_01

        LogEvent(eventName, new Dictionary<string, object>
        {
            ["stage_number"] = stageNumber,
            ["attempt_count"] = attemptCount,
            ["reward_watched"] = rewardWatched,
        });
    }
}
