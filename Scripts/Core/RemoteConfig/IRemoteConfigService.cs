using Cysharp.Threading.Tasks;

// 빌드 없이 조정하는 수치를 원격에서 받아 온다. 값을 받기 전이나 실패하면 호출자가 넘긴 기본값을 쓴다.
public interface IRemoteConfigService
{
    UniTask InitializeAsync();

    // 키가 없거나 형식이 맞지 않으면 fallback, min보다 작으면 min을 반환한다.
    int GetInt(string key, int fallback, int min = int.MinValue);
    float GetFloat(string key, float fallback, float min = float.MinValue);
}
