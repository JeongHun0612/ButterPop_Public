using Cysharp.Threading.Tasks;
using UnityEngine;

public class AdsService : IAdsService
{
    public async UniTask InitializeAsync()
    {
        // TODO(광고 SDK): 리워드 광고 초기화 및 사전 로드
        await UniTask.CompletedTask;
    }

    public async UniTask<bool> ShowRewardedAsync()
    {
        // TODO(광고 SDK): 리워드 광고 재생. 시청 완료 시 true, 중도 이탈/로드 실패 시 false 반환.
        Debug.Log("[AdsService] 리워드 광고 재생(스텁): 즉시 보상 지급");
        await UniTask.CompletedTask;
        return true;
    }
}
