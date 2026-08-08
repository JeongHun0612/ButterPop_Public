using Cysharp.Threading.Tasks;
using UnityEngine;

public class SaveService : ISaveService
{
    private readonly ISaveStorage _storage;

    public SaveData Data { get; private set; }

    private SaveService(ISaveStorage storage) => _storage = storage;

    // 생성자에서 await 할 수 없으므로 비동기 로드는 팩토리로 처리한다.
    public static async UniTask<SaveService> CreateAsync(ISaveStorage storage)
    {
        var service = new SaveService(storage);
        service.Data = await storage.LoadAsync() ?? new SaveData();
        service.Migrate();
        Debug.Log($"[SaveService] 로드 완료 (v{service.Data.version})");
        return service;
    }

    public void Save() => SaveAsync().Forget();

    public UniTask SaveAsync() => _storage.SaveAsync(Data);
    // TODO(클라우드): 원격 스토리지 도입 시 동시 저장 합치기(debounce/직렬화) 추가.
    //                 로컬 파일은 원자적 교체라 지금 구조로 충분.

    public void DeleteAll()
    {
        _storage.DeleteAsync().Forget();
        Data = new SaveData();
        Debug.Log("[SaveService] 세이브 삭제");
    }

    private void Migrate()
    {
        // while (Data.version < CurrentVersion) { 단계별 변환; Data.version++; }
    }
}
