using Cysharp.Threading.Tasks;

public interface ISaveStorage
{
    UniTask<SaveData> LoadAsync();
    UniTask<bool> SaveAsync(SaveData data);   // 기록 성공 시 true. 구현이 예외를 삼키므로 실패는 이 값으로만 드러난다
    UniTask DeleteAsync();
}
