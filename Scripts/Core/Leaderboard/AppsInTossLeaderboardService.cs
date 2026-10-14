using System;
using System.Globalization;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class AppsInTossLeaderboardService : ILeaderboardService
{
    private const string Success = "SUCCESS";
    private readonly ISaveService _save;
    private readonly IAppsInTossLeaderboardApi _api;
    private readonly int _stageCount;
    private readonly bool _enabled;

    private bool _supportKnown;
    private bool _supported;
    private bool _checkingSupport;
    private bool _submitting;
    private UniTask<bool> _supportTask;

    public bool IsAvailable => _enabled && (!_supportKnown || _supported);

    public AppsInTossLeaderboardService(ISaveService save, IAppsInTossLeaderboardApi api,
        int stageCount, bool debugUnlock)
    {
        _save = save;
        _api = api;
        _stageCount = stageCount;
        _enabled = save != null && api != null && stageCount > 0 && !debugUnlock;

        if (save == null || api == null || stageCount <= 0)
            Debug.LogError("[AppsInTossLeaderboardService] 세이브·SDK·스테이지 목록 의존성이 필요합니다");
    }

    public async UniTask InitializeAsync()
    {
        if (!IsAvailable) return;

        await EnsureSupportedAsync();
    }

    public void SubmitHighestStage(int stageIndex)
    {
        if (!IsAvailable || _save.IsReadOnly) return;
        if (!LeaderboardScore.TryFromStageIndex(stageIndex, _stageCount, out _))
        {
            Debug.LogWarning($"[AppsInTossLeaderboardService] 제출 범위를 벗어난 스테이지: {stageIndex}");
            return;
        }
        if (_save.Data?.stage == null || stageIndex > _save.Data.stage.highestClearedIndex)
        {
            Debug.LogError("[AppsInTossLeaderboardService] 저장된 진행도를 초과하는 제출을 차단했습니다");
            return;
        }
        if (_submitting) return;

        SubmitAsync().Forget();
    }

    public void ShowLeaderboard()
    {
        if (!IsAvailable) return;

        OpenAsync().Forget();
    }

    private async UniTaskVoid OpenAsync()
    {
        try
        {
            await _api.OpenAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AppsInTossLeaderboardService] 순위 화면 열기 응답 오류: {e.Message}");
        }
    }

    private UniTask<bool> EnsureSupportedAsync()
    {
        if (_supportKnown) return UniTask.FromResult(_supported);
        if (_checkingSupport) return _supportTask;

        _checkingSupport = true;
        _supportTask = CheckSupportAsync().ToAsyncLazy().Task;
        return _supportTask;
    }

    private async UniTask<bool> CheckSupportAsync()
    {
        try
        {
            _supported = await _api.IsSupportedAsync();
            _supportKnown = true;
            if (!_supported)
                Debug.LogWarning("[AppsInTossLeaderboardService] 현재 토스앱은 리더보드를 지원하지 않습니다");
            return _supported;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AppsInTossLeaderboardService] 지원 여부 조회 실패: {e.Message}");
            return false;
        }
        finally { _checkingSupport = false; }
    }

    // 저장이 끝난 최고 기록(1부터 시작하는 스테이지 번호)을 한 번 올린다.
    // 실패한 제출은 기록이 오르거나 다음 실행 때 다시 올린다.
    private async UniTaskVoid SubmitAsync()
    {
        _submitting = true;
        try
        {
            if (!await EnsureSupportedAsync()) return;

            int index = _save.Data?.stage?.highestClearedIndex ?? -1;
            if (!LeaderboardScore.TryFromStageIndex(index, _stageCount, out long score))
            {
                Debug.LogWarning($"[AppsInTossLeaderboardService] 저장된 진행도가 스테이지 범위를 벗어났습니다: {index}");
                return;
            }
            if (!await _save.SaveAsync())
            {
                Debug.LogWarning("[AppsInTossLeaderboardService] 진행도 저장 실패로 제출을 보류합니다");
                return;
            }

            string status = await _api.SubmitScoreAsync(score.ToString(CultureInfo.InvariantCulture));
            if (status != Success)
            {
                // 프로필 없음·리더보드 미승인 등.
                Debug.LogWarning($"[AppsInTossLeaderboardService] 점수 제출 실패: {status ?? "응답 없음"}");
                return;
            }

            Debug.Log($"[AppsInTossLeaderboardService] 진행도 제출 완료: 스테이지 {score}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AppsInTossLeaderboardService] 점수 제출 통신 실패: {e.Message}");
        }
        finally { _submitting = false; }
    }
}
