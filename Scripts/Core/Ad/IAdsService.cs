using Cysharp.Threading.Tasks;

public interface IAdsService
{
    UniTask InitializeAsync();

    /// <summary>
    /// 리워드 광고를 재생한다. 시청을 완료하여 보상을 지급해야 하면 true를 반환한다.
    /// </summary>
    UniTask<bool> ShowRewardedAsync();
}
