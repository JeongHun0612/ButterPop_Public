#if APPS_IN_TOSS
using System;
using AppsInToss;
using UnityEngine;

// 미니앱이 백그라운드로 가면 오디오를 멈추고, 돌아오면 다른 정지 사유(광고·결제)가 없을 때 재개한다.
// Unity WebGL은 visibilitychange를 처리하지 않아 SDK의 AITVisibilityHelper로 감지한다.
public static class AppsInTossBackgroundAudio
{
    private static IDisposable _pause;

    public static void Initialize()
    {
        AITVisibilityHelper.OnVisibilityChanged += OnVisibilityChanged;

        // SDK jslib은 IsVisible을 처음 읽을 때 visibilitychange 리스너를 등록한다. 구독만으로는 이벤트가 오지 않는다.
        OnVisibilityChanged(AITVisibilityHelper.IsVisible);
    }

    private static void OnVisibilityChanged(bool isVisible)
    {
        if (!isVisible)
        {
            if (_pause != null) return;

            _pause = AudioPauseGate.Acquire();
            Debug.Log("[AppsInTossBackgroundAudio] 백그라운드 전환: 오디오 정지");
            return;
        }

        if (_pause == null) return;

        _pause.Dispose();
        _pause = null;
        Debug.Log(AudioPauseGate.IsPaused
            ? "[AppsInTossBackgroundAudio] 포그라운드 복귀: 광고·결제 중이라 정지 유지"
            : "[AppsInTossBackgroundAudio] 포그라운드 복귀: 오디오 재개");
    }
}
#endif
