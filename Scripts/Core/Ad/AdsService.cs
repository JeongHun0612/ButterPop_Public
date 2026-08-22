using Cysharp.Threading.Tasks;

// 게임이 사용하는 광고 계약 구현. SDK는 IAdProvider가 감추고,
// 여기서는 광고 제거 게이팅 · preload · (추후)빈도 제한 같은 정책을 담당한다.
public class AdsService : IAdsService
{
    private readonly IAdProvider _provider;
    private readonly IPurchaseService _purchase;

    public AdsService(IAdProvider provider, IPurchaseService purchase)
    {
        _provider = provider;
        _purchase = purchase;
        _purchase.OnPurchasesUpdated += OnPurchasesUpdated;
    }

    public bool IsRewardedReady => _provider.IsRewardedReady;

    public async UniTask InitializeAsync()
    {
        await _provider.InitializeAsync();

        // 전면·리워드는 표시 전에 미리 로드해 둔다.
        _provider.LoadInterstitial();
        _provider.LoadRewarded();
    }

    public void ShowBanner()
    {
        if (_purchase.RemoveBannerAds) return;
        _provider.ShowBanner();
    }

    public void HideBanner() => _provider.HideBanner();

    public async UniTask<bool> ShowInterstitialAsync()
    {
        if (_purchase.RemoveAllAds) return false;

        // 로드가 안 됐으면 다음을 위해 로드만 걸고 흐름은 막지 않는다(게임 진행 우선).
        if (!_provider.IsInterstitialReady)
        {
            _provider.LoadInterstitial();
            return false;
        }

        bool shown = await _provider.ShowInterstitialAsync();
        _provider.LoadInterstitial(); // 다음 표시를 위해 재로드
        return shown;
    }

    public async UniTask<bool> ShowRewardedAsync()
    {
        // 리워드는 광고 제거와 무관하게 항상 허용한다(유저가 보상을 위해 자발적으로 시청).
        if (!_provider.IsRewardedReady)
        {
            _provider.LoadRewarded();
            return false;
        }

        bool rewarded = await _provider.ShowRewardedAsync();
        _provider.LoadRewarded(); // 다음 시청을 위해 재로드
        return rewarded;
    }

    // 광고 제거 구매가 반영되면 이미 떠 있는 배너를 즉시 내린다.
    private void OnPurchasesUpdated()
    {
        if (_purchase.RemoveBannerAds) _provider.HideBanner();
    }
}
