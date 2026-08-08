using Cysharp.Threading.Tasks;

public interface ISaveService
{
    SaveData Data { get; }
    void Save();              // 발사 후 잊기 (게임플레이 중 일반 저장)
    UniTask SaveAsync();      // 완료 보장이 필요할 때 (앱 종료/일시정지 flush 등)
    void DeleteAll();
}
