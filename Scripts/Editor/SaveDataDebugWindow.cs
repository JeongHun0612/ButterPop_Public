using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 세이브 데이터(save.dat) 개발용 편집 창. 에디터 전용.
// 파일을 직접 읽고 쓰므로 플레이 정지 상태에서 사용을 권장한다(플레이 중엔 인메모리 데이터가 덮어쓴다).
public class SaveDataDebugWindow : EditorWindow
{
    private const string FileName = "save.dat";

    private SaveData _data;
    private Vector2 _scroll;
    private Vector2 _jsonScroll;
    private bool _showJson;

    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
    private static string TempPath => FilePath + ".tmp";
    private static bool FileExists => File.Exists(FilePath);

    [MenuItem("ButterPop/세이브/세이브 데이터 창")]
    private static void Open()
    {
        var window = GetWindow<SaveDataDebugWindow>("세이브 데이터");
        window.minSize = new Vector2(360f, 420f);
        window.Reload();
        window.Show();
    }

    [MenuItem("ButterPop/세이브/세이브 폴더 열기")]
    private static void OpenSaveFolder() => EditorUtility.RevealInFinder(Application.persistentDataPath);

    private void OnEnable() => Reload();

    private void OnGUI()
    {
        DrawHeader();

        if (_data == null)
        {
            EditorGUILayout.HelpBox("세이브 데이터를 불러오지 못했습니다. '새로고침'을 눌러 주세요.", MessageType.Warning);
            if (GUILayout.Button("새로고침")) Reload();
            return;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawMetaSection();
        DrawStageSection();
        DrawSkinSection();
        DrawPurchaseSection();
        DrawSettingsSection();
        DrawJsonPreview();

        EditorGUILayout.EndScrollView();

        DrawActionButtons();
    }

    private void Reload()
    {
        _data = Load();
        Repaint();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("세이브 데이터 편집", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(FilePath, EditorStyles.miniLabel);
        EditorGUILayout.LabelField(FileExists ? "상태: 파일 있음" : "상태: 파일 없음(새 세이브)", EditorStyles.miniLabel);

        if (Application.isPlaying)
            EditorGUILayout.HelpBox("플레이 중입니다. 여기서 파일을 바꿔도 인메모리 데이터가 덮어쓸 수 있습니다. 플레이 정지 후 사용을 권장합니다.", MessageType.Warning);

        EditorGUILayout.Space(4f);
    }

    private void DrawMetaSection()
    {
        DrawSectionTitle("메타");
        _data.version = EditorGUILayout.IntField("버전", _data.version);
    }

    private void DrawStageSection()
    {
        DrawSectionTitle("스테이지");
        _data.stage.highestClearedIndex = EditorGUILayout.IntField(
            new GUIContent("최고 클리어 인덱스", "-1 = 미클리어. 위치 기반이므로 StageList 순서 기준."),
            _data.stage.highestClearedIndex);
    }

    private void DrawSkinSection()
    {
        DrawSectionTitle("스킨");

        // 보유 목록은 세이브에 없다. 잠금 테스트는 "구매/광고"의 전체 광고 제거로.
        EditorGUILayout.LabelField("장착 (equipped)", EditorStyles.miniBoldLabel);
        var equipped = _data.skin.equipped;
        for (int i = 0; i < equipped.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            equipped[i].category = (SkinCategory)EditorGUILayout.EnumPopup(equipped[i].category, GUILayout.Width(90f));
            equipped[i].skinId = EditorGUILayout.TextField(equipped[i].skinId);
            if (GUILayout.Button("삭제", GUILayout.Width(48f)))
            {
                equipped.RemoveAt(i);
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("장착 항목 추가"))
            equipped.Add(new EquippedEntry { category = SkinCategory.Squish, skinId = "" });
    }

    private void DrawPurchaseSection()
    {
        DrawSectionTitle("구매/광고");
        _data.purchase.bannerRemoveAds = EditorGUILayout.Toggle("배너 광고 제거", _data.purchase.bannerRemoveAds);
        _data.purchase.allRemoveAds = EditorGUILayout.Toggle(
            new GUIContent("전체 광고 제거", "전면 포함 + 올스킨 언락"), _data.purchase.allRemoveAds);
    }

    private void DrawSettingsSection()
    {
        DrawSectionTitle("설정");
        _data.settings.bgmVolume = EditorGUILayout.Slider("BGM 볼륨", _data.settings.bgmVolume, 0f, 1f);
        _data.settings.sfxVolume = EditorGUILayout.Slider("SFX 볼륨", _data.settings.sfxVolume, 0f, 1f);
        _data.settings.vibration = EditorGUILayout.Toggle("진동", _data.settings.vibration);
        _data.settings.muted = EditorGUILayout.Toggle("음소거", _data.settings.muted);
    }

    private void DrawJsonPreview()
    {
        EditorGUILayout.Space(6f);
        _showJson = EditorGUILayout.Foldout(_showJson, "JSON 미리보기(현재 편집 중인 값)", true);
        if (!_showJson) return;

        var json = JsonUtility.ToJson(_data, true);
        _jsonScroll = EditorGUILayout.BeginScrollView(_jsonScroll, GUILayout.Height(140f));
        var height = EditorStyles.textArea.CalcHeight(new GUIContent(json), EditorGUIUtility.currentViewWidth - 40f);
        EditorGUILayout.SelectableLabel(json, EditorStyles.textArea, GUILayout.ExpandHeight(true), GUILayout.Height(height));
        EditorGUILayout.EndScrollView();
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("새로고침(파일에서 로드)")) Reload();
        if (GUILayout.Button("저장(파일에 쓰기)")) Save();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("전체 삭제(리셋)")) DeleteAll();
        if (GUILayout.Button("폴더 열기")) OpenSaveFolder();
        if (GUILayout.Button("로그 출력")) LogContents();
        EditorGUILayout.EndHorizontal();
    }

    private void Save()
    {
        Write(_data);
        Debug.Log("[SaveDataDebugWindow] 세이브 파일에 저장");
    }

    private void DeleteAll()
    {
        if (!EditorUtility.DisplayDialog("세이브 삭제", "저장 데이터를 전부 삭제할까요?", "삭제", "취소")) return;

        if (FileExists) File.Delete(FilePath);
        if (File.Exists(TempPath)) File.Delete(TempPath);

        _data = new SaveData();
        Debug.Log("[SaveDataDebugWindow] 세이브 전체 삭제");
        WarnIfPlaying();
        Repaint();
    }

    private void LogContents()
    {
        if (!FileExists)
        {
            Debug.Log("[SaveDataDebugWindow] 세이브 파일 없음(새 세이브 상태)");
            return;
        }
        Debug.Log($"[SaveDataDebugWindow] {FilePath}\n{File.ReadAllText(FilePath)}");
    }

    private static SaveData Load()
    {
        if (!FileExists) return new SaveData();
        try
        {
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath)) ?? new SaveData();
            return EnsureValid(data);
        }
        catch
        {
            Debug.LogWarning("[SaveDataDebugWindow] 세이브 파싱 실패, 기본값으로 표시");
            return new SaveData();
        }
    }

    private static void Write(SaveData data)
    {
        File.WriteAllText(FilePath, JsonUtility.ToJson(data));
        WarnIfPlaying();
    }

    // JsonUtility가 null로 채운 하위 객체/리스트를 방어적으로 보정한다.
    private static SaveData EnsureValid(SaveData data)
    {
        data.stage ??= new StageSaveData();
        data.skin ??= new SkinSaveData();
        data.skin.equipped ??= new List<EquippedEntry>();
        data.purchase ??= new PurchaseSaveData();
        data.settings ??= new SettingsSaveData();
        return data;
    }

    private static void WarnIfPlaying()
    {
        if (Application.isPlaying)
            Debug.LogWarning("[SaveDataDebugWindow] 플레이 중 변경은 인메모리 데이터에 덮어써질 수 있습니다. 플레이 정지 후 사용을 권장합니다.");
    }

    private static void DrawSectionTitle(string title)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }
}
