using UnityEngine;

public class DebugStageBootstrap : MonoBehaviour
{
    [Header("스테이지 목록")]
    [SerializeField] private StageListSO stageList;

    [Header("테스트할 스테이지")]
    [SerializeField] private StageDataSO debugStageData;

    private void Start()
    {
        if (stageList == null || stageList.Count == 0)
        {
            Debug.LogWarning("[DebugStageBootstrap] stageList가 비어있습니다.");
            return;
        }

        if (!StageManager.TryGetInstance(out var stageManager))
        {
            Debug.LogError("[DebugStageBootstrap] StageManager가 씬에 없습니다.");
            return;
        }

        if (debugStageData == null)
        {
            Debug.Log("[DebugStageBootstrap] debugStageData 미지정 → 0번 스테이지 로드");
            stageManager.LoadStage(0);
            stageManager.ResetSquish();
            return;
        }

        int index = FindStageIndex(debugStageData);
        if (index < 0)
        {
            Debug.LogWarning($"[DebugStageBootstrap] {debugStageData.stageId}이 stageList에 없습니다. 직접 로드합니다.");
            stageManager.LoadStageData(debugStageData);
            stageManager.ResetSquish();
            return;
        }

        Debug.Log($"[DebugStageBootstrap] 스테이지 {index} 로드: {debugStageData.stageId}");
        stageManager.LoadStage(index);
        stageManager.ResetSquish();
    }

    private int FindStageIndex(StageDataSO data)
    {
        for (int i = 0; i < stageList.Count; i++)
        {
            if (stageList.Get(i) == data) return i;
        }
        return -1;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private Vector2 _scroll;
    private bool _showList = false;
    private GUIStyle _boxStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _currentButtonStyle;
    private bool _stylesInitialized;

    private void InitStyles()
    {
        if (_stylesInitialized) return;

        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 32,
            padding = new RectOffset(16, 16, 16, 16)
        };

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 34,
            richText = true
        };

        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 32,
            fixedHeight = 64
        };

        _currentButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 32,
            fixedHeight = 64,
            fontStyle = FontStyle.Bold
        };
        _currentButtonStyle.normal.textColor = Color.yellow;
        _currentButtonStyle.hover.textColor = Color.yellow;

        _stylesInitialized = true;
    }

    private void OnGUI()
    {
        if (!StageManager.TryGetInstance(out var stageManager) || stageList == null) return;

        InitStyles();
        float scale = Mathf.Max(Screen.width / 1080f, 1f);
        float topBarHeight = 110 * scale;
        float panelWidth = 360 * scale;
        float panelHeight = 500 * scale;

        // --- 상단 바: 화면 전체 가로 ---
        GUILayout.BeginArea(new Rect(0, 0, Screen.width, topBarHeight));
        GUILayout.BeginHorizontal(_boxStyle);

        string stageLabel = stageManager.CurrentStageData != null
            ? $"{stageManager.CurrentStageIndex}: {stageManager.CurrentStageData.stageId}"
            : $"{stageManager.CurrentStageIndex}";
        GUILayout.Label($"<b>[{stageManager.CurrentState}]</b>  {stageLabel}", _labelStyle);

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("리셋", _buttonStyle, GUILayout.Width(180 * scale), GUILayout.Height(72 * scale)))
        {
            stageManager.ResetSquish();
        }
        if (GUILayout.Button("다음", _buttonStyle, GUILayout.Width(180 * scale), GUILayout.Height(72 * scale)))
        {
            stageManager.LoadNextStage();
            stageManager.ResetSquish();
        }
        if (GUILayout.Button(_showList ? "목록 ▲" : "목록 ▼", _buttonStyle, GUILayout.Width(200 * scale), GUILayout.Height(72 * scale)))
            _showList = !_showList;

        GUILayout.EndHorizontal();
        GUILayout.EndArea();

        // --- 스테이지 목록 패널 ---
        if (_showList)
        {
            GUILayout.BeginArea(new Rect(16, topBarHeight + 4, panelWidth, panelHeight));
            GUILayout.BeginVertical(_boxStyle);

            float listHeight = 440 * scale;
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(listHeight));
            for (int i = 0; i < stageList.Count; i++)
            {
                var data = stageList.Get(i);
                string label = $"{i}: {data.stageId}";
                bool isCurrent = i == stageManager.CurrentStageIndex;

                if (GUILayout.Button(label, isCurrent ? _currentButtonStyle : _buttonStyle))
                {
                    stageManager.LoadStage(i);
                    stageManager.ResetSquish();
                }
            }
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
#endif
}
