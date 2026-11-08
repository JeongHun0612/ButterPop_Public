using System;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;
using UnityEngine;
using UgsRemoteConfig = Unity.Services.RemoteConfig.RemoteConfigService;

// UGS Remote Config 구현. SDK가 저장해 둔 마지막 응답을 먼저 쓰고, 새 응답을 받으면 교체한다.
public class UgsRemoteConfigService : IRemoteConfigService
{
    private struct UserAttributes { }
    private struct AppAttributes { }

    private RuntimeConfig _config;

    public async UniTask InitializeAsync()
    {
        try
        {
            await UnityServices.InitializeAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"[UgsRemoteConfigService] UnityServices 초기화 실패, 기본값을 사용합니다: {e.Message}");
            return;
        }

        UseConfig(UgsRemoteConfig.Instance.appConfig);

        // 오프라인이면 로그인·요청이 실패하므로 캐시 값을 유지한다.
        try
        {
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            UseConfig(await UgsRemoteConfig.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes()));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UgsRemoteConfigService] 원격 설정 요청 실패, 이전 값을 유지합니다: {e.Message}");
        }
    }

    public int GetInt(string key, int fallback, int min = int.MinValue) =>
        TryRead(key, out int value) ? ClampToMin(key, value, min) : fallback;

    public float GetFloat(string key, float fallback, float min = float.MinValue) =>
        TryRead(key, out float value) ? ClampToMin(key, value, min) : fallback;

    private void UseConfig(RuntimeConfig config)
    {
        if (config == null) return;

        _config = config;
        Debug.Log($"[UgsRemoteConfigService] 설정 적용 (origin {config.origin}): {config.config?.ToString(Formatting.None)}");
    }

    private bool TryRead<T>(string key, out T value)
    {
        value = default;
        var token = _config?.config?[key];
        if (token == null) return false;

        try
        {
            value = token.Value<T>();
            return true;
        }
        catch (Exception)
        {
            Debug.LogWarning($"[UgsRemoteConfigService] {key} 값 {token}을(를) {typeof(T).Name}(으)로 읽을 수 없어 기본값을 사용합니다");
            return false;
        }
    }

    private static T ClampToMin<T>(string key, T value, T min) where T : IComparable<T>
    {
        if (value.CompareTo(min) >= 0) return value;

        Debug.LogWarning($"[UgsRemoteConfigService] {key} 값 {value}이(가) 최솟값보다 작아 {min}으로 보정합니다");
        return min;
    }
}
