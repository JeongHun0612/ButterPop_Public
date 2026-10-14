using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;

public class DebugStageBootstrap : MonoBehaviour
{
    [Header("스테이지 목록")]
    [SerializeField] private StageListSO stageList;

    [Header("스킨 목록")]
    [SerializeField] private SkinDatabaseSO skinDatabase;

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        PreloadLocalizedNamesAsync().Forget();
#endif

        if (debugStageData == null)
        {
            Debug.Log("[DebugStageBootstrap] debugStageData 미지정 → 0번 스테이지 로드");
            LoadStageForDebug(stageManager, 0);
            return;
        }

        int index = FindStageIndex(debugStageData);
        if (index < 0)
        {
            Debug.LogWarning($"[DebugStageBootstrap] {debugStageData.stageId}이 stageList에 없습니다. 직접 로드합니다.");
            LoadStageDataForDebug(stageManager, debugStageData);
            return;
        }

        Debug.Log($"[DebugStageBootstrap] 스테이지 {index} 로드: {debugStageData.stageId}");
        LoadStageForDebug(stageManager, index);
    }

    private int FindStageIndex(StageDataSO data)
    {
        for (int i = 0; i < stageList.Count; i++)
        {
            if (stageList.Get(i) == data) return i;
        }
        return -1;
    }

    private static void LoadStageForDebug(StageManager stageManager, int index)
    {
        stageManager.LoadStage(index);
        stageManager.SetState(GameState.Ready);
    }

    private static void LoadStageDataForDebug(StageManager stageManager, StageDataSO data)
    {
        stageManager.LoadStageData(data);
        stageManager.SetState(GameState.Ready);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private Vector2 _scroll;
    private Vector2 _skinScroll;
    private bool _showList = false;
    private bool _showSkinList = false;
    private SquishSkinSO _debugSkin;
    private string[] _localizedStageNames;
    private string[] _localizedSkinNames;
    private GUIStyle _boxStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _currentButtonStyle;
    private GUIStyle _listButtonStyle;
    private GUIStyle _currentListButtonStyle;
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

        // 목록 버튼은 텍스트를 좌측 정렬해 스테이지 번호를 세로로 맞춘다
        _listButtonStyle = new GUIStyle(_buttonStyle)
        {
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(16, 8, 0, 0)
        };

        _currentListButtonStyle = new GUIStyle(_currentButtonStyle)
        {
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(16, 8, 0, 0)
        };

        _stylesInitialized = true;
    }

    private void OnGUI()
    {
        if (!StageManager.TryGetInstance(out var stageManager) || stageList == null) return;

        InitStyles();
        float scale = Mathf.Max(Screen.width / 1080f, 1f);
        float topBarHeight = 190 * scale;
        float panelWidth = 360 * scale;
        float panelHeight = 500 * scale;

        // --- 상단 바: 화면 전체 가로 ---
        GUILayout.BeginArea(new Rect(0, 0, Screen.width, topBarHeight));
        GUILayout.BeginVertical(_boxStyle);
        GUILayout.BeginHorizontal();

        string stageLabel = stageManager.CurrentStageData != null
            ? $"{stageManager.CurrentStageIndex}: {stageManager.CurrentStageData.stageId}"
            : $"{stageManager.CurrentStageIndex}";
        GUILayout.Label($"<b>[{stageManager.CurrentState}]</b>  {stageLabel}", _labelStyle);

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("리셋", _buttonStyle, GUILayout.Width(180 * scale), GUILayout.Height(72 * scale)))
        {
            stageManager.ResetSquish();
        }
        bool guiEnabled = GUI.enabled;
        GUI.enabled = stageManager.CurrentStageIndex > 0;
        if (GUILayout.Button("이전", _buttonStyle, GUILayout.Width(180 * scale), GUILayout.Height(72 * scale)))
        {
            LoadStageForDebug(stageManager, stageManager.CurrentStageIndex - 1);
            ApplyDebugSkin(stageManager);
        }
        GUI.enabled = guiEnabled;
        if (GUILayout.Button("다음", _buttonStyle, GUILayout.Width(180 * scale), GUILayout.Height(72 * scale)))
        {
            stageManager.LoadNextStage();
            ApplyDebugSkin(stageManager);
            stageManager.SetState(GameState.Ready);
        }

        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button(_showList ? "목록 ▲" : "목록 ▼", _buttonStyle, GUILayout.Width(200 * scale), GUILayout.Height(72 * scale)))
        {
            _showList = !_showList;
            if (_showList) _showSkinList = false;
        }
        if (GUILayout.Button(_showSkinList ? "스킨 ▲" : "스킨 ▼", _buttonStyle, GUILayout.Width(200 * scale), GUILayout.Height(72 * scale)))
        {
            _showSkinList = !_showSkinList;
            if (_showSkinList) _showList = false;
        }

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
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
                string label = $"{i}: {GetStageDisplayName(i, data)}";
                bool isCurrent = i == stageManager.CurrentStageIndex;

                if (GUILayout.Button(label, isCurrent ? _currentListButtonStyle : _listButtonStyle))
                {
                    LoadStageForDebug(stageManager, i);
                    ApplyDebugSkin(stageManager);
                }
            }
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        if (_showSkinList)
        {
            GUILayout.BeginArea(new Rect(16, topBarHeight + 4, panelWidth, panelHeight));
            GUILayout.BeginVertical(_boxStyle);

            float listHeight = 440 * scale;
            _skinScroll = GUILayout.BeginScrollView(_skinScroll, GUILayout.Height(listHeight));

            var skinTab = skinDatabase != null ? skinDatabase.GetTab(SkinCategory.Squish) : null;
            if (skinTab == null)
            {
                GUILayout.Label("스킨 데이터가 없습니다.", _labelStyle);
            }
            else
            {
                for (int i = 0; i < skinTab.Count; i++)
                {
                    if (skinTab.skins[i] is not SquishSkinSO skin) continue;

                    string skinName = GetSkinDisplayName(i, skin);
                    bool isCurrent = stageManager.Squish != null && stageManager.Squish.CurrentSkin == skin;

                    if (GUILayout.Button($"{i}: {skinName}", isCurrent ? _currentListButtonStyle : _listButtonStyle))
                    {
                        _debugSkin = skin;
                        ApplyDebugSkin(stageManager);
                    }
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }

    // Keep the temporary debug skin across stage changes without altering saved equipment.
    private void ApplyDebugSkin(StageManager stageManager)
    {
        if (_debugSkin != null && stageManager.Squish != null)
            stageManager.Squish.ApplySkin(_debugSkin);
    }

    private async UniTaskVoid PreloadLocalizedNamesAsync()
    {
        var stageNameTasks = new UniTask<string>[stageList.Count];
        for (int i = 0; i < stageList.Count; i++)
        {
            var data = stageList.Get(i);
            stageNameTasks[i] = data != null
                ? GetLocalizedNameAsync(data.LocalizedName, data.stageId)
                : UniTask.FromResult(string.Empty);
        }

        var skinTab = skinDatabase != null ? skinDatabase.GetTab(SkinCategory.Squish) : null;
        int skinCount = skinTab != null ? skinTab.Count : 0;
        var skinNameTasks = new UniTask<string>[skinCount];
        for (int i = 0; i < skinCount; i++)
        {
            var skin = skinTab.skins[i];
            skinNameTasks[i] = skin != null
                ? GetLocalizedNameAsync(skin.LocalizedName, skin.id)
                : UniTask.FromResult(string.Empty);
        }

        var names = await UniTask.WhenAll(
            UniTask.WhenAll(stageNameTasks),
            UniTask.WhenAll(skinNameTasks));

        if (this == null) return;
        _localizedStageNames = names.Item1;
        _localizedSkinNames = names.Item2;
    }

    private string GetStageDisplayName(int index, StageDataSO data)
    {
        if (_localizedStageNames != null && index < _localizedStageNames.Length
            && !string.IsNullOrEmpty(_localizedStageNames[index]))
            return _localizedStageNames[index];

        return data != null ? data.stageId : string.Empty;
    }

    private string GetSkinDisplayName(int index, SkinDataSO skin)
    {
        if (_localizedSkinNames != null && index < _localizedSkinNames.Length
            && !string.IsNullOrEmpty(_localizedSkinNames[index]))
            return _localizedSkinNames[index];

        return skin != null ? skin.id : string.Empty;
    }

    private static async UniTask<string> GetLocalizedNameAsync(LocalizedString localizedName, string fallback)
    {
        if (localizedName == null || localizedName.IsEmpty) return fallback;
        return await localizedName.GetLocalizedStringAsync();
    }
#endif
}
