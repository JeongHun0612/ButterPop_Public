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
        public string stageId = DefaultStageId;
        public Vector2 squishSpawnPosition = DefaultSquishSpawnPosition;
        public Vector2 armPosition = DefaultArmPosition;
        public Vector2 cameraCenter = Vector2.zero;
        public float cameraOrthoSize = 6f;
        public Color backgroundColor = StageDataSO.DefaultBackgroundColor;
        public List<StageObjectEntry> stageObjects = new();
    }

    [Serializable]
    private class StageObjectEntry
    {
        public GameObject prefab;
        public Sprite sprite;
        public Vector2 position;
        public float rotation;
        public Vector2 scale = Vector2.one;
        public bool flipX;
        public bool coverSquish;

        // Size X/Y를 원본 비율을 유지하며 함께 조절할지 여부 (에디터 편의용, SO에는 저장하지 않음).
        public bool lockAspect = true;

        // 비율 고정 시 기준이 되는 종횡비(X/Y). 락을 켜는 순간 캡처한다.
        // 매번 현재 값에서 파생하지 않고 이 값을 쓰므로, 입력 중 한 축이 0을 스쳐도 비율이 유실되지 않는다.
        public float lockAspectRatio = 1f;
    }

    private WorkingData _data;
    private StageDataSO _loadedSO;

    private bool _isEditMode;
    private GameObject _previewParent;
    private GameObject _squishPreviewInstance;
    private GameObject _handPreviewInstance;
    private readonly List<GameObject> _objectPreviewInstances = new();
    private Camera _mainCamera;
    private float _lastKnownAspect;

    private const string SquishPrefabPath = "Assets/03. Prefabs/Controllers/Squish.prefab";
    private const string HandPrefabPath = "Assets/03. Prefabs/Controllers/Arm.prefab";
    private const string PreviewParentName = "[StageMaker] Preview";
    private const string GeneratedPrefabFolder = "Assets/03. Prefabs/StageObjects";

    // 신규 작업의 기본 스테이지 이름. 첫 오브젝트 추가 시 이름 자동 설정 여부 판별에도 사용한다.
    private const string DefaultStageId = "New Stage";

    // 스퀴시 낙하 시작점 · 손의 기본 위치. 신규 작업 초기값이자 "기본값으로" 버튼의 복원 대상.
    private static readonly Vector2 DefaultSquishSpawnPosition = new(-1.5f, 2.5f);
    private static readonly Vector2 DefaultArmPosition = new(-3f, -2f);

    private Vector2 _scrollPos;

    [MenuItem("ButterPop/Stage Maker")]
    public static void Open()
    {
        var window = GetWindow<StageMakerWindow>("Stage Maker");
        window.minSize = new Vector2(360, 600);
    }

    public static void Open(StageDataSO stageData)
    {
        var window = GetWindow<StageMakerWindow>("Stage Maker");
        window.minSize = new Vector2(360, 600);
        window.LoadStageData(stageData);
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.update += OnEditorUpdate;
        EnsureWorkingData();
        Undo.undoRedoPerformed += OnUndoRedo;

        // 도메인 리로드(스크립트 컴파일 등)로 _data는 파괴되어 기본값으로 리셋되지만,
        // 창에 직렬화된 _loadedSO는 살아남아 상태가 어긋난다(오브젝트가 비어 프리뷰가 안 뜸).
        // 로드된 SO가 있으면 그 데이터로 다시 채워 일관성을 맞춘다.
        if (_loadedSO != null) LoadFromSO(_loadedSO);
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.update -= OnEditorUpdate;
        Undo.undoRedoPerformed -= OnUndoRedo;
        if (_isEditMode) ExitEditMode();
        if (_data != null) DestroyImmediate(_data);
    }

    // Game View 비율(_mainCamera.aspect)이 바뀌면 씬 뷰를 갱신해
    // "현재 기기에서 실제로 보이는 영역" 프레임이 실시간으로 따라오게 한다.
    private void OnEditorUpdate()
    {
        if (!_isEditMode || _mainCamera == null) return;

        if (!Mathf.Approximately(_mainCamera.aspect, _lastKnownAspect))
        {
            _lastKnownAspect = _mainCamera.aspect;
            SceneView.RepaintAll();
        }
    }

    private void EnsureWorkingData()
    {
        if (_data != null) return;
        _data = CreateInstance<WorkingData>();
        _data.hideFlags = HideFlags.DontSave;
    }

    private void LoadStageData(StageDataSO stageData)
    {
        if (stageData == null) return;

        EnsureWorkingData();
        if (_isEditMode) ExitEditMode();

        _loadedSO = stageData;
        LoadFromSO(stageData);
        Repaint();
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
        DrawSquishSpawn();
        EditorGUILayout.Space(4);
        DrawHand();
        EditorGUILayout.Space(2);
        DrawResetSpawnButton();
        EditorGUILayout.Space(4);
        DrawCameraSettings();
        EditorGUILayout.Space(4);
        DrawBackgroundSettings();
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
        _data.squishSpawnPosition = so.squishSpawnPosition;
        _data.armPosition = so.armPosition;
        _data.cameraCenter = so.cameraCenter;
        _data.cameraOrthoSize = so.cameraOrthographicSize;
        _data.backgroundColor = so.backgroundColor;

        _data.stageObjects.Clear();
        if (so.stageObjects != null)
        {
            foreach (var obj in so.stageObjects)
            {
                _data.stageObjects.Add(new StageObjectEntry
                {
                    prefab = obj.prefab,
                    sprite = obj.sprite,
                    position = obj.position,
                    rotation = obj.rotation,
                    scale = obj.scale,
                    flipX = obj.flipX,
                    coverSquish = obj.coverSquish
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
        string newId = EditorGUILayout.TextField("Stage Id", _data.stageId);
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("Edit Stage Info");
            _data.stageId = newId;
        }
    }

    private void DrawSquishSpawn()
    {
        EditorGUILayout.LabelField("스퀴시 낙하 시작점", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        var newPos = EditorGUILayout.Vector2Field("Spawn Position", _data.squishSpawnPosition);
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("Move Squish Spawn");
            _data.squishSpawnPosition = newPos;
            if (_isEditMode) UpdateSquishPreviewPosition();
        }
    }

    private void DrawHand()
    {
        EditorGUILayout.LabelField("손 위치", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        var newPos = EditorGUILayout.Vector2Field("Hand Position", _data.armPosition);
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("Move Hand");
            _data.armPosition = newPos;
            if (_isEditMode) UpdateHandPreviewPosition();
            SceneView.RepaintAll();
        }
    }

    private void DrawResetSpawnButton()
    {
        if (!GUILayout.Button("스퀴시·손 위치 기본값으로", GUILayout.Height(22))) return;

        RecordUndo("Reset Spawn Positions");
        _data.squishSpawnPosition = DefaultSquishSpawnPosition;
        _data.armPosition = DefaultArmPosition;
        if (_isEditMode)
        {
            UpdateSquishPreviewPosition();
            UpdateHandPreviewPosition();
        }
        SceneView.RepaintAll();
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

    private void DrawBackgroundSettings()
    {
        EditorGUILayout.LabelField("배경", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        var newColor = EditorGUILayout.ColorField(
            new GUIContent("Background Color", "카메라 Solid Color 배경색. 기본값 #3C3C3C."),
            _data.backgroundColor);
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("Edit Background Color");
            _data.backgroundColor = newColor;
            if (_isEditMode) SyncGameCamera();
        }

        if (GUILayout.Button("기본값", GUILayout.Width(60)))
        {
            RecordUndo("Reset Background Color");
            _data.backgroundColor = StageDataSO.DefaultBackgroundColor;
            if (_isEditMode) SyncGameCamera();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawStageObjects()
    {
        EditorGUILayout.LabelField("스테이지 오브젝트", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Prefab을 직접 지정하거나, Sprite만 넣으면 자동으로 프리팹이 생성됩니다.", MessageType.Info);

        for (int i = 0; i < _data.stageObjects.Count; i++)
        {
            var entry = _data.stageObjects[i];

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 헤더
            EditorGUILayout.BeginHorizontal();
            string label = entry.prefab != null ? entry.prefab.name
                : entry.sprite != null ? entry.sprite.name
                : $"Object [{i}]";
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

            var newPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", entry.prefab, typeof(GameObject), false);

            Sprite newSprite = entry.sprite;
            if (newPrefab == null)
            {
                newSprite = (Sprite)EditorGUILayout.ObjectField("Sprite", entry.sprite, typeof(Sprite), false);
            }

            // 프리뷰 이미지
            UnityEngine.Object previewTarget = newPrefab != null ? (UnityEngine.Object)newPrefab : entry.sprite;
            if (previewTarget != null)
            {
                var previewTex = AssetPreview.GetAssetPreview(previewTarget);
                if (previewTex != null)
                {
                    var rect = GUILayoutUtility.GetRect(64, 64, GUILayout.ExpandWidth(false));
                    GUI.DrawTexture(rect, previewTex, ScaleMode.ScaleToFit);
                }
            }

            // 좁은 인스펙터에서도 벡터 필드를 "라벨 | X Y" 한 줄로 렌더링한다.
            bool prevWideMode = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;

            var newPosition = EditorGUILayout.Vector2Field("Position", entry.position);
            float newRotation = EditorGUILayout.FloatField("Rotation", entry.rotation);

            EditorGUILayout.BeginHorizontal();
            var newScale = EditorGUILayout.Vector2Field("Size", entry.scale);
            var lockIcon = new GUIContent(
                EditorGUIUtility.IconContent(entry.lockAspect ? "Linked" : "Unlinked"))
            {
                tooltip = "비율 고정: 켜면 Size X/Y가 원본 비율을 유지하며 함께 조절됩니다."
            };
            bool newLockAspect = GUILayout.Toggle(
                entry.lockAspect, lockIcon, "IconButton", GUILayout.Width(22f), GUILayout.Height(18f));
            EditorGUILayout.EndHorizontal();

            // 비율 고정이 새로 켜진 순간의 종횡비를 저장해 이후 입력의 기준으로 삼는다.
            if (newLockAspect && !entry.lockAspect)
                entry.lockAspectRatio = CaptureAspectRatio(entry.scale);

            newScale = ApplyAspectLock(entry.scale, newScale, newLockAspect, entry.lockAspectRatio);

            EditorGUIUtility.wideMode = prevWideMode;

            bool newFlipX = EditorGUILayout.Toggle("Flip X", entry.flipX);
            bool newCoverSquish = EditorGUILayout.Toggle(
                new GUIContent("스퀴시 가리기", "체크 시 Sorting Order를 높여 스퀴시를 가립니다 (돔 내부 등)."),
                entry.coverSquish);

            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo("Edit Stage Object");
                bool visualChanged = entry.prefab != newPrefab || entry.sprite != newSprite;
                entry.prefab = newPrefab;
                entry.sprite = newSprite;
                if (visualChanged && i == 0 && _data.stageId == DefaultStageId)
                {
                    if (newPrefab != null) _data.stageId = newPrefab.name;
                    else if (newSprite != null) _data.stageId = newSprite.name;
                }
                entry.position = newPosition;
                entry.rotation = newRotation;
                entry.scale = newScale;
                entry.lockAspect = newLockAspect;
                entry.flipX = newFlipX;
                entry.coverSquish = newCoverSquish;

                if (_isEditMode)
                {
                    if (visualChanged)
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

        EditorGUILayout.Space(4);

        if (GUILayout.Button("초기화", GUILayout.Height(26)))
        {
            if (EditorUtility.DisplayDialog("초기화", "현재 작업 내용을 모두 초기화합니다.", "확인", "취소"))
                ResetWorkingData();
        }
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

        var squishPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SquishPrefabPath);
        if (squishPrefab != null)
        {
            _squishPreviewInstance = (GameObject)PrefabUtility.InstantiatePrefab(squishPrefab, _previewParent.transform);
            _squishPreviewInstance.name = "[StageMaker] Squish";
            _squishPreviewInstance.hideFlags = HideFlags.DontSave;
            SetPreviewStatic(_squishPreviewInstance);
            UpdateSquishPreviewPosition();
        }

        var handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HandPrefabPath);
        if (handPrefab != null)
        {
            _handPreviewInstance = (GameObject)PrefabUtility.InstantiatePrefab(handPrefab, _previewParent.transform);
            _handPreviewInstance.name = "[StageMaker] Hand";
            _handPreviewInstance.hideFlags = HideFlags.DontSave;
            SetPreviewStatic(_handPreviewInstance);
            UpdateHandPreviewPosition();
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
        GameObject go;

        if (entry.prefab != null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab, _previewParent.transform);
            go.name = $"[StageMaker] Object_{index}";
            go.hideFlags = HideFlags.DontSave;
            SetPreviewStatic(go);
        }
        else
        {
            go = new GameObject($"[StageMaker] Object_{index}");
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(_previewParent.transform);

            if (entry.sprite != null)
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = entry.sprite;
            }
        }

        ApplyEntryToTransform(go.transform, entry);
        ApplyCoverSquish(go, entry);
        return go;
    }

    // 비율 고정: 저장된 종횡비(ratio = X/Y)를 기준으로, 사용자가 바꾼 축에 맞춰 반대 축을 계산한다.
    // 인스펙터·핸들은 한 번에 한 축만 바꾸므로 X를 먼저 검사하고, 아니면 Y로 판단한다.
    // 값을 이전 값에서 나눗셈으로 파생하지 않으므로, "0.5" 입력 도중의 "0"처럼 한 축이 잠깐 0이 돼도
    // 비율이 유실되지 않고 회복된다.
    private static Vector2 ApplyAspectLock(Vector2 previous, Vector2 next, bool locked, float ratio)
    {
        if (!locked || ratio <= 0f) return next;

        if (!Mathf.Approximately(next.x, previous.x))
            next.y = next.x / ratio;
        else if (!Mathf.Approximately(next.y, previous.y))
            next.x = next.y * ratio;

        return next;
    }

    // 현재 스케일에서 종횡비(X/Y)를 구한다. 축이 0이면 비율을 정의할 수 없어 1로 둔다.
    private static float CaptureAspectRatio(Vector2 scale)
        => Mathf.Approximately(scale.x, 0f) || Mathf.Approximately(scale.y, 0f) ? 1f : scale.x / scale.y;

    private void ApplyEntryToTransform(Transform t, StageObjectEntry entry)
    {
        t.position = new Vector3(entry.position.x, entry.position.y, 0f);
        t.rotation = Quaternion.Euler(0f, 0f, entry.rotation);
        float scaleX = entry.flipX ? -entry.scale.x : entry.scale.x;
        t.localScale = new Vector3(scaleX, entry.scale.y, 1f);
    }

    // 프리뷰에서도 스퀴시 가리기 여부에 따라 Sorting Order를 반영해 실제 그려지는 순서를 확인할 수 있게 한다.
    // 원본 프리팹의 Order in Layer(흰자·검은자 등 내부 상대 순서)를 유지하기 위해 덮어쓰지 않고 오프셋만 더한다.
    // 원본 값을 기준으로 계산하므로 이동·회전 등으로 반복 호출돼도 누적되지 않는다(idempotent).
    // 제외 규칙(IgnoreCoverSquish)은 StageBuilder.ApplyCoverSquish와 동일하게 맞춘다.
    private void ApplyCoverSquish(GameObject go, StageObjectEntry entry)
    {
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            bool cover = entry.coverSquish && sr.GetComponentInParent<IgnoreCoverSquish>(true) == null;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(sr);
            int baseOrder = source != null ? source.sortingOrder : 0;
            sr.sortingOrder = baseOrder + (cover ? StageDataSO.CoverSquishSortingOrder : 0);
        }
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

    private void UpdateSquishPreviewPosition()
    {
        if (_squishPreviewInstance != null)
            _squishPreviewInstance.transform.position = new Vector3(
                _data.squishSpawnPosition.x, _data.squishSpawnPosition.y, 0f);
    }

    private void UpdateHandPreviewPosition()
    {
        if (_handPreviewInstance != null)
            _handPreviewInstance.transform.position = new Vector3(
                _data.armPosition.x, _data.armPosition.y, 0f);
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
        ApplyCoverSquish(instance, entry);

        if (entry.prefab != null) return;

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

        // 배경색도 실시간 반영해 프리뷰에서 최종 배경을 확인할 수 있게 한다.
        _mainCamera.clearFlags = CameraClearFlags.SolidColor;
        _mainCamera.backgroundColor = _data.backgroundColor;
    }

    private void CleanupPreview()
    {
        _squishPreviewInstance = null;
        _handPreviewInstance = null;
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

        changed |= HandleSquishSpawn();
        changed |= HandleHand();
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

    private bool HandleSquishSpawn()
    {
        Vector3 pos = new Vector3(_data.squishSpawnPosition.x, _data.squishSpawnPosition.y, 0f);

        // 낙하 방향 화살표
        Handles.color = Color.yellow;
        Handles.DrawDottedLine(pos, pos + Vector3.down * 1.5f, 4f);
        Handles.ConeHandleCap(0, pos + Vector3.down * 1.5f, Quaternion.Euler(180f, 0f, 0f), 0.2f, EventType.Repaint);
        Handles.Label(pos + Vector3.up * 0.4f, "Squish Drop",
            new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.yellow } });

        if (Tools.current != Tool.Move) return false;

        EditorGUI.BeginChangeCheck();
        var newPos = Handles.PositionHandle(pos, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("Move Squish Spawn");
            _data.squishSpawnPosition = new Vector2(newPos.x, newPos.y);
            UpdateSquishPreviewPosition();
            return true;
        }
        return false;
    }

    private bool HandleHand()
    {
        Vector3 pos = new Vector3(_data.armPosition.x, _data.armPosition.y, 0f);

        Handles.color = new Color(0.7f, 0.4f, 1f);
        Handles.DrawWireDisc(pos, Vector3.forward, 0.3f);
        Handles.Label(pos + Vector3.up * 0.5f, "Hand",
            new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.7f, 0.4f, 1f) } });

        if (Tools.current != Tool.Move) return false;

        EditorGUI.BeginChangeCheck();
        var newPos = Handles.PositionHandle(pos, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("Move Hand");
            _data.armPosition = new Vector2(newPos.x, newPos.y);
            UpdateHandPreviewPosition();
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
        Vector2 c = _data.cameraCenter;

        // 1) 기준 프레임(9:19.5) — 런타임 보정상 "모든 기기에서 항상 보이는 최소 영역".
        //    여기 안에 배치한 오브젝트는 어떤 기기에서도 잘리지 않는다.
        DrawFrame(c, _data.cameraOrthoSize, StageDataSO.ReferenceAspect,
            new Color(0, 1, 1, 0.05f), new Color(0, 1, 1, 0.4f));

        // 2) 현재 Game View 기기 프레임 — 위 보정 공식을 그대로 적용한, 이 기기에서 실제로 보이는 영역.
        //    Game View 비율을 바꾸면 실시간으로 갱신된다. 기준 프레임보다 넓거나 위아래로 더 크게 나타난다.
        if (_mainCamera != null)
        {
            float deviceAspect = _mainCamera.aspect;
            float deviceSize = StageDataSO.ResolveOrthographicSize(_data.cameraOrthoSize, deviceAspect);
            DrawFrame(c, deviceSize, deviceAspect,
                new Color(1f, 0.6f, 0f, 0f), new Color(1f, 0.6f, 0f, 0.7f));

            float dhW = deviceSize * deviceAspect;
            float dhH = deviceSize;
            var style = new GUIStyle(EditorStyles.miniBoldLabel)
            { normal = { textColor = new Color(1f, 0.6f, 0f) } };
            Handles.Label(new Vector3(c.x - dhW, c.y + dhH + 0.15f, 0),
                $"Device View  {deviceAspect:0.000}  ({deviceSize / _data.cameraOrthoSize:0.00}x)", style);
        }
    }

    private static void DrawFrame(Vector2 center, float halfH, float aspect, Color fill, Color outline)
    {
        float halfW = halfH * aspect;
        var corners = new Vector3[]
        {
            new(center.x - halfW, center.y - halfH, 0),
            new(center.x - halfW, center.y + halfH, 0),
            new(center.x + halfW, center.y + halfH, 0),
            new(center.x + halfW, center.y - halfH, 0),
        };
        Handles.DrawSolidRectangleWithOutline(corners, fill, outline);
    }

    private void DrawFallLine()
    {
        float halfH = _data.cameraOrthoSize;
        float aspect = StageDataSO.ReferenceAspect;
        float halfW = halfH * aspect;
        float cx = _data.cameraCenter.x;
        float cy = _data.cameraCenter.y;

        var squishPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SquishPrefabPath);
        var detector = squishPrefab != null ? squishPrefab.GetComponentInChildren<SquishSettleDetector>() : null;
        var so = detector != null ? new SerializedObject(detector) : null;
        var marginProp = so?.FindProperty("outOfBoundsMargin");
        float margin = marginProp != null ? marginProp.floatValue : 2f;

        float yLine = cy - halfH - margin;
        float xLine = halfW + margin;

        Handles.color = new Color(1f, 0.3f, 0.3f, 0.6f);
        var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.3f, 0.3f) } };

        Handles.DrawDottedLine(new Vector3(cx - xLine, yLine, 0), new Vector3(cx + xLine, yLine, 0), 4f);
        Handles.Label(new Vector3(cx - xLine, yLine + 0.2f, 0), $"Fall Y", style);

        Handles.DrawDottedLine(new Vector3(cx - xLine, cy - halfH, 0), new Vector3(cx - xLine, cy + halfH, 0), 4f);
        Handles.DrawDottedLine(new Vector3(cx + xLine, cy - halfH, 0), new Vector3(cx + xLine, cy + halfH, 0), 4f);
        Handles.Label(new Vector3(cx + xLine + 0.1f, cy, 0), $"Fall X", style);
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
            string label = entry.prefab != null ? entry.prefab.name
                : entry.sprite != null ? entry.sprite.name
                : $"Object [{i}]";
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
                        entry.scale = ApplyAspectLock(entry.scale, new Vector2(newScale.x, newScale.y), entry.lockAspect, entry.lockAspectRatio);
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
        string defaultName = $"StageData_{_data.stageId}";
        string path = EditorUtility.SaveFilePanelInProject(
            "StageData 저장", defaultName, "asset",
            "StageData SO를 저장할 위치를 선택하세요.",
            "Assets/08. ScriptableObjects/StageDatas/Stages");

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
        so.squishSpawnPosition = _data.squishSpawnPosition;
        so.armPosition = _data.armPosition;
        so.cameraCenter = _data.cameraCenter;
        so.cameraOrthographicSize = _data.cameraOrthoSize;
        so.backgroundColor = _data.backgroundColor;

        EnsureFolder(GeneratedPrefabFolder);

        var spawnDataList = new List<StageObjectSpawnData>();
        foreach (var entry in _data.stageObjects)
        {
            var spawnData = new StageObjectSpawnData
            {
                sprite = entry.sprite,
                position = entry.position,
                rotation = entry.rotation,
                scale = entry.scale,
                flipX = entry.flipX,
                coverSquish = entry.coverSquish
            };

            if (entry.prefab != null)
            {
                spawnData.prefab = entry.prefab;
                EnsureStageObjectComponent(entry.prefab);
            }
            else if (entry.sprite != null)
                spawnData.prefab = GetOrCreatePrefab(entry.sprite);

            spawnDataList.Add(spawnData);
        }

        so.stageObjects = spawnDataList.ToArray();
    }

    private GameObject GetOrCreatePrefab(Sprite sprite)
    {
        // Images/StageObjects 규칙과 동일하게 타입별 서브폴더에 넣는다. 예: Jelly_01 → StageObjects/Jelly/Jelly_01.prefab
        string categoryFolder = $"{GeneratedPrefabFolder}/{GetCategoryName(sprite.name)}";
        string prefabPath = $"{categoryFolder}/{sprite.name}.prefab";

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null) return existing;

        EnsureFolder(categoryFolder);

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

    // 에셋 이름에서 타입(카테고리) 폴더명을 얻는다. 첫 언더스코어 앞부분. 예: Eye_Sclera_01 → Eye
    private static string GetCategoryName(string assetName)
    {
        int underscore = assetName.IndexOf('_');
        return underscore > 0 ? assetName.Substring(0, underscore) : assetName;
    }

    private static void EnsureStageObjectComponent(GameObject prefab)
    {
        if (prefab.GetComponentInChildren<StageObject>() != null) return;

        using var edit = new PrefabUtility.EditPrefabContentsScope(AssetDatabase.GetAssetPath(prefab));
        var root = edit.prefabContentsRoot;

        var collider = root.GetComponentInChildren<Collider2D>();
        var target = collider != null ? collider.gameObject : root;

        target.AddComponent<StageObject>();
        Debug.Log($"[StageMaker] StageObject 컴포넌트 추가: {prefab.name}/{target.name}");
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

    private void ResetWorkingData()
    {
        if (_isEditMode) ExitEditMode();
        _loadedSO = null;

        RecordUndo("Reset Stage Maker");
        _data.stageId = DefaultStageId;
        _data.squishSpawnPosition = DefaultSquishSpawnPosition;
        _data.armPosition = DefaultArmPosition;
        _data.cameraCenter = Vector2.zero;
        _data.cameraOrthoSize = 6f;
        _data.backgroundColor = StageDataSO.DefaultBackgroundColor;
        _data.stageObjects.Clear();

        Repaint();
    }

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
