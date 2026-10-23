using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// "{번호}_{한글 이름}/{이름}_누끼{N}.png" 형식의 원본 폴더를 읽어 스프라이트·프리팹·StageData·로컬라이즈 키를 한 번에 만든다.
public class StageBatchImporterWindow : EditorWindow
{
    private class SourceEntry
    {
        public int number;
        public string folderName;
        public string koName;
        public string id;
        public string enName;
        // 첫 번째가 스테이지에 쓰는 스프라이트, 나머지는 교체 후보로 이미지 폴더에만 보관한다
        public List<string> imagePaths = new();
        public bool selected = true;
    }

    private const string SourceFolderPrefsKey = "ButterPop.StageBatchImporter.SourceFolder";
    private const string ManifestFileName = "stage_manifest.csv";
    private const string ImageRootFolder = "Assets/04. Images/StageObjects";
    private const string StageDataFolder = "Assets/08. ScriptableObjects/StageDatas/Stages";
    private const string StageListPath = "Assets/08. ScriptableObjects/StageDatas/StageList.asset";
    private const string StageTableName = "Stage";
    private const string TestScenePath = "Assets/01. Scenes/StageMaker.unity";
    private const string StageVariantSuffix = "_01";
    private const float PixelsPerUnit = 100f;
    private const float ThumbnailSize = 56f;

    private static readonly Color SelectedThumbnailColor = new(0.3f, 0.8f, 0.3f, 0.6f);
    private static readonly Color ThumbnailBackgroundColor = new(0f, 0f, 0f, 0.2f);

    // 기존 스테이지의 오브젝트 배치 평균값. 스퀴시 낙하 지점의 오른쪽 아래에 놓인다.
    private static readonly Vector2 DefaultObjectPosition = new(1.3f, -0.5f);

    private static readonly Regex FolderPattern = new(@"^(\d+)_(.+)$");
    private static readonly Regex NukkiIndexPattern = new(@"누끼(\d+)");
    private static readonly Regex IdPattern = new(@"^[A-Z][A-Za-z0-9]*$");

    private string _sourceFolder;
    private readonly List<SourceEntry> _entries = new();
    // 원본 이미지는 프로젝트 밖에 있어 에셋 프리뷰를 쓸 수 없으므로 파일에서 직접 읽어 캐싱한다.
    private readonly Dictionary<string, Texture2D> _thumbnails = new();
    private int _maxImageCount;
    private bool _addToStageList = true;
    private float _targetWorldSize = 3f;
    private Vector2 _scrollPos;

    [MenuItem("ButterPop/Stage Batch Importer")]
    public static void Open()
    {
        var window = GetWindow<StageBatchImporterWindow>("Stage Batch Importer");
        window.minSize = new Vector2(760, 480);
    }

    private void OnEnable()
    {
        _sourceFolder = EditorPrefs.GetString(SourceFolderPrefsKey, string.Empty);
        if (Directory.Exists(_sourceFolder)) Scan();
    }

