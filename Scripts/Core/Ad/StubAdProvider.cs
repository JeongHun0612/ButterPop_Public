using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

// SDK 미연동 개발용 스텁 프로바이더. 실제 광고 없이 흐름·게이팅을 테스트한다.
// 실 SDK(AdMob·LevelPlay 등)를 붙일 때 이 자리에 해당 IAdProvider 구현을 교체한다.
public class StubAdProvider : IAdProvider
{
    public event Action<bool> OnRewardedAvailabilityChanged;

    // 스텁은 수익이 없어 발화하지 않는다.
    public event Action<AdImpression> OnAdImpression { add { } remove { } }

    public bool IsInterstitialReady => true;
    public bool IsRewardedReady => true;

    // 스텁은 배너 없음.
    public float BannerHeightPixels => 0f;
    public BannerSide BannerSide => BannerSide.Top;

    public async UniTask InitializeAsync()
    {
        Debug.Log("[StubAdProvider] 초기화(스텁)");
        OnRewardedAvailabilityChanged?.Invoke(true);
        await UniTask.CompletedTask;
    }

    public void ShowBanner() => Debug.Log("[StubAdProvider] 배너 표시(스텁)");

    public void HideBanner() => Debug.Log("[StubAdProvider] 배너 숨김(스텁)");

    public void LoadInterstitial() => Debug.Log("[StubAdProvider] 전면 로드(스텁)");

    public async UniTask<bool> ShowInterstitialAsync()
    {
        Debug.Log("[StubAdProvider] 전면 표시(스텁)");
        await UniTask.CompletedTask;
        return true;
    }

    public void LoadRewarded() => Debug.Log("[StubAdProvider] 리워드 로드(스텁)");

    public async UniTask<bool> ShowRewardedAsync()
    {
        Debug.Log("[StubAdProvider] 리워드 재생(스텁): 즉시 보상 지급");
        await UniTask.CompletedTask;
        return true;
    }
}
