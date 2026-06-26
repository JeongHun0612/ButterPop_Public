using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class StageMakerWindow : EditorWindow
{
    // Undo 지원을 위한 Serializable 작업 데이터 (에셋으로 저장하지 않음)
    private class WorkingData : ScriptableObject
    {
        public int stageId;
        public string stageName = "New Stage";
        public Vector2 bananaSpawnPosition = new(0f, 3f);
        public Vector2 monkeyArmPosition = new(0f, -2f);
        public Vector2 cameraCenter = Vector2.zero;
        public float cameraOrthoSize = 5f;
        public float fallYThreshold = -8f;
        public List<StageObjectEntry> stageObjects = new();
    }

    [Serializable]
    private class StageObjectEntry
    {
        public Sprite sprite;
        public Vector2 position;
        public float rotation;
        public Vector2 scale = Vector2.one;
    }

    private WorkingData _data;
    private SerializedObject _serializedData;
    private StageDataSO _loadedSO;

    private bool _isEditMode;
    private GameObject _previewParent;
    private GameObject _bananaPreviewInstance;
    private GameObject _monkeyArmPreviewInstance;
    private readonly List<GameObject> _objectPreviewInstances = new();
    private Camera _mainCamera;

    private const string BananaPrefabPath = "Assets/03. Prefabs/Banana.prefab";
    private const string MonkeyArmPrefabPath = "Assets/03. Prefabs/MonkeyArm.prefab";
    private const string PreviewParentName = "[StageMaker] Preview";
    private const string GeneratedPrefabFolder = "Assets/03. Prefabs/StageObjects";

    private Vector2 _scrollPos;

    [MenuItem("BananaGallery/Stage Maker")]
    public static void Open()
    {
        var window = GetWindow<StageMakerWindow>("Stage Maker");
        window.minSize = new Vector2(360, 600);
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EnsureWorkingData();
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= OnUndoRedo;
        if (_isEditMode) ExitEditMode();
        if (_data != null) DestroyImmediate(_data);
    }

    private void EnsureWorkingData()
    {
        if (_data != null) return;
        _data = CreateInstance<WorkingData>();
        _data.hideFlags = HideFlags.DontSave;
        _serializedData = null;
    }

    private void OnUndoRedo()
    {
        if (_isEditMode) RebuildPreview();
        Repaint();
    }

    private void RecordUndo(string actionName)
    {
        Undo.RecordObject(_data, actionName);
    }

    private void OnGUI()
    {
        EnsureWorkingData();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Stage Maker", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        DrawLoadSection();
        DrawSeparator();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        DrawStageInfo();
        EditorGUILayout.Space(4);
        DrawBananaSpawn();
        EditorGUILayout.Space(4);
        DrawMonkeyArm();
        EditorGUILayout.Space(4);
        DrawCameraSettings();
        EditorGUILayout.Space(4);
        DrawFallThreshold();
        EditorGUILayout.Space(4);
        DrawStageObjects();

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8);
        DrawBottomButtons();
        EditorGUILayout.Space(4);
    }

    #region Load / New

    private void DrawLoadSection()
    {
        EditorGUILayout.LabelField("기존 SO 불러오기 (선택사항)", EditorStyles.miniLabel);
        EditorGUI.BeginChangeCheck();
        var newSO = (StageDataSO)EditorGUILayout.ObjectField(
            "기존 StageData", _loadedSO, typeof(StageDataSO), false);
        if (EditorGUI.EndChangeCheck() && newSO != _loadedSO)
        {
            if (_isEditMode) ExitEditMode();
            _loadedSO = newSO;
            if (newSO != null) LoadFromSO(newSO);
        }

        if (_loadedSO != null)
            EditorGUILayout.HelpBox($"편집 중: {_loadedSO.name}", MessageType.None);
    }

    private void LoadFromSO(StageDataSO so)
    {
        RecordUndo("Load StageData");

        _data.stageId = so.stageId;
        _data.stageName = so.stageName;
        _data.bananaSpawnPosition = so.bananaSpawnPosition;
        _data.monkeyArmPosition = so.monkeyArmPosition;
        _data.cameraCenter = so.cameraCenter;
        _data.cameraOrthoSize = so.cameraOrthographicSize;
        _data.fallYThreshold = so.fallYThreshold;

        _data.stageObjects.Clear();
        if (so.stageObjects != null)
        {
            foreach (var obj in so.stageObjects)
            {
                _data.stageObjects.Add(new StageObjectEntry
                {
                    sprite = obj.sprite,
                    position = obj.position,
                    rotation = obj.rotation,
                    scale = obj.scale
                });
            }
        }
    }

    #endregion

    #region Inspector Sections

    private void DrawStageInfo()
    {
        EditorGUILayout.LabelField("스테이지 정보", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        int newId = EditorGUILayout.IntField("Stage ID", _data.stageId);
        string newName = EditorGUILayout.TextField("Stage Name", _data.stageName);
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("Edit Stage Info");
            _data.stageId = newId;
            _data.stageName = newName;
        }
    }

    private void DrawBananaSpawn()
    {
        EditorGUILayout.LabelField("바나나 낙하 시작점", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        var newPos = EditorGUILayout.Vector2Field("Spawn Position", _data.bananaSpawnPosition);
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("Move Banana Spawn");
            _data.bananaSpawnPosition = newPos;
            if (_isEditMode) UpdateBananaPreviewPosition();
        }
    }

    private void DrawMonkeyArm()
    {
        EditorGUILayout.LabelField("원숭이 팔", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        var newPos = EditorGUILayout.Vector2Field("Arm Position", _data.monkeyArmPosition);
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("Move Monkey Arm");
            _data.monkeyArmPosition = newPos;
            if (_isEditMode) UpdateMonkeyArmPreviewPosition();
            SceneView.RepaintAll();
        }
    }

    private void DrawCameraSettings()
    {
        EditorGUILayout.LabelField("카메라", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        var newCenter = EditorGUILayout.Vector2Field("Camera Center", _data.cameraCenter);
        float newOrtho = EditorGUILayout.FloatField("Orthographic Size", _data.cameraOrthoSize);
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("Edit Camera");
            _data.cameraCenter = newCenter;
            _data.cameraOrthoSize = newOrtho;
            if (_isEditMode) SyncGameCamera();
        }
    }

    private void DrawFallThreshold()
    {
        EditorGUILayout.LabelField("판정", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        float newVal = EditorGUILayout.FloatField("Fall Y Threshold", _data.fallYThreshold);
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("Edit Fall Threshold");
            _data.fallYThreshold = newVal;
            SceneView.RepaintAll();
        }
    }

    private void DrawStageObjects()
    {
        EditorGUILayout.LabelField("스테이지 오브젝트", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Sprite 이미지를 넣으면 SO 생성 시 자동으로 프리팹이 만들어집니다.", MessageType.Info);

        for (int i = 0; i < _data.stageObjects.Count; i++)
        {
            var entry = _data.stageObjects[i];

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 헤더
            EditorGUILayout.BeginHorizontal();
            string label = entry.sprite != null ? entry.sprite.name : $"Object [{i}]";
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            if (GUILayout.Button("삭제", GUILayout.Width(50)))
            {
                RecordUndo("Remove Stage Object");
                _data.stageObjects.RemoveAt(i);
                if (_isEditMode) RebuildPreview();
                GUIUtility.ExitGUI();
                return;
            }
            EditorGUILayout.EndHorizontal();

            // 필드
            EditorGUI.BeginChangeCheck();

            var newSprite = (Sprite)EditorGUILayout.ObjectField("Sprite", entry.sprite, typeof(Sprite), false);

            if (entry.sprite != null)
            {
                var previewTex = AssetPreview.GetAssetPreview(entry.sprite);
                if (previewTex != null)
                {
                    var rect = GUILayoutUtility.GetRect(64, 64, GUILayout.ExpandWidth(false));
                    GUI.DrawTexture(rect, previewTex, ScaleMode.ScaleToFit);
                }
            }

            var newPosition = EditorGUILayout.Vector2Field("Position", entry.position);
            float newRotation = EditorGUILayout.FloatField("Rotation", entry.rotation);
            var newScale = EditorGUILayout.Vector2Field("Size", entry.scale);

            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("Edit Stage Object");
                bool spriteChanged = entry.sprite != newSprite;
                entry.sprite = newSprite;
                entry.position = newPosition;
                entry.rotation = newRotation;
                entry.scale = newScale;

                if (_isEditMode)
                {
                    if (spriteChanged)
                        RebuildPreview();
                    else
                        UpdateObjectPreview(i);
                }
            }

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("+ 오브젝트 추가", GUILayout.Height(26)))
        {
            RecordUndo("Add Stage Object");
            _data.stageObjects.Add(new StageObjectEntry());
            if (_isEditMode) RebuildPreview();
        }
    }

    #endregion

    #region Bottom Buttons

    private void DrawBottomButtons()
    {
        var prevColor = GUI.backgroundColor;
        GUI.backgroundColor = _isEditMode ? new Color(1f, 0.4f, 0.4f) : new Color(0.4f, 1f, 0.4f);
        string editLabel = _isEditMode ? "편집 모드 종료" : "편집 모드 시작 (Scene + Game 프리뷰)";
        if (GUILayout.Button(editLabel, GUILayout.Height(30)))
        {
            if (_isEditMode) ExitEditMode();
            else EnterEditMode();
        }
        GUI.backgroundColor = prevColor;

        EditorGUILayout.Space(4);

        if (_loadedSO != null)
        {
            if (GUILayout.Button("기존 SO에 덮어쓰기 저장", GUILayout.Height(30)))
                SaveToExistingSO();
        }

        if (GUILayout.Button("새 StageData SO 생성", GUILayout.Height(30)))
            CreateNewSO();
    }

    #endregion

    #region Edit Mode

    private void EnterEditMode()
    {
        _isEditMode = true;
        _mainCamera = Camera.main;
        RebuildPreview();
        SyncGameCamera();
        SceneView.RepaintAll();
    }

    private void ExitEditMode()
    {
        _isEditMode = false;
        CleanupPreview();
        SceneView.RepaintAll();
    }

    private void RebuildPreview()
    {
        CleanupPreview();

        _previewParent = new GameObject(PreviewParentName);
        _previewParent.hideFlags = HideFlags.DontSave;

        var bananaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BananaPrefabPath);
        if (bananaPrefab != null)
        {
            _bananaPreviewInstance = (GameObject)PrefabUtility.InstantiatePrefab(bananaPrefab, _previewParent.transform);
            _bananaPreviewInstance.name = "[StageMaker] Banana";
            _bananaPreviewInstance.hideFlags = HideFlags.DontSave;
            SetPreviewStatic(_bananaPreviewInstance);
            UpdateBananaPreviewPosition();
        }

        var monkeyArmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonkeyArmPrefabPath);
        if (monkeyArmPrefab != null)
        {
            _monkeyArmPreviewInstance = (GameObject)PrefabUtility.InstantiatePrefab(monkeyArmPrefab, _previewParent.transform);
            _monkeyArmPreviewInstance.name = "[StageMaker] MonkeyArm";
            _monkeyArmPreviewInstance.hideFlags = HideFlags.DontSave;
            SetPreviewStatic(_monkeyArmPreviewInstance);
            UpdateMonkeyArmPreviewPosition();
        }

        _objectPreviewInstances.Clear();
        for (int i = 0; i < _data.stageObjects.Count; i++)
        {
            var instance = CreateObjectPreview(_data.stageObjects[i], i);
            _objectPreviewInstances.Add(instance);
        }

        SceneVisibilityManager.instance.DisablePicking(_previewParent, true);

        SyncGameCamera();
    }

    private GameObject CreateObjectPreview(StageObjectEntry entry, int index)
    {
        var go = new GameObject($"[StageMaker] Object_{index}");
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(_previewParent.transform);
        ApplyEntryToTransform(go.transform, entry);

        if (entry.sprite != null)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = entry.sprite;
        }

        return go;
    }

    private void ApplyEntryToTransform(Transform t, StageObjectEntry entry)
    {
        t.position = new Vector3(entry.position.x, entry.position.y, 0f);
        t.rotation = Quaternion.Euler(0f, 0f, entry.rotation);
        t.localScale = new Vector3(entry.scale.x, entry.scale.y, 1f);
    }

    private void SetPreviewStatic(GameObject go)
    {
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

        foreach (var mono in go.GetComponents<MonoBehaviour>())
        {
            if (mono != null) mono.enabled = false;
        }
    }

    private void UpdateBananaPreviewPosition()
    {
        if (_bananaPreviewInstance != null)
            _bananaPreviewInstance.transform.position = new Vector3(
                _data.bananaSpawnPosition.x, _data.bananaSpawnPosition.y, 0f);
    }

    private void UpdateMonkeyArmPreviewPosition()
    {
        if (_monkeyArmPreviewInstance != null)
            _monkeyArmPreviewInstance.transform.position = new Vector3(
                _data.monkeyArmPosition.x, _data.monkeyArmPosition.y, 0f);
    }

    private void UpdateObjectPreview(int index)
    {
        if (index < 0 || index >= _objectPreviewInstances.Count) return;

        var instance = _objectPreviewInstances[index];
        var entry = _data.stageObjects[index];

        if (instance == null)
        {
            RebuildPreview();
            return;
        }

        ApplyEntryToTransform(instance.transform, entry);

        var sr = instance.GetComponent<SpriteRenderer>();
        if (entry.sprite != null)
        {
            if (sr == null) sr = instance.AddComponent<SpriteRenderer>();
            sr.sprite = entry.sprite;
        }
        else if (sr != null)
        {
            DestroyImmediate(sr);
        }
    }

    private void SyncGameCamera()
    {
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return;

        Undo.RecordObject(_mainCamera.transform, "Stage Maker Camera");
        Undo.RecordObject(_mainCamera, "Stage Maker Camera");

        _mainCamera.transform.position = new Vector3(
            _data.cameraCenter.x, _data.cameraCenter.y, _mainCamera.transform.position.z);
        _mainCamera.orthographicSize = _data.cameraOrthoSize;
    }

    private void CleanupPreview()
    {
        _bananaPreviewInstance = null;
        _monkeyArmPreviewInstance = null;
        _objectPreviewInstances.Clear();
        _previewParent = null;

        var existing = GameObject.Find(PreviewParentName);
        if (existing != null) DestroyImmediate(existing);
    }

    #endregion

    #region Scene GUI (Handles)

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!_isEditMode || _data == null) return;

        bool changed = false;

        changed |= HandleBananaSpawn();
        changed |= HandleMonkeyArm();
        changed |= HandleCameraCenter();
        DrawCameraRect();
        DrawFallLine();
        changed |= HandleStageObjects();

        if (changed)
        {
            SyncGameCamera();
            Repaint();
        }
    }

    private bool HandleBananaSpawn()
    {
        Vector3 pos = new Vector3(_data.bananaSpawnPosition.x, _data.bananaSpawnPosition.y, 0f);

        // 낙하 방향 화살표
        Handles.color = Color.yellow;
        Handles.DrawDottedLine(pos, pos + Vector3.down * 1.5f, 4f);
        Handles.ConeHandleCap(0, pos + Vector3.down * 1.5f, Quaternion.Euler(180f, 0f, 0f), 0.2f, EventType.Repaint);
        Handles.Label(pos + Vector3.up * 0.4f, "Banana Drop",
            new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.yellow } });

        if (Tools.current != Tool.Move) return false;

        EditorGUI.BeginChangeCheck();
        var newPos = Handles.PositionHandle(pos, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("Move Banana Spawn");
            _data.bananaSpawnPosition = new Vector2(newPos.x, newPos.y);
            UpdateBananaPreviewPosition();
            return true;
        }
        return false;
    }

    private bool HandleMonkeyArm()
    {
        Vector3 pos = new Vector3(_data.monkeyArmPosition.x, _data.monkeyArmPosition.y, 0f);

        Handles.color = new Color(0.7f, 0.4f, 1f);
        Handles.DrawWireDisc(pos, Vector3.forward, 0.3f);
        Handles.Label(pos + Vector3.up * 0.5f, "Monkey Arm",
            new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.7f, 0.4f, 1f) } });

        if (Tools.current != Tool.Move) return false;

        EditorGUI.BeginChangeCheck();
        var newPos = Handles.PositionHandle(pos, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("Move Monkey Arm");
            _data.monkeyArmPosition = new Vector2(newPos.x, newPos.y);
            UpdateMonkeyArmPreviewPosition();
            return true;
        }
        return false;
    }

    private bool HandleCameraCenter()
    {
        Vector3 pos = new Vector3(_data.cameraCenter.x, _data.cameraCenter.y, 0f);

        Handles.color = Color.cyan;
        Handles.DrawWireDisc(pos, Vector3.forward, 0.25f);
        Handles.Label(pos + Vector3.up * 0.45f, "Camera Center",
            new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.cyan } });

        if (Tools.current != Tool.Move) return false;

        EditorGUI.BeginChangeCheck();
        var newPos = Handles.PositionHandle(pos, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("Move Camera Center");
            _data.cameraCenter = new Vector2(newPos.x, newPos.y);
            return true;
        }
        return false;
    }

    private void DrawCameraRect()
    {
        float aspect = _mainCamera != null ? _mainCamera.aspect : 16f / 9f;
        float halfW = _data.cameraOrthoSize * aspect;
        float halfH = _data.cameraOrthoSize;
        Vector2 c = _data.cameraCenter;

        var corners = new Vector3[]
        {
            new(c.x - halfW, c.y - halfH, 0),
            new(c.x - halfW, c.y + halfH, 0),
            new(c.x + halfW, c.y + halfH, 0),
            new(c.x + halfW, c.y - halfH, 0),
        };

        Handles.DrawSolidRectangleWithOutline(corners, new Color(0, 1, 1, 0.05f), new Color(0, 1, 1, 0.4f));
    }

    private void DrawFallLine()
    {
        float y = _data.fallYThreshold;
        Handles.color = new Color(1f, 0.3f, 0.3f, 0.6f);
        Handles.DrawDottedLine(new Vector3(-50, y, 0), new Vector3(50, y, 0), 4f);
        Handles.Label(new Vector3(-49f, y + 0.2f, 0), $"Fall Y = {y}",
            new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.3f, 0.3f) } });
    }

    private bool HandleStageObjects()
    {
        bool changed = false;
        var tool = Tools.current;

        for (int i = 0; i < _data.stageObjects.Count; i++)
        {
            var entry = _data.stageObjects[i];
            Vector3 pos = new Vector3(entry.position.x, entry.position.y, 0f);
            Quaternion rot = Quaternion.Euler(0, 0, entry.rotation);
            Vector3 scl = new Vector3(entry.scale.x, entry.scale.y, 1f);

            Handles.color = Color.green;
            string label = entry.sprite != null ? entry.sprite.name : $"Object [{i}]";
            Handles.Label(pos + Vector3.up * 0.35f, label,
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.green } });

            switch (tool)
            {
                case Tool.Move:
                {
                    EditorGUI.BeginChangeCheck();
                    var newPos = Handles.PositionHandle(pos, rot);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordUndo("Move Stage Object");
                        entry.position = new Vector2(newPos.x, newPos.y);
                        if (i < _objectPreviewInstances.Count && _objectPreviewInstances[i] != null)
                            ApplyEntryToTransform(_objectPreviewInstances[i].transform, entry);
                        changed = true;
                    }
                    break;
                }
                case Tool.Rotate:
                {
                    EditorGUI.BeginChangeCheck();
                    var newRot = Handles.RotationHandle(rot, pos);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordUndo("Rotate Stage Object");
                        entry.rotation = newRot.eulerAngles.z;
                        if (i < _objectPreviewInstances.Count && _objectPreviewInstances[i] != null)
                            ApplyEntryToTransform(_objectPreviewInstances[i].transform, entry);
                        changed = true;
                    }
                    break;
                }
                case Tool.Scale:
                {
                    EditorGUI.BeginChangeCheck();
                    var newScale = Handles.ScaleHandle(scl, pos, rot);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordUndo("Scale Stage Object");
                        entry.scale = new Vector2(newScale.x, newScale.y);
                        if (i < _objectPreviewInstances.Count && _objectPreviewInstances[i] != null)
                            ApplyEntryToTransform(_objectPreviewInstances[i].transform, entry);
                        changed = true;
                    }
                    break;
                }
            }
        }

        return changed;
    }

    #endregion

    #region SO 생성 / 저장

    private void CreateNewSO()
    {
        string defaultName = $"StageData_{_data.stageId}_{_data.stageName}";
        string path = EditorUtility.SaveFilePanelInProject(
            "StageData 저장", defaultName, "asset",
            "StageData SO를 저장할 위치를 선택하세요.",
            "Assets/08. ScriptableObjects");

        if (string.IsNullOrEmpty(path)) return;

        var so = CreateInstance<StageDataSO>();
        ApplyWorkingDataToSO(so);
        AssetDatabase.CreateAsset(so, path);
        AssetDatabase.SaveAssets();

        _loadedSO = so;
        EditorGUIUtility.PingObject(so);
        Debug.Log($"[StageMaker] StageData 생성 완료: {path}");
    }

    private void SaveToExistingSO()
    {
        if (_loadedSO == null) return;

        bool confirmed = EditorUtility.DisplayDialog(
            "StageData 덮어쓰기",
            $"'{_loadedSO.name}' 에 현재 작업 내용을 저장합니다.\n기존 데이터는 복구할 수 없습니다.",
            "저장", "취소");

        if (!confirmed) return;

        Undo.RecordObject(_loadedSO, "Stage Maker Save");
        ApplyWorkingDataToSO(_loadedSO);
        EditorUtility.SetDirty(_loadedSO);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("저장 완료", $"'{_loadedSO.name}' 저장이 완료되었습니다.", "확인");
        Debug.Log($"[StageMaker] StageData 저장 완료: {_loadedSO.name}");
    }

    private void ApplyWorkingDataToSO(StageDataSO so)
    {
        so.stageId = _data.stageId;
        so.stageName = _data.stageName;
        so.bananaSpawnPosition = _data.bananaSpawnPosition;
        so.monkeyArmPosition = _data.monkeyArmPosition;
        so.cameraCenter = _data.cameraCenter;
        so.cameraOrthographicSize = _data.cameraOrthoSize;
        so.fallYThreshold = _data.fallYThreshold;

        EnsureFolder(GeneratedPrefabFolder);

        var spawnDataList = new List<StageObjectSpawnData>();
        foreach (var entry in _data.stageObjects)
        {
            var spawnData = new StageObjectSpawnData
            {
                sprite = entry.sprite,
                position = entry.position,
                rotation = entry.rotation,
                scale = entry.scale
            };

            if (entry.sprite != null)
                spawnData.prefab = GetOrCreatePrefab(entry.sprite);

            spawnDataList.Add(spawnData);
        }

        so.stageObjects = spawnDataList.ToArray();
    }

    private GameObject GetOrCreatePrefab(Sprite sprite)
    {
        string prefabPath = $"{GeneratedPrefabFolder}/{sprite.name}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null) return existing;

        var go = new GameObject(sprite.name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        go.AddComponent<PolygonCollider2D>();
        go.AddComponent<StageObject>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        DestroyImmediate(go);

        Debug.Log($"[StageMaker] 프리팹 생성: {prefabPath}");
        return prefab;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string folderName = Path.GetFileName(folderPath);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }

    #endregion

    #region Utility

    private void DrawSeparator()
    {
        EditorGUILayout.Space(4);
        var rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        EditorGUILayout.Space(4);
    }

    #endregion
}
