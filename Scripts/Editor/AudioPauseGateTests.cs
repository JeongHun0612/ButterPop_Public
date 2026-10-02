#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// 광고·결제·백그라운드 정지가 겹쳐도 마지막 사유가 풀릴 때만 오디오가 재개되는지 검증한다.
public class AudioPauseGateTests
{
    private readonly List<IDisposable> _handles = new();

    [SetUp]
    public void RequireNoPauseLeft() => Assume.That(AudioPauseGate.IsPaused, Is.False, "이전 테스트가 정지 사유를 남겼습니다");

    // 단언이 중간에 실패해도 정지 사유가 다음 테스트로 남지 않게 획득 역순으로 푼다.
    [TearDown]
    public void ReleaseAll()
    {
        for (int i = _handles.Count - 1; i >= 0; i--) _handles[i].Dispose();
        _handles.Clear();
    }

    [Test]
    public void SingleReasonPausesAndResumes()
    {
        var ad = Pause();
        Assert.IsTrue(AudioPauseGate.IsPaused);
        Assert.IsTrue(AudioListener.pause);

        ad.Dispose();
        Assert.IsFalse(AudioPauseGate.IsPaused);
        Assert.IsFalse(AudioListener.pause);
    }

    // 광고 중 백그라운드에 갔다 돌아와도 광고가 끝날 때까지 정지가 유지된다.
    [Test]
    public void ReturningFromBackgroundDuringAdKeepsPaused()
    {
        var ad = Pause();
        var background = Pause();

        background.Dispose();
        Assert.IsTrue(AudioPauseGate.IsPaused);

        ad.Dispose();
        Assert.IsFalse(AudioPauseGate.IsPaused);
    }

    // 결제가 백그라운드에서 끝나도 복귀 전에는 재개되지 않고, 복귀 후 정지가 남지 않는다.
    [Test]
    public void PurchaseEndingInBackgroundResumesOnReturn()
    {
        var purchase = Pause();
        var background = Pause();

        purchase.Dispose();
        Assert.IsTrue(AudioPauseGate.IsPaused);

        background.Dispose();
        Assert.IsFalse(AudioPauseGate.IsPaused);
        Assert.IsFalse(AudioListener.pause);
    }

    [Test]
    public void DisposingTwiceReleasesOnce()
    {
        var ad = Pause();
        var background = Pause();

        ad.Dispose();
        ad.Dispose();
        Assert.IsTrue(AudioPauseGate.IsPaused);

        background.Dispose();
        Assert.IsFalse(AudioPauseGate.IsPaused);
    }

    private IDisposable Pause()
    {
        var handle = AudioPauseGate.Acquire();
        _handles.Add(handle);
        return handle;
    }
}
#endif
