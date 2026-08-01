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

        if (StageManager.Instance == null)
        {
            Debug.LogError("[DebugStageBootstrap] StageManager가 씬에 없습니다.");
            return;
        }

        if (debugStageData == null)
        {
            Debug.Log("[DebugStageBootstrap] debugStageData 미지정 → 0번 스테이지 로드");
            StageManager.Instance.LoadStage(0);
            StageManager.Instance.ResetBanana();
            return;
        }

        int index = FindStageIndex(debugStageData);
        if (index < 0)
        {
            Debug.LogWarning($"[DebugStageBootstrap] {debugStageData.stageName}이 stageList에 없습니다. 직접 로드합니다.");
            StageManager.Instance.LoadStageData(debugStageData);
            StageManager.Instance.ResetBanana();
            return;
        }

        Debug.Log($"[DebugStageBootstrap] 스테이지 {index} 로드: {debugStageData.stageName}");
        StageManager.Instance.LoadStage(index);
        StageManager.Instance.ResetBanana();
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
    private bool _showList = true;
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
        if (StageManager.Instance == null || stageList == null) return;

        InitStyles();

        var sm = StageManager.Instance;
        float scale = Mathf.Max(Screen.width / 1080f, 1f);
        float topBarHeight = 110 * scale;
        float panelWidth = 360 * scale;
        float panelHeight = 500 * scale;

        // --- 상단 바: 화면 전체 가로 ---
        GUILayout.BeginArea(new Rect(0, 0, Screen.width, topBarHeight));
        GUILayout.BeginHorizontal(_boxStyle);

        string stageLabel = sm.CurrentStageData != null
            ? $"{sm.CurrentStageIndex}: {sm.CurrentStageData.stageName}"
            : $"{sm.CurrentStageIndex}";
        GUILayout.Label($"<b>[{sm.CurrentState}]</b>  {stageLabel}", _labelStyle);

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("리셋", _buttonStyle, GUILayout.Width(180 * scale), GUILayout.Height(72 * scale)))
        {
            sm.ResetBanana();
        }
        if (GUILayout.Button("다음", _buttonStyle, GUILayout.Width(180 * scale), GUILayout.Height(72 * scale)))
        {
            sm.LoadNextStage();
            sm.ResetBanana();
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
                string label = $"{i}: {data.stageName}";
                bool isCurrent = i == sm.CurrentStageIndex;

                if (GUILayout.Button(label, isCurrent ? _currentButtonStyle : _buttonStyle))
                {
                    sm.LoadStage(i);
                    sm.ResetBanana();
                }
            }
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
#endif
}
