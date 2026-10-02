using System;
using UnityEngine;

// 광고·결제·백그라운드처럼 오디오를 멈추는 사유가 겹쳐도 모든 사유가 풀렸을 때만 재개한다.
// AudioListener.pause는 이 클래스만 바꾼다. 음소거는 AudioListener.volume을 쓰므로 별개다.
public static class AudioPauseGate
{
    private static int _count;

    public static bool IsPaused => _count > 0;

    // 반환값을 Dispose하면 이 사유가 풀린다. 같은 핸들을 여러 번 Dispose해도 한 번만 반영된다.
    public static IDisposable Acquire()
    {
        _count++;
        Apply();
        return new Handle();
    }

    private static void Release()
    {
        if (_count == 0) return;

        _count--;
        Apply();
    }

    private static void Apply() => AudioListener.pause = _count > 0;

    private sealed class Handle : IDisposable
    {
        private bool _released;

        public void Dispose()
        {
            if (_released) return;

            _released = true;
            Release();
        }
    }
}
