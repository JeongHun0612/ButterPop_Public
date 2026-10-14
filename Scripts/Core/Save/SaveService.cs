using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class SaveService : ISaveService
{
    private readonly ISaveStorage _storage;

    // 가장 마지막에 예약된 저장. 새 요청을 이 뒤에 이어 붙여 쓰기를 한 줄로 세운다.
    private UniTask<bool> _pending = UniTask.FromResult(true);

    public SaveData Data { get; private set; }
    public bool IsReadOnly { get; private set; } = true;

    private SaveService(ISaveStorage storage) => _storage = storage;

    // 생성자에서 await 할 수 없으므로 비동기 로드는 팩토리로 처리한다.
    public static async UniTask<SaveService> CreateAsync(ISaveStorage storage)
    {
        var service = new SaveService(storage);
        try
        {
            service.Data = await storage.LoadAsync() ?? new SaveData();
            SaveMigration.Apply(service.Data);
            service.IsReadOnly = false;
            Debug.Log($"[SaveService] 로드 완료 (v{service.Data.version})");
        }
        catch (Exception e)
        {
            service.Data = new SaveData();
            Debug.LogError($"[SaveService] 로드 실패, 기존 데이터 보호를 위해 이번 세션의 저장·삭제를 차단합니다: {e.Message}");
        }
        return service;
    }

    public void Save() => SaveAsync().Forget();

    // 저장은 항상 직렬화한다. 동시에 두 건이 돌면 같은 임시 파일을 밟거나
    // 완료 순서가 뒤집혀 오래된 스냅샷이 최신을 덮어쓸 수 있다.
    // (File.Replace의 원자성은 읽는 쪽만 보호할 뿐 작성자끼리를 조정하지 않는다)
    public UniTask<bool> SaveAsync()
    {
        if (IsReadOnly)
        {
            Debug.LogWarning("[SaveService] 로드 실패 세션의 저장 요청을 차단했습니다");
            return UniTask.FromResult(false);
        }

        // Preserve: 반환분(호출자)과 다음 요청의 체인이 같은 UniTask를 함께 await 해야 한다.
        _pending = ChainSaveAsync(_pending).Preserve();
        return _pending;
    }

    public void DeleteAll()
    {
        if (IsReadOnly)
        {
            Debug.LogWarning("[SaveService] 로드 실패 세션의 삭제 요청을 차단했습니다");
            return;
        }

        _storage.DeleteAsync().Forget();
        Data = new SaveData();
        Debug.Log("[SaveService] 세이브 삭제");
    }

    // 앞 저장이 끝난 뒤 직렬화한다. Data는 실행 시점에 읽으므로 항상 최신 상태가 기록된다.
    private async UniTask<bool> ChainSaveAsync(UniTask<bool> previous)
    {
        // 앞 저장의 성패는 그 호출자가 이미 돌려받았다. 여기선 완료만 기다려 순서를 지키고,
        // 예외가 나더라도 체인이 영구히 오염되지 않게 흡수한다.
        try { await previous; }
        catch { /* 무시: 이 저장과는 무관한 앞 요청의 실패 */ }

        return await _storage.SaveAsync(Data);
    }
}
