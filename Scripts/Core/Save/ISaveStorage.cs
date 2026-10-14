using Cysharp.Threading.Tasks;

public interface ISaveStorage
{
    UniTask<SaveData> LoadAsync();           // 데이터가 없을 때만 null. 조회·역직렬화 실패는 예외로 전달한다
    UniTask<bool> SaveAsync(SaveData data);   // 기록 성공 시 true. 구현이 예외를 삼키므로 실패는 이 값으로만 드러난다
    UniTask DeleteAsync();
}
