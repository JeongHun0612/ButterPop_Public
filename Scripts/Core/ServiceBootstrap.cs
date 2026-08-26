using Cysharp.Threading.Tasks;

public static class ServiceBootstrap
{
    public static async UniTask Initialize()
    {
        // 세이브 서비스: 파일에서 세이브를 비동기로 로드한 뒤 등록한다.
        // 다른 서비스들이 세이브 데이터에 의존하므로 가장 먼저 준비한다.
        var save = await SaveService.CreateAsync(new FileSaveStorage("save.dat"));
        Services.Register<ISaveService>(save);

        // 구매 서비스: 세이브의 구매 데이터를 읽고 쓰며, 초기화 시 스토어에서 보유 상품을 복원한다.
        // 에디터는 스토어에 붙을 수 없어 스텁을 쓴다. 실기기 빌드만 Unity IAP를 탄다.
#if UNITY_IAP && !UNITY_EDITOR
        IIapProvider iapProvider = new UnityIapProvider();
#else
        IIapProvider iapProvider = new StubIapProvider();
#endif
        var purchase = new PurchaseService(save, iapProvider);
        Services.Register<IPurchaseService>(purchase);

        // 스토어 응답을 기다리면 네트워크가 없을 때 부팅이 멈춘다.
        purchase.InitializeAsync().Forget();

        // 스킨 서비스: 세이브의 스킨 데이터(소유/장착)를 읽고 쓴다. 올스킨 언락 판정을 위해 구매 서비스를 참조한다.
        Services.Register<ISkinService>(new SkinService(save, purchase));

        // 광고 서비스: 배너/전면/리워드를 재생한다. 광고 제거 게이팅을 위해 구매 서비스를 참조한다.
#if LEVELPLAY_SDK
        IAdProvider adProvider = new LevelPlayProvider();
#else
        IAdProvider adProvider = new StubAdProvider();
#endif
        var ads = new AdsService(adProvider, purchase);
        Services.Register<IAdsService>(ads);
        ads.InitializeAsync().Forget();
    }
}

