using Cysharp.Threading.Tasks;

public static class ServiceBootstrap
{
    // 등록 순서가 곧 의존 순서다. 뒤에 오는 서비스가 앞의 것을 인자로 받는다.
    public static async UniTask Initialize()
    {
        Services.Register<ILocalizationService>(await CreateLocalizationAsync());

        // 이후 서비스들이 세이브를 인자로 받으므로 가장 먼저 준비한다.
        var save = await SaveService.CreateAsync(CreateSaveStorage());
        Services.Register<ISaveService>(save);

        // 결제·광고·리더보드가 이벤트를 남길 수 있도록 그보다 먼저 준비한다.
        var analytics = new AnalyticsService(CreateAnalyticsProvider());
        Services.Register<IAnalyticsService>(analytics);

        var purchase = new PurchaseService(save, CreateIapProvider());
        Services.Register<IPurchaseService>(purchase);

        // 올스킨 언락 판정에 구매 여부가 필요하다.
        Services.Register<ISkinService>(new SkinService(save, purchase));

        // 광고 제거 게이팅에 구매 여부가 필요하다.
        var ads = new AdsService(CreateAdProvider(), purchase, analytics);
        Services.Register<IAdsService>(ads);

        var leaderboard = CreateLeaderboard();
        Services.Register<ILeaderboardService>(leaderboard);

        // SDK 응답을 기다리면 네트워크가 없을 때 부팅이 멈추므로 떼어 낸다.
        // 구독자가 모두 붙은 뒤 시작해야 복원 완료 통지를 놓치지 않는다.
        analytics.InitializeAsync().Forget();
        purchase.InitializeAsync().Forget();
        ads.InitializeAsync().Forget();
        leaderboard.InitializeAsync().Forget();

#if LEVELPLAY_SDK && (UNITY_ANDROID || UNITY_IOS) && BUTTERPOP_AD_TEST_SUITE && !UNITY_EDITOR
        LevelPlayTestSuite.Launch();
#endif
    }

    private static async UniTask<ILocalizationService> CreateLocalizationAsync()
    {
        var localization = new LocalizationService();
        await localization.InitializeAsync();
        return localization;
    }

    private static ISaveStorage CreateSaveStorage()
    {
#if APPS_IN_TOSS && UNITY_WEBGL && !UNITY_EDITOR
        return new AppsInTossSaveStorage("butterpop.save.v1");
#else
        return new FileSaveStorage("save.dat");
#endif
    }

    // 디버그 빌드 이벤트가 실데이터에 섞이지 않도록 릴리즈 기기 빌드만 수집한다.
    // 에디터의 Firebase는 데스크톱 스텁이라 수집되지 않으므로 제외한다.
    private static IAnalyticsProvider CreateAnalyticsProvider()
    {
#if FIREBASE_SDK && UNITY_ANDROID && !UNITY_EDITOR
        if (BuildFlags.IsRelease) return new FirebaseAnalyticsProvider();
#endif
        return new StubAnalyticsProvider();
    }

    // 스토어에 붙을 수 있는 모바일 릴리즈 빌드만 Unity IAP를 탄다.
    private static IIapProvider CreateIapProvider()
    {
#if APPS_IN_TOSS && UNITY_WEBGL && !UNITY_EDITOR
        return new AppsInTossIapProvider();
#elif UNITY_IAP
        if (BuildFlags.UseLiveIap) return new UnityIapProvider();
#endif
        return new StubIapProvider();
    }

    // 실광고는 릴리즈 빌드만 태운다(LevelPlay는 에디터에서도 테스트되므로 에디터를 제외하지 않는다).
    // #if 조건은 LevelPlayProvider의 컴파일 게이트와 동일하게 유지한다.
    private static IAdProvider CreateAdProvider()
    {
#if APPS_IN_TOSS && UNITY_WEBGL && !UNITY_EDITOR
        return new AppsInTossAdProvider();
#elif LEVELPLAY_SDK && (UNITY_ANDROID || UNITY_IOS)
        if (BuildFlags.IsRelease) return new LevelPlayProvider();
#endif
        return new StubAdProvider();
    }

    // 광고·결제와 달리 릴리즈 여부로 가르지 않는다. 붙는 비용이 없고 디버그 빌드에서 실제로 검증해야 한다.
    private static ILeaderboardService CreateLeaderboard()
    {
#if APPS_IN_TOSS && UNITY_WEBGL && !UNITY_EDITOR
        // 게임센터 리더보드를 연결하기 전까지 랭킹 진입 버튼을 숨긴다.
        return new StubLeaderboardService(isAvailable: false);
#elif GPGS_SDK && UNITY_ANDROID && !UNITY_EDITOR
        return new GpgsLeaderboardService();
#else
        return new StubLeaderboardService();
#endif
    }
}
