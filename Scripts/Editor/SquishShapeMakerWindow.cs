#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 스퀴시 셰이프 저작 툴 — 자체 캔버스 방식(Unity Sprite Editor 스타일).
// - 스프라이트 한 장을 캔버스에 띄우고 그 위에서 스파인 제어점(중심선 + 국소 폭)을 찍어
//   SquishBody 컴포넌트에 직접 저장한다.
// - Auto 생성: 불투명 픽셀 중심선(medial)을 곡률 기반으로 데시메이션해 제어점 시드를 만든다.
public class SquishShapeMakerWindow : EditorWindow
{
    private const float HandlePickRadius = 10f;    // 제어점 마우스 히트 반경(px)
    private const float SidebarWidth = 264f;

    // Undo 지원 작업 데이터 (에셋으로 저장하지 않음).
    private class WorkingData : ScriptableObject
    {
        public Sprite sprite;
        public List<SpinePoint> controlPoints = new();
        public bool smoothSpine = true;
        public bool bendEnabled = false;
        public int boneCount = 11;
        public int meshCols = 20;
        public int meshRows = 5;
        public float bendStiffness = 60f;
        public float bendDamping = 8f;
        public float maxBendAnglePerJoint = 18f;
        public float totalMass = 1.2f;
        public float gravityScale = 1.5f;
        public SquishBody target;
    }

    private WorkingData _data;

    // 에디터 UI 상태 (Undo 불필요).
    private int _autoTargetPoints = 6;
    private Vector2 _sidebarScroll;

    // 캔버스 뷰 상태.
    private float _zoom = 1f;
    private Vector2 _pan = Vector2.zero;
    private bool _viewInit;

    // 상호작용 상태.
    private int _dragPoint = -1;
    private bool _dragWidth;

    // 현재 프레임 좌표 변환 컨텍스트 (DrawCanvas에서 채움).
    private float _fitScale = 1f;
    private Vector2 _canvasSize;

    [MenuItem("BananaGallery/Squish/Squish Shape Maker")]
    public static void Open()
    {
        var w = GetWindow<SquishShapeMakerWindow>("Squish Shape Maker");
        w.minSize = new Vector2(720f, 460f);
        w.Show();
    }

    public static void Open(SquishBody target)
    {
        var w = GetWindow<SquishShapeMakerWindow>("Squish Shape Maker");
        w.minSize = new Vector2(720f, 460f);
        w.EnsureData();
        w._data.target = target;
        w.LoadFromBody();
        w.Show();
    }

