using Cysharp.Threading.Tasks;

public interface ISaveStorage
{
    UniTask<SaveData> LoadAsync();
    UniTask SaveAsync(SaveData data);
    UniTask DeleteAsync();
}
