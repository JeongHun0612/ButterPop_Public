using Cysharp.Threading.Tasks;

public static class ServiceBootstrap
{
    public static async UniTask Initialize()
    {
        var localization = new LocalizationService();
        await localization.InitializeAsync();
        Services.Register<ILocalizationService>(localization);

        // 세이브 서비스: 파일에서 세이브를 비동기로 로드한 뒤 등록한다.
        // 다른 서비스들이 세이브 데이터에 의존하므로 가장 먼저 준비한다.
        var save = await SaveService.CreateAsync(new FileSaveStorage("save.dat"));
        Services.Register<ISaveService>(save);

        // 구매 서비스: 세이브의 구매 데이터를 읽고 쓰며, 초기화 시 스토어에서 보유 상품을 복원한다.
        // 스토어에 붙을 수 있는 모바일 릴리즈 빌드만 Unity IAP를 타고, 나머지는 스텁을 쓴다.
        IIapProvider iapProvider = new StubIapProvider();
#if UNITY_IAP
        if (BuildFlags.UseLiveIap) iapProvider = new UnityIapProvider();
#endif
        var purchase = new PurchaseService(save, iapProvider);
        Services.Register<IPurchaseService>(purchase);

        // 스토어 응답을 기다리면 네트워크가 없을 때 부팅이 멈춘다.
        purchase.InitializeAsync().Forget();

        // 스킨 서비스: 세이브의 스킨 데이터(소유/장착)를 읽고 쓴다. 올스킨 언락 판정을 위해 구매 서비스를 참조한다.
        Services.Register<ISkinService>(new SkinService(save, purchase));

        // 광고 서비스: 배너/전면/리워드를 재생한다. 광고 제거 게이팅을 위해 구매 서비스를 참조한다.
        // 실광고는 릴리즈 빌드만 태운다(LevelPlay는 에디터에서도 테스트되므로 에디터도 포함).
        // 그 외는 스텁이라 배너가 없고 보상형도 시청 없이 보상한다.
        // 아래 #if는 LevelPlayProvider의 컴파일 게이트와 동일하게 유지한다(클래스 존재 여부 판정).
        IAdProvider adProvider = new StubAdProvider();
#if LEVELPLAY_SDK && (UNITY_ANDROID || UNITY_IOS)
        if (BuildFlags.IsRelease) adProvider = new LevelPlayProvider();
#endif
        var ads = new AdsService(adProvider, purchase);
        Services.Register<IAdsService>(ads);
        ads.InitializeAsync().Forget();
    }
}

