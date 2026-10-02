using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 광고 로드 실패 후 재시도를 예약한다. 실패가 이어질수록 간격표를 따라 늘리고(마지막 간격은 반복), 한 번에 하나만 예약한다.
// 언제 재시도할지는 SDK마다 달라 각 Provider가 정하고, 여기서는 타이밍만 맡는다.
public sealed class AdRetryTimer
{
    private readonly string _tag;
    private readonly string _label;
    private readonly TimeSpan[] _delays;

    private int _attempt;
    private CancellationTokenSource _cts;

    public AdRetryTimer(string tag, string label, TimeSpan[] delays)
    {
        _tag = tag;
        _label = label;
        _delays = delays;
    }

    // 이미 예약돼 있으면 무시한다.
    public void Schedule(Action retry)
    {
        if (_cts != null) return;

        TimeSpan delay = _delays[Math.Min(_attempt, _delays.Length - 1)];
        _attempt++;
        _cts = new CancellationTokenSource();
        RunAsync(delay, retry, _cts.Token).Forget();
        Debug.LogWarning($"[{_tag}] {_label}를 {delay.TotalSeconds:0}초 후 재시도합니다 (시도 {_attempt})");
    }

    // 성공하면 다음 실패는 첫 간격부터 다시 센다.
    public void Reset()
    {
        _attempt = 0;
        Cancel();
    }

    public void Cancel()
    {
        if (_cts == null) return;

        _cts.Cancel();
        _cts.Dispose();
        _cts = null;
    }

    private async UniTaskVoid RunAsync(TimeSpan delay, Action retry, CancellationToken token)
    {
        bool canceled = await UniTask.Delay(delay, cancellationToken: token).SuppressCancellationThrow();
        if (canceled) return;

        Cancel();
        retry();
    }
}
