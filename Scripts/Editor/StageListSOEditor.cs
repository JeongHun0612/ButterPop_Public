using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(StageListSO))]
public class StageListSOEditor : Editor
{
    private const string SquishPrefabPath = "Assets/03. Prefabs/Controllers/Squish.prefab";
    private const string ArmPrefabPath = "Assets/03. Prefabs/Controllers/Arm.prefab";
    private const float MaxPreviewHeight = 520f;
    private const float PreviewPadding = 8f;
    private const float MaxStageListHeight = 170f;

    private SerializedProperty _stagesProperty;
    private ReorderableList _stageList;
    private PreviewRenderUtility _previewUtility;
    private StageDataSO _previewedStage;
    private Hash128 _previewDependencyHash;
    private int _selectedIndex;
    private bool _previewDirty = true;
    private Vector2 _stageListScrollPosition;

    private StageListSO StageList => (StageListSO)target;

    private void OnEnable()
    {
        _stagesProperty = serializedObject.FindProperty("stages");
        _stageList = new ReorderableList(serializedObject, _stagesProperty, true, true, true, true);
        _stageList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, $"스테이지 ({_stagesProperty.arraySize})");
        };
        _stageList.drawElementCallback = (rect, index, active, focused) =>
        {
            // 오브젝트 필드가 클릭 이벤트를 소비하기 전에 프리뷰 선택을 반영한다.
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                && rect.Contains(Event.current.mousePosition))
                SelectStage(index);

            rect.y += 1f;
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(rect, _stagesProperty.GetArrayElementAtIndex(index),
                new GUIContent($"Element {index}"));
            if (EditorGUI.EndChangeCheck()) SelectStage(index);
        };
        _stageList.onSelectCallback = list => SelectStage(list.index);
        _stageList.onReorderCallback = list => SelectStage(list.index);
        _stageList.index = _selectedIndex;
        EditorApplication.projectChanged += OnProjectChanged;
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= OnProjectChanged;
        CleanupPreview();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (var scroll = new EditorGUILayout.ScrollViewScope(
            _stageListScrollPosition, GUILayout.MaxHeight(MaxStageListHeight)))
        {
            _stageListScrollPosition = scroll.scrollPosition;
            _stageList.DoLayoutList();
        }
        if (serializedObject.ApplyModifiedProperties())
            _previewDirty = true;

        EditorGUILayout.Space(10f);
        DrawSeparator();
        EditorGUILayout.Space(6f);
        DrawPreviewSection();
    }

    private void DrawPreviewSection()
    {
        EditorGUILayout.LabelField("스테이지 프리뷰", EditorStyles.boldLabel);

        int count = StageList.Count;
        if (count == 0)
        {
            EditorGUILayout.HelpBox("프리뷰할 스테이지가 없습니다.", MessageType.Info);
            return;
        }

        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, count - 1);
        DrawStageSelector(count);

        StageDataSO stage = StageList.Get(_selectedIndex);
        if (stage == null)
        {
            CleanupPreview();
            EditorGUILayout.HelpBox("선택한 항목에 StageData SO가 지정되지 않았습니다.", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(4f);
        DrawStageSummary(stage, count);
        DrawStagePreview(stage);

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("Stage Maker에서 열기", GUILayout.Height(28f)))
            StageMakerWindow.Open(stage);

        EditorGUILayout.LabelField("프리뷰를 더블 클릭해도 Stage Maker에서 열립니다.", EditorStyles.centeredGreyMiniLabel);
    }

    private void DrawStageSelector(int count)
    {
        string[] names = new string[count];
        for (int i = 0; i < count; i++)
        {
            StageDataSO stage = StageList.Get(i);
            names[i] = stage == null
                ? $"{i + 1}. (비어 있음)"
                : $"{i + 1}. {GetStageDisplayName(stage)}";
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(count <= 1))
            {
                if (GUILayout.Button("◀", GUILayout.Width(32f)))
                    SelectStage((_selectedIndex - 1 + count) % count);
            }

            int nextIndex = EditorGUILayout.Popup(_selectedIndex, names);
            if (nextIndex != _selectedIndex)
                SelectStage(nextIndex);

            using (new EditorGUI.DisabledScope(count <= 1))
            {
                if (GUILayout.Button("▶", GUILayout.Width(32f)))
                    SelectStage((_selectedIndex + 1) % count);
            }
        }
    }

    private void DrawStageSummary(StageDataSO stage, int count)
    {
        int objectCount = stage.stageObjects?.Length ?? 0;
        string stageName = GetStageDisplayName(stage);

        EditorGUILayout.LabelField(stageName, EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            $"{_selectedIndex + 1} / {count}  ·  오브젝트 {objectCount}개",
            EditorStyles.miniLabel);
    }

    private void DrawStagePreview(StageDataSO stage)
    {
        float availableWidth = Mathf.Max(1f, EditorGUIUtility.currentViewWidth - 44f);
        float previewHeight = Mathf.Min(MaxPreviewHeight, availableWidth / StageDataSO.ReferenceAspect);
        float previewWidth = previewHeight * StageDataSO.ReferenceAspect;
        float layoutHeight = previewHeight + PreviewPadding * 2f;
        Rect layoutRect = EditorGUILayout.GetControlRect(false, layoutHeight);
        Rect frameRect = new(
            layoutRect.center.x - previewWidth * 0.5f - PreviewPadding,
            layoutRect.y,
            previewWidth + PreviewPadding * 2f,
            layoutHeight);
        Rect previewRect = new(
            frameRect.x + PreviewPadding,
            frameRect.y + PreviewPadding,
            previewWidth,
            previewHeight);

        EditorGUI.DrawRect(frameRect, new Color(0.12f, 0.12f, 0.12f, 1f));

        EnsurePreview(stage);
        if (_previewUtility != null && Event.current.type == EventType.Repaint)
        {
            // 배경색은 캐시된 프리뷰를 다시 만들지 않아도 현재 스테이지 값을 반영한다.
            _previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
            _previewUtility.camera.backgroundColor = stage.backgroundColor;
            _previewUtility.BeginPreview(previewRect, GUIStyle.none);
            _previewUtility.Render(true);
            Texture texture = _previewUtility.EndPreview();
            GUI.DrawTexture(previewRect, texture, ScaleMode.StretchToFill, false);
        }

        EditorGUIUtility.AddCursorRect(previewRect, MouseCursor.Link);
        Event currentEvent = Event.current;
        if (currentEvent.type == EventType.MouseDown && currentEvent.clickCount == 2 && previewRect.Contains(currentEvent.mousePosition))
        {
            StageMakerWindow.Open(stage);
            currentEvent.Use();
        }
    }

    private void EnsurePreview(StageDataSO stage)
    {
        Hash128 dependencyHash = GetDependencyHash(stage);
        if (!_previewDirty && _previewedStage == stage && _previewDependencyHash == dependencyHash)
            return;

        RebuildPreview(stage);
        _previewedStage = stage;
        _previewDependencyHash = dependencyHash;
        _previewDirty = false;
    }

    private void RebuildPreview(StageDataSO stage)
    {
        CleanupPreview();
        if (stage == null) return;

        _previewUtility = new PreviewRenderUtility();
        _previewUtility.ambientColor = Color.white;

        Camera previewCamera = _previewUtility.camera;
        previewCamera.orthographic = true;
        previewCamera.orthographicSize = Mathf.Max(0.01f, stage.cameraOrthographicSize);
        previewCamera.aspect = StageDataSO.ReferenceAspect;
        previewCamera.transform.position = new Vector3(stage.cameraCenter.x, stage.cameraCenter.y, -10f);
        previewCamera.transform.rotation = Quaternion.identity;
        previewCamera.nearClipPlane = 0.01f;
        previewCamera.farClipPlane = 100f;

        AddControllerPreview(SquishPrefabPath, stage.squishSpawnPosition, "[StageList Preview] Squish");
        AddControllerPreview(ArmPrefabPath, stage.armPosition, "[StageList Preview] Arm");

        if (stage.stageObjects == null) return;

        for (int i = 0; i < stage.stageObjects.Length; i++)
            AddStageObjectPreview(stage.stageObjects[i], i);
    }

    private void AddControllerPreview(string prefabPath, Vector2 position, string objectName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;

        GameObject instance = Instantiate(prefab);
        instance.name = objectName;
        instance.transform.position = new Vector3(position.x, position.y, 0f);
        PreparePreviewObject(instance);
        _previewUtility.AddSingleGO(instance);
    }

    private void AddStageObjectPreview(StageObjectSpawnData spawnData, int index)
    {
        if (spawnData == null) return;

        GameObject instance;
        if (spawnData.prefab != null)
        {
            instance = Instantiate(spawnData.prefab);
        }
        else
        {
            instance = new GameObject();
            if (spawnData.sprite != null)
            {
                var spriteRenderer = instance.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = spawnData.sprite;
            }
        }

        instance.name = $"[StageList Preview] Object_{index}";
        instance.transform.position = new Vector3(spawnData.position.x, spawnData.position.y, 0f);
        instance.transform.rotation = Quaternion.Euler(0f, 0f, spawnData.rotation);
        float scaleX = spawnData.flipX ? -spawnData.scale.x : spawnData.scale.x;
        instance.transform.localScale = new Vector3(scaleX, spawnData.scale.y, 1f);

        ApplyCoverSquish(instance, spawnData.coverSquish);
        PreparePreviewObject(instance);
        _previewUtility.AddSingleGO(instance);
    }

    private static void ApplyCoverSquish(GameObject instance, bool coverSquish)
    {
        if (!coverSquish) return;

        foreach (SpriteRenderer spriteRenderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (spriteRenderer.GetComponentInParent<IgnoreCoverSquish>(true) != null) continue;
            spriteRenderer.sortingOrder += StageDataSO.CoverSquishSortingOrder;
        }
    }

    private static void PreparePreviewObject(GameObject instance)
    {
        foreach (Rigidbody2D body in instance.GetComponentsInChildren<Rigidbody2D>(true))
            body.simulated = false;

        foreach (Behaviour behaviour in instance.GetComponentsInChildren<Behaviour>(true))
            behaviour.enabled = false;

        foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            child.gameObject.hideFlags = HideFlags.HideAndDontSave;
    }

    private void SelectStage(int index)
    {
        _selectedIndex = index;
        if (_stageList != null) _stageList.index = index;
        _previewDirty = true;
        Repaint();
    }

    private void OnProjectChanged()
    {
        _previewDirty = true;
        Repaint();
    }

    private void CleanupPreview()
    {
        if (_previewUtility != null)
        {
            _previewUtility.Cleanup();
            _previewUtility = null;
        }

        _previewedStage = null;
        _previewDependencyHash = default;
    }

    private static Hash128 GetDependencyHash(StageDataSO stage)
    {
        string path = AssetDatabase.GetAssetPath(stage);
        return string.IsNullOrEmpty(path) ? default : AssetDatabase.GetAssetDependencyHash(path);
    }

    private static string GetStageDisplayName(StageDataSO stage)
        => string.IsNullOrWhiteSpace(stage.stageId) ? stage.name : stage.stageId;

    private static void DrawSeparator()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
    }
}
