using Cysharp.Threading.Tasks;

public interface ISaveService
{
    SaveData Data { get; }
    bool IsReadOnly { get; }        // 로드 실패 세션에서는 기존 데이터를 보호하기 위해 저장·삭제를 차단한다
    void Save();                    // 발사 후 잊기 (게임플레이 중 일반 저장)
    UniTask<bool> SaveAsync();      // 완료 보장이 필요할 때 (앱 종료/일시정지 flush 등). 기록 성공 시 true
    void DeleteAll();
}
