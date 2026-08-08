using Cysharp.Threading.Tasks;

public static class ServiceBootstrap
{
    public static async UniTask Initialize()
    {
        // 세이브 서비스: 파일에서 세이브를 비동기로 로드한 뒤 등록한다.
        // 다른 서비스들이 세이브 데이터에 의존하므로 가장 먼저 준비한다.
        var save = await SaveService.CreateAsync(new FileSaveStorage("save.dat"));
        Services.Register<ISaveService>(save);

        // 스킨 서비스: 세이브의 스킨 데이터(소유/장착)를 읽고 쓴다.
        Services.Register<ISkinService>(new SkinService(save));

        // 구매 서비스: 세이브의 구매 데이터를 읽고 쓰며, 초기화 시 스토어에서 보유 상품을 복원한다.
        var purchase = new PurchaseService(save);
        Services.Register<IPurchaseService>(purchase);
        await purchase.InitializeAsync();
    }
}

