using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 앱인토스 Storage 조회를 재시도하고 응답을 세이브로 해석한다.
// 브릿지 호출을 주입받으므로 APPS_IN_TOSS 디파인이 없는 빌드 타깃에서도 테스트할 수 있다.
public static class AppsInTossSaveLoader
{
    private const int TimeoutMs = 2000;
    private const int Attempts = 3;
    private const int RetryDelayMs = 250;

    // 데이터가 없으면 null. 조회가 끝내 실패하거나 값이 손상됐으면 예외를 던진다.
    public static async UniTask<SaveData> LoadAsync(Func<int, UniTask<string>> getItem)
    {
        string json = null;
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            try
            {
                json = await getItem(TimeoutMs);
                break;
            }
            catch (Exception e)
            {
                if (attempt == Attempts - 1) throw;

                Debug.LogWarning($"[AppsInTossSaveLoader] 조회 실패, 재시도 {attempt + 1}/{Attempts - 1}: {e.Message}");
                await UniTask.Delay(RetryDelayMs * (attempt + 1), DelayType.Realtime);
            }
        }

        // SDK 브릿지는 키가 없을 때 JS null을 JSON.stringify한 문자열 "null"을 그대로 넘긴다.
        // 게임은 항상 JSON 객체를 저장하므로 "null"·빈 값은 데이터 부재로 본다.
        if (string.IsNullOrWhiteSpace(json) || json == "null") return null;

        var data = JsonUtility.FromJson<SaveData>(json);
        if (data == null) throw new FormatException("세이브 값을 역직렬화할 수 없습니다");
        return data;
    }
}