    private void OnEnable()
    {
        EnsureData();

        if (Selection.activeObject is Sprite s) _data.sprite = s;
        else if (Selection.activeObject is Texture2D tex)
        {
            foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(tex)))
                if (o is Sprite spr) { _data.sprite = spr; break; }
        }
    }

    private void OnDisable()
    {
        if (_data != null) DestroyImmediate(_data);
    }

    private void OnGUI()
    {
        EnsureData();
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawSidebar();
            DrawCanvas();
        }
    }

    private void EnsureData()
    {
        if (_data != null) return;
        _data = CreateInstance<WorkingData>();
        _data.hideFlags = HideFlags.DontSave;
    }

    // ---------- 좌측 컨트롤 패널 ----------

    private void DrawSidebar()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(SidebarWidth)))
        {
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

            EditorGUILayout.LabelField("스프라이트", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var sp = (Sprite)EditorGUILayout.ObjectField(_data.sprite, typeof(Sprite), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_data, "Change Sprite");
                _data.sprite = sp;
                _viewInit = false;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("스파인 편집", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("캔버스 — 점 드래그: 이동 / 파란 점: 폭 / Shift+클릭: 추가 / 우클릭: 삭제 / 휠: 줌 / Alt·중클릭 드래그: 팬", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("직선 초기화")) InitLine();
                if (GUILayout.Button("끝에 추가")) AddPointAtEnd();
                if (GUILayout.Button("마지막 삭제")) RemoveLast();
            }

            EditorGUILayout.Space();
            _autoTargetPoints = EditorGUILayout.IntSlider("Auto 목표 점 수", _autoTargetPoints, 2, 16);
            using (new EditorGUI.DisabledScope(_data.sprite == null))
                if (GUILayout.Button("⚡ 스프라이트에서 Auto 생성", GUILayout.Height(24)))
                    AutoGenerateFromSprite();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("형태 / 물리 파라미터", EditorStyles.boldLabel);
            _data.smoothSpine = EditorGUILayout.Toggle("Smooth Spine", _data.smoothSpine);
            _data.bendEnabled = EditorGUILayout.Toggle("Bend Enabled", _data.bendEnabled);
            _data.boneCount = EditorGUILayout.IntSlider("Bone Count", _data.boneCount, 2, 32);
            _data.meshCols = EditorGUILayout.IntSlider("Mesh Cols", _data.meshCols, 2, 64);
            _data.meshRows = EditorGUILayout.IntSlider("Mesh Rows", _data.meshRows, 2, 16);
            _data.totalMass = EditorGUILayout.FloatField("Total Mass", _data.totalMass);
            _data.gravityScale = EditorGUILayout.FloatField("Gravity Scale", _data.gravityScale);
            _data.bendStiffness = EditorGUILayout.FloatField("Bend Stiffness", _data.bendStiffness);
            _data.bendDamping = EditorGUILayout.FloatField("Bend Damping", _data.bendDamping);
            _data.maxBendAnglePerJoint = EditorGUILayout.FloatField("Max Bend Angle/Joint", _data.maxBendAnglePerJoint);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("저장 / 불러오기", EditorStyles.boldLabel);
            _data.target = (SquishBody)EditorGUILayout.ObjectField("대상 SquishBody", _data.target, typeof(SquishBody), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_data.sprite == null || _data.controlPoints.Count < 2 || _data.target == null))
                    if (GUILayout.Button("▶ Body에 저장", GUILayout.Height(26))) SaveToBody();
                using (new EditorGUI.DisabledScope(_data.target == null))
                    if (GUILayout.Button("Body에서 불러오기", GUILayout.Height(26))) LoadFromBody();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("뷰 리셋(Fit)")) _viewInit = false;
            EditorGUILayout.LabelField($"제어점 {_data.controlPoints.Count}개", EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();
        }
    }

    // ---------- 우측 캔버스 ----------

    private void DrawCanvas()
    {
        Rect canvas = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (Event.current.type == EventType.Repaint)
            EditorGUI.DrawRect(canvas, new Color(0.16f, 0.16f, 0.16f));

        if (_data.sprite == null)
        {
            GUI.Label(canvas, "스프라이트를 지정하세요", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        _canvasSize = canvas.size;
        EnsureViewInit();
        ComputeFit();

        GUI.BeginClip(canvas);
        if (Event.current.type == EventType.Repaint)
        {
            DrawSpriteTexture();
            DrawOverlays();
            DrawControlHandles();
        }
        HandleCanvasInput();
        GUI.EndClip();
    }

    private void EnsureViewInit()
    {
        if (_viewInit) return;
        _zoom = 1f;
        _pan = Vector2.zero;
        _viewInit = true;
    }

    private void ComputeFit()
    {
        Vector2 size = _data.sprite.bounds.size;
        if (size.x < 1e-4f || size.y < 1e-4f) { _fitScale = 100f * _zoom; return; }
        float margin = 0.85f;
        _fitScale = Mathf.Min(_canvasSize.x / size.x, _canvasSize.y / size.y) * margin * _zoom;
    }

    private Vector2 LocalToCanvas(Vector2 local)
    {
        Vector3 c = _data.sprite.bounds.center;
        Vector2 origin = _canvasSize * 0.5f + _pan;
        return new Vector2(origin.x + (local.x - c.x) * _fitScale, origin.y - (local.y - c.y) * _fitScale);
    }

    private Vector2 CanvasToLocal(Vector2 gui)
    {
        Vector3 c = _data.sprite.bounds.center;
        Vector2 origin = _canvasSize * 0.5f + _pan;
        return new Vector2(c.x + (gui.x - origin.x) / _fitScale, c.y - (gui.y - origin.y) / _fitScale);
    }

    private void DrawSpriteTexture()
    {
        var sprite = _data.sprite;
        Bounds b = sprite.bounds;
        Vector2 topLeft = LocalToCanvas(new Vector2(b.center.x - b.extents.x, b.center.y + b.extents.y));
        Rect dst = new Rect(topLeft.x, topLeft.y, b.size.x * _fitScale, b.size.y * _fitScale);

        var tex = sprite.texture;
        Rect r = sprite.rect;
        Rect uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
        GUI.DrawTextureWithTexCoords(dst, tex, uv, true);
    }

    private void DrawOverlays()
    {
        var cps = _data.controlPoints;
        if (cps.Count < 2) return;
        var arr = cps.ToArray();
        var samples = SpineSampler.Resample(arr, Mathf.Max(_data.meshCols, 24), _data.smoothSpine);

        Handles.color = Color.cyan;
        for (int i = 0; i < samples.Length - 1; i++)
            Handles.DrawAAPolyLine(3f, ToV3(LocalToCanvas(samples[i].position)), ToV3(LocalToCanvas(samples[i + 1].position)));

        Handles.color = new Color(1f, 0.15f, 0.9f, 1f);
        for (int i = 0; i < samples.Length - 1; i++)
        {
            DrawRibbonEdge(samples[i], samples[i + 1], +1f);
            DrawRibbonEdge(samples[i], samples[i + 1], -1f);
        }

        var bones = SpineSampler.Resample(arr, _data.boneCount, _data.smoothSpine);
        Handles.color = new Color(0.2f, 1f, 0.4f, 0.85f);
        foreach (var bpt in bones)
            Handles.DrawSolidDisc(ToV3(LocalToCanvas(bpt.position)), Vector3.forward, 3f);
    }

    private void DrawRibbonEdge(SpineSample a, SpineSample b, float side)
    {
        Vector2 na = new Vector2(-Mathf.Sin(a.angleDeg * Mathf.Deg2Rad), Mathf.Cos(a.angleDeg * Mathf.Deg2Rad));
        Vector2 nb = new Vector2(-Mathf.Sin(b.angleDeg * Mathf.Deg2Rad), Mathf.Cos(b.angleDeg * Mathf.Deg2Rad));
        Handles.DrawAAPolyLine(4f,
            ToV3(LocalToCanvas(a.position + na * (side * a.halfWidth))),
            ToV3(LocalToCanvas(b.position + nb * (side * b.halfWidth))));
    }

    private void DrawControlHandles()
    {
        var cps = _data.controlPoints;
        for (int i = 0; i < cps.Count; i++)
        {
            Vector2 p = LocalToCanvas(cps[i].position);

            Vector2 n = LocalNormal(i);
            Vector2 wp = LocalToCanvas(cps[i].position + n * cps[i].halfWidth);
            Handles.color = new Color(0.3f, 0.6f, 1f, 0.5f);
            Handles.DrawAAPolyLine(1.5f, ToV3(p), ToV3(wp));
            Handles.color = (i == _dragPoint && _dragWidth) ? Color.yellow : new Color(0.3f, 0.6f, 1f);
            Handles.DrawSolidDisc(ToV3(wp), Vector3.forward, 4f);

            Handles.color = (i == _dragPoint && !_dragWidth) ? Color.yellow : Color.white;
            Handles.DrawSolidDisc(ToV3(p), Vector3.forward, 5f);
        }
    }

    private void HandleCanvasInput()
    {
        Event e = Event.current;
        Vector2 m = e.mousePosition;

        switch (e.type)
        {
            case EventType.ScrollWheel:
                _zoom = Mathf.Clamp(_zoom * (1f - e.delta.y * 0.05f), 0.1f, 20f);
                e.Use();
                Repaint();
                break;

            case EventType.MouseDown:
                if (e.button == 2 || (e.button == 0 && e.alt)) { e.Use(); break; }
                if (e.button == 1)
                {
                    int hit = PickPoint(m);
                    if (hit >= 0)
                    {
                        Undo.RecordObject(_data, "Delete Spine Point");
                        _data.controlPoints.RemoveAt(hit);
                        e.Use();
                        Repaint();
                    }
                    break;
                }
                if (e.button == 0)
                {
                    if (e.shift)
                    {
                        Undo.RecordObject(_data, "Add Spine Point");
                        var cps = _data.controlPoints;
                        _data.controlPoints.Add(new SpinePoint
                        {
                            position = CanvasToLocal(m),
                            halfWidth = cps.Count > 0 ? cps[cps.Count - 1].halfWidth : 0.25f
                        });
                        e.Use();
                        Repaint();
                        break;
                    }
                    int w = PickWidthHandle(m);
                    if (w >= 0) { _dragPoint = w; _dragWidth = true; e.Use(); break; }
                    int pt = PickPoint(m);
                    if (pt >= 0) { _dragPoint = pt; _dragWidth = false; e.Use(); }
                }
                break;

            case EventType.MouseDrag:
                if (e.button == 2 || (e.button == 0 && e.alt)) { _pan += e.delta; e.Use(); Repaint(); break; }
                if (_dragPoint >= 0 && _dragPoint < _data.controlPoints.Count)
                {
                    Undo.RecordObject(_data, _dragWidth ? "Adjust Width" : "Move Spine Point");
                    var cps = _data.controlPoints;
                    var pt = cps[_dragPoint];
                    if (_dragWidth)
                        pt.halfWidth = Mathf.Max(0.02f, Vector2.Distance(CanvasToLocal(m), pt.position));
                    else
                        pt.position = CanvasToLocal(m);
                    cps[_dragPoint] = pt;
                    e.Use();
                    Repaint();
                }
                break;

            case EventType.MouseUp:
                _dragPoint = -1;
                _dragWidth = false;
                break;
        }
    }

    private int PickPoint(Vector2 m)
    {
        var cps = _data.controlPoints;
        int best = -1;
        float bestD = HandlePickRadius;
        for (int i = 0; i < cps.Count; i++)
        {
            float d = Vector2.Distance(m, LocalToCanvas(cps[i].position));
            if (d <= bestD) { bestD = d; best = i; }
        }
        return best;
    }

    private int PickWidthHandle(Vector2 m)
    {
        var cps = _data.controlPoints;
        int best = -1;
        float bestD = HandlePickRadius;
        for (int i = 0; i < cps.Count; i++)
        {
            Vector2 wp = LocalToCanvas(cps[i].position + LocalNormal(i) * cps[i].halfWidth);
            float d = Vector2.Distance(m, wp);
            if (d <= bestD) { bestD = d; best = i; }
        }
        return best;
    }

    private Vector2 LocalNormal(int i)
    {
        var cps = _data.controlPoints;
        if (cps.Count < 2) return Vector2.up;

        Vector2 tangent;
        if (i == 0) tangent = cps[1].position - cps[0].position;
        else if (i == cps.Count - 1) tangent = cps[i].position - cps[i - 1].position;
        else tangent = cps[i + 1].position - cps[i - 1].position;

        tangent = tangent.normalized;
        return new Vector2(-tangent.y, tangent.x);
    }

    private static Vector3 ToV3(Vector2 v) => new Vector3(v.x, v.y, 0f);

    // ---------- 스파인 버튼 액션 ----------

    private void InitLine()
    {
        if (_data.sprite == null) return;
        Undo.RecordObject(_data, "Init Spine");
        float half = _data.sprite.bounds.extents.x * 0.7f;
        float hw = _data.sprite.bounds.extents.y * 0.4f;
        _data.controlPoints.Clear();
        _data.controlPoints.Add(new SpinePoint { position = new Vector2(-half, 0f), halfWidth = hw });
        _data.controlPoints.Add(new SpinePoint { position = new Vector2(half, 0f), halfWidth = hw });
        Repaint();
    }

    private void AddPointAtEnd()
    {
        Undo.RecordObject(_data, "Add Spine Point");
        var cps = _data.controlPoints;
        if (cps.Count == 0) { InitLine(); return; }
        if (cps.Count == 1) { cps.Add(new SpinePoint { position = cps[0].position + Vector2.right, halfWidth = cps[0].halfWidth }); return; }

        var last = cps[cps.Count - 1];
        var prev = cps[cps.Count - 2];
        cps.Add(new SpinePoint { position = last.position + (last.position - prev.position), halfWidth = last.halfWidth });
        Repaint();
    }

    private void RemoveLast()
    {
        if (_data.controlPoints.Count == 0) return;
        Undo.RecordObject(_data, "Remove Spine Point");
        _data.controlPoints.RemoveAt(_data.controlPoints.Count - 1);
        Repaint();
    }

    private void AutoGenerateFromSprite()
    {
        if (_data.sprite == null)
        {
            Debug.LogWarning("[Squish] Auto 생성: 스프라이트가 지정되지 않았습니다.");
            return;
        }

        if (!SquishSpineAutoGenerator.TryGenerate(_data.sprite, _autoTargetPoints, out var points))
            return;

        Undo.RecordObject(_data, "Auto Generate Spine");
        _data.controlPoints.Clear();
        foreach (var pt in points)
            _data.controlPoints.Add(pt);

        Repaint();
    }

    // ---------- 저장 / 불러오기 ----------

    private void SaveToBody()
    {
        var body = _data.target;
        if (body == null) return;

        // SquishBody에 스파인 + 물리 파라미터 저장 (SerializedObject로 private 필드 접근)
        var bodySo = new SerializedObject(body);

        var cpProp = bodySo.FindProperty("controlPoints");
        cpProp.arraySize = _data.controlPoints.Count;
        for (int i = 0; i < _data.controlPoints.Count; i++)
        {
            var elem = cpProp.GetArrayElementAtIndex(i);
            elem.FindPropertyRelative("position").vector2Value = _data.controlPoints[i].position;
            elem.FindPropertyRelative("halfWidth").floatValue = _data.controlPoints[i].halfWidth;
        }
        bodySo.FindProperty("smoothSpine").boolValue = _data.smoothSpine;
        bodySo.FindProperty("bendEnabled").boolValue = _data.bendEnabled;
        bodySo.FindProperty("boneCount").intValue = _data.boneCount;
        bodySo.FindProperty("totalMass").floatValue = _data.totalMass;
        bodySo.FindProperty("gravityScale").floatValue = _data.gravityScale;
        bodySo.FindProperty("bendStiffness").floatValue = _data.bendStiffness;
        bodySo.FindProperty("bendDamping").floatValue = _data.bendDamping;
        bodySo.FindProperty("maxBendAnglePerJoint").floatValue = _data.maxBendAnglePerJoint;

        bodySo.ApplyModifiedProperties();

        // 같은 GO의 SquishMesh에 메쉬 해상도 저장
        var mesh = body.GetComponent<SquishMesh>();
        if (mesh != null)
        {
            var meshSo = new SerializedObject(mesh);
            meshSo.FindProperty("meshCols").intValue = _data.meshCols;
            meshSo.FindProperty("meshRows").intValue = _data.meshRows;
            meshSo.ApplyModifiedProperties();
        }

        // SquishSkinSO에 불투명영역 저장
        if (mesh != null && mesh.SkinSO != null && _data.sprite != null)
        {
            mesh.SkinSO.opaqueUVRect = SquishSpriteUtil.ComputeOpaqueUVRect(_data.sprite);
            EditorUtility.SetDirty(mesh.SkinSO);
        }

        Debug.Log($"[Squish] Body 저장 완료: {body.name}  (제어점 {_data.controlPoints.Count}개)");
    }

    private void LoadFromBody()
    {
        var body = _data.target;
        if (body == null) return;

        Undo.RecordObject(_data, "Load Shape");

        // SquishBody에서 읽기 (SerializedObject로 private 필드 접근)
        var bodySo = new SerializedObject(body);

        var cpProp = bodySo.FindProperty("controlPoints");
        _data.controlPoints.Clear();
        for (int i = 0; i < cpProp.arraySize; i++)
        {
            var elem = cpProp.GetArrayElementAtIndex(i);
            _data.controlPoints.Add(new SpinePoint
            {
                position = elem.FindPropertyRelative("position").vector2Value,
                halfWidth = elem.FindPropertyRelative("halfWidth").floatValue
            });
        }
        _data.smoothSpine = bodySo.FindProperty("smoothSpine").boolValue;
        _data.bendEnabled = bodySo.FindProperty("bendEnabled").boolValue;
        _data.boneCount = bodySo.FindProperty("boneCount").intValue;
        _data.totalMass = bodySo.FindProperty("totalMass").floatValue;
        _data.gravityScale = bodySo.FindProperty("gravityScale").floatValue;
        _data.bendStiffness = bodySo.FindProperty("bendStiffness").floatValue;
        _data.bendDamping = bodySo.FindProperty("bendDamping").floatValue;
        _data.maxBendAnglePerJoint = bodySo.FindProperty("maxBendAnglePerJoint").floatValue;

        // SquishMesh에서 메쉬 해상도 읽기
        var mesh = body.GetComponent<SquishMesh>();
        if (mesh != null)
        {
            var meshSo = new SerializedObject(mesh);
            _data.meshCols = meshSo.FindProperty("meshCols").intValue;
            _data.meshRows = meshSo.FindProperty("meshRows").intValue;
        }

        // SquishSkinSO에서 스프라이트 읽기
        if (mesh != null && mesh.SkinSO != null && mesh.SkinSO.sprite != null)
            _data.sprite = mesh.SkinSO.sprite;

        _viewInit = false;
        Repaint();
    }
}
#endif
