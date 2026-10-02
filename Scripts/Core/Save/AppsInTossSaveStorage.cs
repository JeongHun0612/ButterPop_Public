#if APPS_IN_TOSS
using System;
using AppsInToss;
using Cysharp.Threading.Tasks;
using UnityEngine;

// System.IO와 스레드풀을 사용할 수 없는 WebGL용 저장소.
// SaveData 전체를 하나의 JSON 값으로 저장해 기존 마이그레이션 흐름을 그대로 재사용한다.
public sealed class AppsInTossSaveStorage : ISaveStorage
{
    private const int TimeoutMs = 10000;
    private readonly string _key;

    public AppsInTossSaveStorage(string key) => _key = key;

    public UniTask<SaveData> LoadAsync() => AppsInTossSaveLoader.LoadAsync(GetItemAsync);

    public async UniTask<bool> SaveAsync(SaveData data)
    {
        try
        {
            await AIT.StorageSetItem(_key, JsonUtility.ToJson(data), TimeoutMs);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[AppsInTossSaveStorage] 저장 실패: {e.Message}");
            return false;
        }
    }

    public async UniTask DeleteAsync()
    {
        try
        {
            await AIT.StorageRemoveItem(_key, TimeoutMs);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AppsInTossSaveStorage] 삭제 실패: {e.Message}");
        }
    }

    private async UniTask<string> GetItemAsync(int timeoutMs) => await AIT.StorageGetItem(_key, timeoutMs);
}
#endif
