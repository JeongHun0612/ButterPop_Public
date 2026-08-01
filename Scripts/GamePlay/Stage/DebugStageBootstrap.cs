using UnityEngine;

public class DebugStageBootstrap : MonoBehaviour
{
    [Header("테스트할 스테이지")]
    [SerializeField] private StageDataSO debugStageData;

    private void Start()
    {
        if (debugStageData == null)
        {
            Debug.LogWarning("[DebugStageBootstrap] debugStageData가 비어있습니다.");
            return;
        }

        if (StageManager.Instance == null)
        {
            Debug.LogError("[DebugStageBootstrap] StageManager가 씬에 없습니다.");
            return;
        }

        Debug.Log($"[DebugStageBootstrap] {debugStageData.stageName} 로드");
        StageManager.Instance.LoadStageData(debugStageData);
        StageManager.Instance.SetState(GameState.Ready);
    }
}
