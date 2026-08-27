using System;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class FileSaveStorage : ISaveStorage
{
    private readonly string _path;
    private readonly string _tempPath;

    public FileSaveStorage(string fileName)
    {
        _path = Path.Combine(Application.persistentDataPath, fileName);
        _tempPath = _path + ".tmp";
    }

    public async UniTask<SaveData> LoadAsync()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            // 파일 읽기만 스레드풀에서, 역직렬화는 메인 스레드에서 (JsonUtility는 메인 스레드 전용)
            var json = await UniTask.RunOnThreadPool(() => File.ReadAllText(_path));
            return JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileSaveStorage] 로드 실패, 새 세이브로 폴백: {e.Message}");
            return null;
        }
    }

    // 호출자가 겹치지 않는다는 전제로 임시 파일 하나를 재사용한다(직렬화는 SaveService가 담당).
    public async UniTask<bool> SaveAsync(SaveData data)
    {
        try
        {
            var json = JsonUtility.ToJson(data);
            await UniTask.RunOnThreadPool(() =>
            {
                File.WriteAllText(_tempPath, json);

                if (File.Exists(_path))
                    File.Replace(_tempPath, _path, null);
                else
                    File.Move(_tempPath, _path);
            });

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileSaveStorage] 저장 실패: {e.Message}");
            return false;
        }
    }

    public async UniTask DeleteAsync()
    {
        await UniTask.RunOnThreadPool(() =>
        {
            if (File.Exists(_path)) File.Delete(_path);
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        });
    }
}