    private void OnDisable() => ClearThumbnails();

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        DrawSourceSection();
        EditorGUILayout.Space(4);
        DrawOptions();
        EditorGUILayout.Space(4);
        DrawEntries();
        EditorGUILayout.Space(6);
        DrawBottomButtons();
        EditorGUILayout.Space(4);
    }

    #region GUI

    private void DrawSourceSection()
    {
        EditorGUILayout.LabelField("원본 폴더", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        _sourceFolder = EditorGUILayout.TextField(_sourceFolder);
        if (EditorGUI.EndChangeCheck())
            EditorPrefs.SetString(SourceFolderPrefsKey, _sourceFolder);

        if (GUILayout.Button("찾기", GUILayout.Width(50)))
        {
            string picked = EditorUtility.OpenFolderPanel("원본 이미지 폴더", _sourceFolder, string.Empty);
            if (!string.IsNullOrEmpty(picked))
            {
                _sourceFolder = picked;
                EditorPrefs.SetString(SourceFolderPrefsKey, _sourceFolder);
                Scan();
            }
        }

        if (GUILayout.Button("다시 읽기", GUILayout.Width(70))) Scan();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            $"하위 폴더 \"번호_한글이름\"마다 스테이지 1개를 만듭니다. \"번호_한글이름\" 폴더 하나를 고르면 그 스테이지만 읽습니다. 영문 ID·영문 이름은 {ManifestFileName}에서 읽고, 수정 후 매니페스트 저장으로 남길 수 있습니다.",
            MessageType.None);
    }

    private void DrawOptions()
    {
        EditorGUILayout.LabelField("생성 옵션", EditorStyles.boldLabel);
        _targetWorldSize = EditorGUILayout.FloatField(
            new GUIContent("기본 크기 (월드 단위)", "새 StageData의 오브젝트 Scale을 긴 변이 이 크기가 되도록 계산합니다."),
            _targetWorldSize);
        _addToStageList = EditorGUILayout.Toggle(
            new GUIContent("StageList 끝에 추가", "새로 만든 StageData를 번호 순서대로 StageList 끝에 추가합니다."),
            _addToStageList);
    }

    private void DrawEntries()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"스테이지 ({_entries.Count})", EditorStyles.boldLabel);
        if (GUILayout.Button("전체 선택", GUILayout.Width(70))) SetAllSelected(true);
        if (GUILayout.Button("신규만", GUILayout.Width(60)))
        {
            foreach (var entry in _entries) entry.selected = LoadStageData(entry) == null;
        }
        if (GUILayout.Button("전체 해제", GUILayout.Width(70))) SetAllSelected(false);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Space(22);
        GUILayout.Label("번호", GUILayout.Width(36));
        GUILayout.Label("한글 이름", GUILayout.Width(120));
        GUILayout.Label("영문 ID", GUILayout.Width(150));
        GUILayout.Label("영문 이름", GUILayout.Width(150));
        GUILayout.Label("이미지 (클릭해 사용할 이미지 선택)", GUILayout.Width(GetThumbnailAreaWidth()));
        GUILayout.Label("상태");
        EditorGUILayout.EndHorizontal();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
        foreach (var entry in _entries) DrawEntry(entry);
        EditorGUILayout.EndScrollView();
    }

    private void DrawEntry(SourceEntry entry)
    {
        var stageData = LoadStageData(entry);

        EditorGUILayout.BeginHorizontal(GUILayout.Height(ThumbnailSize + 4f));
        entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(18));
        GUILayout.Label(entry.number.ToString(), GUILayout.Width(36));
        entry.koName = EditorGUILayout.TextField(entry.koName, GUILayout.Width(120));
        entry.id = EditorGUILayout.TextField(entry.id, GUILayout.Width(150));
        entry.enName = EditorGUILayout.TextField(entry.enName, GUILayout.Width(150));
        DrawThumbnails(entry);

        if (!IsValidId(entry.id))
            GUILayout.Label("영문 ID 필요 (PascalCase)", EditorStyles.miniLabel);
        else if (stageData == null)
            GUILayout.Label("신규", EditorStyles.miniLabel);
        else
        {
            GUILayout.Label("생성됨", EditorStyles.miniLabel);
            if (GUILayout.Button("Stage Maker", GUILayout.Width(90))) StageMakerWindow.Open(stageData);
            if (GUILayout.Button("플레이", GUILayout.Width(50))) PlayStage(stageData);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    // 첫 칸(초록 테두리)이 스테이지에 쓰는 이미지다. 다른 칸을 누르면 그 이미지를 첫 칸으로 옮긴다.
    private void DrawThumbnails(SourceEntry entry)
    {
        for (int i = 0; i < _maxImageCount; i++)
        {
            var rect = GUILayoutUtility.GetRect(ThumbnailSize, ThumbnailSize,
                GUILayout.Width(ThumbnailSize), GUILayout.Height(ThumbnailSize));
            if (i >= entry.imagePaths.Count) continue;

            string path = entry.imagePaths[i];
            EditorGUI.DrawRect(rect, i == 0 ? SelectedThumbnailColor : ThumbnailBackgroundColor);

            var texture = GetThumbnail(path);
            if (texture != null)
            {
                var inner = new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f);
                GUI.DrawTexture(inner, texture, ScaleMode.ScaleToFit);
            }

            var content = new GUIContent(string.Empty, Path.GetFileName(path));
            if (GUI.Button(rect, content, GUIStyle.none) && i > 0)
            {
                entry.imagePaths.RemoveAt(i);
                entry.imagePaths.Insert(0, path);
            }
        }
    }

    private float GetThumbnailAreaWidth() => Mathf.Max(1, _maxImageCount) * (ThumbnailSize + 4f);

    private Texture2D GetThumbnail(string path)
    {
        if (_thumbnails.TryGetValue(path, out var cached)) return cached;

        var texture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
        if (!File.Exists(path) || !texture.LoadImage(File.ReadAllBytes(path)))
        {
            DestroyImmediate(texture);
            texture = null;
        }

        _thumbnails[path] = texture;
        return texture;
    }

    private void ClearThumbnails()
    {
        foreach (var texture in _thumbnails.Values)
        {
            if (texture != null) DestroyImmediate(texture);
        }
        _thumbnails.Clear();
    }

    private void DrawBottomButtons()
    {
        int count = _entries.Count(e => e.selected && IsValidId(e.id));

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("매니페스트 저장", GUILayout.Height(30), GUILayout.Width(120))) SaveManifest();

        GUI.enabled = count > 0;
        var prevColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.4f, 1f, 0.4f);
        if (GUILayout.Button($"선택한 {count}개 임포트", GUILayout.Height(30))) Import();
        GUI.backgroundColor = prevColor;
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    private void SetAllSelected(bool selected)
    {
        foreach (var entry in _entries) entry.selected = selected;
    }

    #endregion

    #region Scan / Manifest

    private void Scan()
    {
        _entries.Clear();
        _maxImageCount = 0;
        ClearThumbnails();
        if (!Directory.Exists(_sourceFolder))
        {
            Debug.LogWarning($"[StageBatchImporter] 원본 폴더가 없습니다: {_sourceFolder}");
            return;
        }

        var manifest = ReadManifest();

        var stageDirs = IsStageFolder(_sourceFolder)
            ? new[] { _sourceFolder }
            : Directory.GetDirectories(_sourceFolder);

        foreach (string dir in stageDirs)
        {
            string folderName = Path.GetFileName(dir);
            var match = FolderPattern.Match(folderName);
            if (!match.Success) continue;

            var images = Directory.GetFiles(dir, "*.png")
                .OrderBy(GetImagePriority)
                .ThenBy(Path.GetFileName, StringComparer.Ordinal)
                .ToList();
            if (images.Count == 0) continue;

            int number = int.Parse(match.Groups[1].Value);
            var entry = new SourceEntry
            {
                number = number,
                folderName = folderName,
                koName = Regex.Replace(match.Groups[2].Value, @"\(.*?\)", string.Empty).Trim(),
                imagePaths = images
            };

            if (manifest.TryGetValue(number, out var row))
            {
                entry.id = row.id;
                if (!string.IsNullOrEmpty(row.koName)) entry.koName = row.koName;
                entry.enName = row.enName;

                string chosen = images.FirstOrDefault(p => Path.GetFileName(p) == row.image);
                if (chosen != null)
                {
                    images.Remove(chosen);
                    images.Insert(0, chosen);
                }
            }

            _maxImageCount = Mathf.Max(_maxImageCount, images.Count);
            _entries.Add(entry);
        }

        _entries.Sort((a, b) => a.number.CompareTo(b.number));
        Repaint();
    }

    private static bool IsStageFolder(string path) => FolderPattern.IsMatch(Path.GetFileName(path));

    // 스테이지 폴더 하나를 고른 경우 매니페스트는 상위 폴더에서 찾는다.
    private string GetManifestFolder()
        => IsStageFolder(_sourceFolder) ? Path.GetDirectoryName(_sourceFolder) : _sourceFolder;

    // 파일명에 "사용"이 들어간 이미지를 가장 먼저, 그다음은 누끼 번호 순으로 기본 스프라이트를 고른다.
    private static int GetImagePriority(string path)
    {
        string fileName = Path.GetFileNameWithoutExtension(path);
        if (fileName.Contains("사용")) return -1;

        var match = NukkiIndexPattern.Match(fileName);
        return match.Success ? int.Parse(match.Groups[1].Value) : int.MaxValue;
    }

    // 열: number,id,ko,en,image. image(사용할 원본 파일명)는 비어 있으면 기본 우선순위를 따른다.
    private Dictionary<int, (string id, string koName, string enName, string image)> ReadManifest()
    {
        var result = new Dictionary<int, (string, string, string, string)>();
        string path = Path.Combine(GetManifestFolder(), ManifestFileName);
        if (!File.Exists(path)) return result;

        foreach (string line in File.ReadAllLines(path, Encoding.UTF8).Skip(1))
        {
            var cols = line.Split(',');
            if (cols.Length < 4 || !int.TryParse(cols[0].Trim(), out int number)) continue;
            string image = cols.Length > 4 ? cols[4].Trim() : string.Empty;
            result[number] = (cols[1].Trim(), cols[2].Trim(), cols[3].Trim(), image);
        }
        return result;
    }

    private void SaveManifest()
    {
        if (!Directory.Exists(_sourceFolder)) return;

        // 지금 목록에 없는 스테이지 줄(폴더 하나만 연 경우 등)은 그대로 남긴다.
        var rows = ReadManifest();
        foreach (var entry in _entries)
            rows[entry.number] = (entry.id, entry.koName, entry.enName, Path.GetFileName(entry.imagePaths[0]));

        var sb = new StringBuilder("number,id,ko,en,image\n");
        foreach (var pair in rows.OrderBy(p => p.Key))
            sb.Append($"{pair.Key},{pair.Value.id},{pair.Value.koName},{pair.Value.enName},{pair.Value.image}\n");

        string path = Path.Combine(GetManifestFolder(), ManifestFileName);
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        Debug.Log($"[StageBatchImporter] 매니페스트 저장: {path}");
    }

    #endregion

    #region Import

    private void Import()
    {
        var targets = _entries.Where(e => e.selected && IsValidId(e.id)).ToList();
        var duplicated = targets.GroupBy(e => e.id).FirstOrDefault(g => g.Count() > 1);
        if (duplicated != null)
        {
            EditorUtility.DisplayDialog("영문 ID 중복", $"'{duplicated.Key}' ID가 여러 스테이지에 지정됐습니다.", "확인");
            return;
        }

        var newImages = new List<string>();
        var updatedImages = new List<string>();
        var created = new List<string>();

        try
        {
            for (int i = 0; i < targets.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Stage Batch Importer", $"이미지 복사: {targets[i].id}", (float)i / targets.Count);
                CopyImages(targets[i], newImages, updatedImages);
            }

            AssetDatabase.Refresh();
            foreach (string assetPath in newImages) ConfigureSpriteImporter(assetPath);

            var stageList = AssetDatabase.LoadAssetAtPath<StageListSO>(StageListPath);
            if (_addToStageList && stageList == null)
                Debug.LogError($"[StageBatchImporter] StageList를 찾지 못했습니다: {StageListPath}");

            for (int i = 0; i < targets.Count; i++)
            {
                var entry = targets[i];
                EditorUtility.DisplayProgressBar("Stage Batch Importer", $"에셋 생성: {entry.id}", (float)i / targets.Count);

                string spritePath = GetImageAssetPath(entry.id, 0);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite == null)
                {
                    Debug.LogError($"[StageBatchImporter] 스프라이트를 불러오지 못했습니다: {spritePath}");
                    continue;
                }

                var prefab = StageMakerWindow.GetOrCreatePrefab(sprite);
                var stageData = LoadStageData(entry);
                if (stageData == null)
                {
                    stageData = CreateStageData(entry, prefab, sprite);
                    created.Add(entry.id);
                }

                RegisterLocalizedName(entry, stageData);

                if (_addToStageList && stageList != null && !stageList.stages.Contains(stageData))
                {
                    Undo.RecordObject(stageList, "Stage Batch Import");
                    stageList.stages.Add(stageData);
                    EditorUtility.SetDirty(stageList);
                }
            }

            AssetDatabase.SaveAssets();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (updatedImages.Count > 0)
            Debug.LogWarning($"[StageBatchImporter] 기존 이미지가 교체됐습니다. 프리팹의 PolygonCollider2D 모양을 확인하세요:\n{string.Join("\n", updatedImages)}");

        Debug.Log($"[StageBatchImporter] 임포트 완료: 대상 {targets.Count}개, 신규 StageData {created.Count}개 ({string.Join(", ", created)})");
        Repaint();
    }

    // 스테이지 스프라이트는 "{Id}_01.png", 교체 후보는 "{Id}_01_2.png"처럼 원본 순서를 붙여 복사한다.
    // 내용이 같은 파일은 건너뛰어 불필요한 재임포트를 막는다.
    private static void CopyImages(SourceEntry entry, List<string> newImages, List<string> updatedImages)
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        Directory.CreateDirectory(Path.Combine(projectRoot, $"{ImageRootFolder}/{entry.id}"));

        for (int i = 0; i < entry.imagePaths.Count; i++)
        {
            string assetPath = GetImageAssetPath(entry.id, i);
            string fullPath = Path.Combine(projectRoot, assetPath);
            byte[] bytes = File.ReadAllBytes(entry.imagePaths[i]);

            if (File.Exists(fullPath))
            {
                if (File.ReadAllBytes(fullPath).SequenceEqual(bytes)) continue;
                updatedImages.Add(assetPath);
            }
            else
            {
                newImages.Add(assetPath);
            }

            File.WriteAllBytes(fullPath, bytes);
        }
    }

    // 기존 스테이지 이미지와 같은 설정(Sprite·Single·PPU 100·밉맵 없음)으로 맞춘다. 새로 추가된 파일에만 적용해 수동 조정을 보존한다.
    private static void ConfigureSpriteImporter(string assetPath)
    {
        if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
        {
            Debug.LogError($"[StageBatchImporter] TextureImporter를 찾지 못했습니다: {assetPath}");
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    private StageDataSO CreateStageData(SourceEntry entry, GameObject prefab, Sprite sprite)
    {
        Vector2 spriteSize = sprite.bounds.size;
        float longest = Mathf.Max(spriteSize.x, spriteSize.y);
        float scale = longest > 0f ? (float)Math.Round(_targetWorldSize / longest, 2) : 1f;

        var stageData = CreateInstance<StageDataSO>();
        stageData.stageId = GetStageId(entry.id);
        stageData.squishSpawnPosition = StageMakerWindow.DefaultSquishSpawnPosition;
        stageData.armPosition = StageMakerWindow.DefaultArmPosition;
        stageData.stageObjects = new[]
        {
            new StageObjectSpawnData
            {
                prefab = prefab,
                sprite = sprite,
                position = DefaultObjectPosition,
                scale = new Vector2(scale, scale)
            }
        };

        AssetDatabase.CreateAsset(stageData, GetStageDataPath(entry.id));
        Debug.Log($"[StageBatchImporter] StageData 생성: {GetStageDataPath(entry.id)}");
        return stageData;
    }

    // "stage.{stageId}.name" 키를 Stage 테이블에 추가하고 StageData의 이름 참조로 연결한다. 이미 있는 번역은 덮어쓰지 않는다.
    private static void RegisterLocalizedName(SourceEntry entry, StageDataSO stageData)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(StageTableName);
        if (collection == null)
        {
            Debug.LogError($"[StageBatchImporter] '{StageTableName}' StringTableCollection을 찾지 못했습니다.");
            return;
        }

        string key = $"stage.{stageData.stageId}.name";
        var sharedData = collection.SharedData;
        var sharedEntry = sharedData.GetEntry(key);
        if (sharedEntry == null)
        {
            Undo.RecordObject(sharedData, "Stage Batch Import");
            sharedEntry = sharedData.AddKey(key);
            EditorUtility.SetDirty(sharedData);
        }

        foreach (var table in collection.StringTables)
        {
            string value = table.LocaleIdentifier.Code switch
            {
                "ko" => entry.koName,
                "en" => entry.enName,
                _ => null
            };
            if (string.IsNullOrEmpty(value)) continue;

            var tableEntry = table.GetEntry(sharedEntry.Id);
            if (tableEntry != null && !string.IsNullOrEmpty(tableEntry.Value)) continue;

            Undo.RecordObject(table, "Stage Batch Import");
            if (tableEntry != null) tableEntry.Value = value;
            else table.AddEntry(sharedEntry.Id, value);
            EditorUtility.SetDirty(table);
        }

        var localizedName = stageData.LocalizedName;
        if (localizedName.TableEntryReference.KeyId == sharedEntry.Id) return;

        localizedName.SetReference(sharedData.TableCollectionNameGuid, sharedEntry.Id);
        EditorUtility.SetDirty(stageData);
    }

    #endregion

    #region Test

    // 테스트 씬의 DebugStageBootstrap에 스테이지를 지정하고 플레이 모드로 진입한다.
    private static void PlayStage(StageDataSO stageData)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        if (SceneManager.GetActiveScene().path != TestScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(TestScenePath);
        }

        var bootstrap = FindAnyObjectByType<DebugStageBootstrap>();
        if (bootstrap == null)
        {
            Debug.LogError($"[StageBatchImporter] {TestScenePath}에 DebugStageBootstrap이 없습니다.");
            return;
        }

        var serialized = new SerializedObject(bootstrap);
        serialized.FindProperty("debugStageData").objectReferenceValue = stageData;
        serialized.ApplyModifiedProperties();

        EditorApplication.EnterPlaymode();
    }

    #endregion

    #region Path

    private static bool IsValidId(string id) => !string.IsNullOrEmpty(id) && IdPattern.IsMatch(id);

    private static string GetStageId(string id) => $"{id}{StageVariantSuffix}";

    private static string GetImageAssetPath(string id, int index)
        => index == 0
            ? $"{ImageRootFolder}/{id}/{GetStageId(id)}.png"
            : $"{ImageRootFolder}/{id}/{GetStageId(id)}_{index + 1}.png";

    private static string GetStageDataPath(string id) => $"{StageDataFolder}/StageData_{GetStageId(id)}.asset";

    private static StageDataSO LoadStageData(SourceEntry entry)
        => IsValidId(entry.id) ? AssetDatabase.LoadAssetAtPath<StageDataSO>(GetStageDataPath(entry.id)) : null;

    #endregion
}
