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
        // File.Exists는 접근 오류도 false로 반환하므로, 실제 읽기 결과로 파일 부재를 구분한다.
        string json;
        try
        {
            json = await UniTask.RunOnThreadPool(() => File.ReadAllText(_path));
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }

        if (string.IsNullOrWhiteSpace(json))
            throw new FormatException("세이브 파일이 비어 있습니다");

        var data = JsonUtility.FromJson<SaveData>(json);
        if (data == null) throw new FormatException("세이브 파일을 역직렬화할 수 없습니다");
        return data;
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
