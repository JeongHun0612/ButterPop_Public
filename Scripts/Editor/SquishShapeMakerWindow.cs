#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 스퀴시 셰이프(SquishShapeSO) 저작 툴 — 자체 캔버스 방식(Unity Sprite Editor 스타일).
// - 씬/런타임에 의존하지 않는 독립 EditorWindow. 스프라이트 한 장을 캔버스에 띄우고
//   그 위에서 스파인 제어점(중심선 + 국소 폭)을 찍어 SquishShapeSO로 저장한다.
// - Auto 생성: 불투명 픽셀 중심선(medial)을 곡률 기반으로 데시메이션해 제어점 시드를 만든다.
public class SquishShapeMakerWindow : EditorWindow
{
    private const string ShapeDir = "Assets/08. ScriptableObjects/SquishShapes";
    private const byte AlphaThreshold = 25;        // 불투명 판정 알파
    private const float HandlePickRadius = 10f;    // 제어점 마우스 히트 반경(px)
    private const float SidebarWidth = 264f;

    // Undo 지원 작업 데이터 (에셋으로 저장하지 않음).
    private class WorkingData : ScriptableObject
    {
        public Sprite sprite;
        public List<SquishShapeSO.SpinePoint> controlPoints = new();
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
        public SquishShapeSO target;
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

    private void OnEnable()
    {
        EnsureData();

        // 프로젝트에서 스프라이트를 선택 중이면 자동 로드.
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
            _data.target = (SquishShapeSO)EditorGUILayout.ObjectField("대상 SO", _data.target, typeof(SquishShapeSO), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_data.sprite == null || _data.controlPoints.Count < 2))
                    if (GUILayout.Button("▶ SO에 저장", GUILayout.Height(26))) SaveToSO();
                using (new EditorGUI.DisabledScope(_data.target == null))
                    if (GUILayout.Button("SO에서 불러오기", GUILayout.Height(26))) LoadFromSO();
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

        GUI.BeginClip(canvas);   // 이하 좌표는 캔버스 원점(0,0) 기준
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

        // 스파인 중심선
        Handles.color = Color.cyan;
        for (int i = 0; i < samples.Length - 1; i++)
            Handles.DrawAAPolyLine(3f, ToV3(LocalToCanvas(samples[i].position)), ToV3(LocalToCanvas(samples[i + 1].position)));

        // 리본 외곽 (±법선*폭)
        Handles.color = new Color(1f, 0.15f, 0.9f, 1f);
        for (int i = 0; i < samples.Length - 1; i++)
        {
            DrawRibbonEdge(samples[i], samples[i + 1], +1f);
            DrawRibbonEdge(samples[i], samples[i + 1], -1f);
        }

        // 리샘플된 뼈
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

            // 폭 핸들 연결선 + 점
            Vector2 n = LocalNormal(i);
            Vector2 wp = LocalToCanvas(cps[i].position + n * cps[i].halfWidth);
            Handles.color = new Color(0.3f, 0.6f, 1f, 0.5f);
            Handles.DrawAAPolyLine(1.5f, ToV3(p), ToV3(wp));
            Handles.color = (i == _dragPoint && _dragWidth) ? Color.yellow : new Color(0.3f, 0.6f, 1f);
            Handles.DrawSolidDisc(ToV3(wp), Vector3.forward, 4f);

            // 제어점
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
                if (e.button == 2 || (e.button == 0 && e.alt)) { e.Use(); break; }   // 팬은 드래그에서 처리
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
                        _data.controlPoints.Add(new SquishShapeSO.SpinePoint
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
        _data.controlPoints.Add(new SquishShapeSO.SpinePoint { position = new Vector2(-half, 0f), halfWidth = hw });
        _data.controlPoints.Add(new SquishShapeSO.SpinePoint { position = new Vector2(half, 0f), halfWidth = hw });
        Repaint();
    }

    private void AddPointAtEnd()
    {
        Undo.RecordObject(_data, "Add Spine Point");
        var cps = _data.controlPoints;
        if (cps.Count == 0) { InitLine(); return; }
        if (cps.Count == 1) { cps.Add(new SquishShapeSO.SpinePoint { position = cps[0].position + Vector2.right, halfWidth = cps[0].halfWidth }); return; }

        var last = cps[cps.Count - 1];
        var prev = cps[cps.Count - 2];
        cps.Add(new SquishShapeSO.SpinePoint { position = last.position + (last.position - prev.position), halfWidth = last.halfWidth });
        Repaint();
    }

    private void RemoveLast()
    {
        if (_data.controlPoints.Count == 0) return;
        Undo.RecordObject(_data, "Remove Spine Point");
        _data.controlPoints.RemoveAt(_data.controlPoints.Count - 1);
        Repaint();
    }

    // ---------- Auto 생성 (스프라이트 → 중심선 → 곡률 기반 데시메이션) ----------

    private void AutoGenerateFromSprite()
    {
        if (_data.sprite == null)
        {
            Debug.LogWarning("[Squish] Auto 생성: 스프라이트가 지정되지 않았습니다.");
            return;
        }

        if (!TryBuildMedial(_data.sprite, out var medial, out var half))
        {
            Debug.LogWarning("[Squish] Auto 생성 실패: 불투명 픽셀을 찾지 못했습니다. (텍스처 읽기 실패 또는 알파가 전부 투명)");
            return;
        }

        SmoothHalf(half, 2);

        float ppu = Mathf.Max(0.0001f, _data.sprite.pixelsPerUnit);
        float axisLen = Vector2.Distance(medial[0], medial[medial.Count - 1]);
        float epsilon = Mathf.Max(1f / ppu, axisLen * 0.0075f);   // 이보다 작은 굴곡은 무시 → 직선은 2점으로 수렴

        var keep = DecimateByCurvature(medial, _autoTargetPoints, epsilon);

        Undo.RecordObject(_data, "Auto Generate Spine");
        _data.controlPoints.Clear();
        foreach (int i in keep)
            _data.controlPoints.Add(new SquishShapeSO.SpinePoint
            {
                position = medial[i],
                halfWidth = Mathf.Max(0.02f, half[i])
            });

        Repaint();
        Debug.Log($"[Squish] Auto 생성 완료: 중심선 {medial.Count}칼럼 → 제어점 {_data.controlPoints.Count}개 (목표 {_autoTargetPoints}).");
    }

    // 스프라이트 불투명 영역을 스캔해 긴 축을 따라 중심선(medial) 점 + 국소 반폭을 sprite-local로 뽑는다.
    private static bool TryBuildMedial(Sprite sprite, out List<Vector2> medial, out List<float> half)
    {
        medial = new List<Vector2>();
        half = new List<float>();

        string path = AssetDatabase.GetAssetPath(sprite.texture);
        Texture2D readable = null;
        Color32[] px;
        int texW, texH;
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            readable = new Texture2D(2, 2);
            if (!readable.LoadImage(bytes)) { Object.DestroyImmediate(readable); return false; }
            px = readable.GetPixels32();
            texW = readable.width;
            texH = readable.height;
        }
        catch { if (readable != null) Object.DestroyImmediate(readable); return false; }
        Object.DestroyImmediate(readable);

        Rect r = sprite.rect;
        int rx = Mathf.RoundToInt(r.x), ry = Mathf.RoundToInt(r.y);
        int rw = Mathf.RoundToInt(r.width), rh = Mathf.RoundToInt(r.height);
        if (rw <= 1 || rh <= 1 || rx < 0 || ry < 0 || rx + rw > texW || ry + rh > texH) return false;

        var mask = new bool[rw * rh];
        int minX = rw, maxX = -1, minY = rh, maxY = -1;
        for (int y = 0; y < rh; y++)
            for (int x = 0; x < rw; x++)
            {
                bool op = px[(ry + y) * texW + (rx + x)].a > AlphaThreshold;
                mask[y * rw + x] = op;
                if (op)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        if (maxX < 0) return false;

        bool horizontal = (maxX - minX) >= (maxY - minY);
        float ppu = Mathf.Max(0.0001f, sprite.pixelsPerUnit);
        Vector2 pivot = sprite.pivot;   // rect 좌하단 기준 픽셀

        if (horizontal)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int lo = -1, hi = -1;
                for (int y = minY; y <= maxY; y++)
                    if (mask[y * rw + x]) { if (lo < 0) lo = y; hi = y; }
                if (lo < 0) continue;   // 불투명 없는 칼럼(구멍)은 건너뜀
                medial.Add(new Vector2((x + 0.5f - pivot.x) / ppu, ((lo + hi) * 0.5f + 0.5f - pivot.y) / ppu));
                half.Add((hi - lo + 1) * 0.5f / ppu);
            }
        }
        else
        {
            for (int y = minY; y <= maxY; y++)
            {
                int lo = -1, hi = -1;
                for (int x = minX; x <= maxX; x++)
                    if (mask[y * rw + x]) { if (lo < 0) lo = x; hi = x; }
                if (lo < 0) continue;
                medial.Add(new Vector2(((lo + hi) * 0.5f + 0.5f - pivot.x) / ppu, (y + 0.5f - pivot.y) / ppu));
                half.Add((hi - lo + 1) * 0.5f / ppu);
            }
        }

        return medial.Count >= 2;
    }

    // 인접 반폭 이동평균(±window)으로 픽셀 단위 지터를 완화.
    private static void SmoothHalf(List<float> half, int window)
    {
        if (half.Count < 3 || window < 1) return;
        var src = new List<float>(half);
        for (int i = 0; i < half.Count; i++)
        {
            float sum = 0f; int cnt = 0;
            for (int k = -window; k <= window; k++)
            {
                int j = i + k;
                if (j >= 0 && j < src.Count) { sum += src[j]; cnt++; }
            }
            half[i] = sum / cnt;
        }
    }

    // 우선순위 Douglas-Peucker: 편차가 가장 큰 점부터 targetCount개까지 유지. 편차 < epsilon이면 조기 종료.
    private static List<int> DecimateByCurvature(IReadOnlyList<Vector2> pts, int targetCount, float epsilon)
    {
        int n = pts.Count;
        var kept = new List<int> { 0, n - 1 };
        targetCount = Mathf.Clamp(targetCount, 2, n);
        if (n <= 2 || targetCount <= 2) return kept;

        var segments = new List<(int a, int b)> { (0, n - 1) };
        while (kept.Count < targetCount)
        {
            float best = -1f; int bestSeg = -1, bestPt = -1;
            for (int s = 0; s < segments.Count; s++)
            {
                var (a, b) = segments[s];
                if (b - a < 2) continue;
                MaxDeviation(pts, a, b, out int mi, out float md);
                if (md > best) { best = md; bestSeg = s; bestPt = mi; }
            }
            if (bestSeg < 0 || best < epsilon) break;

            var seg = segments[bestSeg];
            segments.RemoveAt(bestSeg);
            segments.Add((seg.a, bestPt));
            segments.Add((bestPt, seg.b));
            kept.Add(bestPt);
        }

        kept.Sort();
        return kept;
    }

    // 선분 (a,b)에서 수직 편차가 최대인 내부 점 인덱스와 거리.
    private static void MaxDeviation(IReadOnlyList<Vector2> pts, int a, int b, out int idx, out float dist)
    {
        idx = -1; dist = -1f;
        Vector2 pa = pts[a], pb = pts[b];
        Vector2 ab = pb - pa;
        float abLen = ab.magnitude;
        for (int i = a + 1; i < b; i++)
        {
            float d = abLen < 1e-6f
                ? Vector2.Distance(pts[i], pa)
                : Mathf.Abs((pts[i].x - pa.x) * ab.y - (pts[i].y - pa.y) * ab.x) / abLen;
            if (d > dist) { dist = d; idx = i; }
        }
    }

    // ---------- 저장 / 불러오기 ----------

    private void SaveToSO()
    {
        var so = _data.target;
        if (so == null)
        {
            string defaultName = (_data.sprite != null ? _data.sprite.name : "Squish") + "_Shape";
            string path = EditorUtility.SaveFilePanelInProject("SquishShape 저장", defaultName, "asset", "", ShapeDir);
            if (string.IsNullOrEmpty(path)) return;

            so = CreateInstance<SquishShapeSO>();
            AssetDatabase.CreateAsset(so, path);
            _data.target = so;
        }

        so.sprite = _data.sprite;
        so.controlPoints = _data.controlPoints.ToArray();
        so.smoothSpine = _data.smoothSpine;
        so.bendEnabled = _data.bendEnabled;
        so.boneCount = _data.boneCount;
        so.meshCols = _data.meshCols;
        so.meshRows = _data.meshRows;
        so.bendStiffness = _data.bendStiffness;
        so.bendDamping = _data.bendDamping;
        so.maxBendAnglePerJoint = _data.maxBendAnglePerJoint;
        so.totalMass = _data.totalMass;
        so.gravityScale = _data.gravityScale;
        so.opaqueUVRect = ComputeOpaqueUVRect(_data.sprite);

        EditorUtility.SetDirty(so);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Squish] SO 저장 완료: {AssetDatabase.GetAssetPath(so)}  (불투명영역 {so.opaqueUVRect})");
    }

    private void LoadFromSO()
    {
        var so = _data.target;
        if (so == null) return;

        Undo.RecordObject(_data, "Load Shape");
        _data.sprite = so.sprite;
        _data.controlPoints = new List<SquishShapeSO.SpinePoint>(so.controlPoints ?? new SquishShapeSO.SpinePoint[0]);
        _data.smoothSpine = so.smoothSpine;
        _data.bendEnabled = so.bendEnabled;
        _data.boneCount = so.boneCount;
        _data.meshCols = so.meshCols;
        _data.meshRows = so.meshRows;
        _data.bendStiffness = so.bendStiffness;
        _data.bendDamping = so.bendDamping;
        _data.maxBendAnglePerJoint = so.maxBendAnglePerJoint;
        _data.totalMass = so.totalMass;
        _data.gravityScale = so.gravityScale;
        _viewInit = false;
        Repaint();
    }

    // 스프라이트 텍스처를 파일에서 읽어(임포트 설정 무관) sprite.rect 내 불투명 영역을 0~1로 계산.
    private static Rect ComputeOpaqueUVRect(Sprite sprite)
    {
        var full = new Rect(0f, 0f, 1f, 1f);
        if (sprite == null) return full;

        string path = AssetDatabase.GetAssetPath(sprite.texture);
        Texture2D readable = null;
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            readable = new Texture2D(2, 2);
            if (!readable.LoadImage(bytes)) { Object.DestroyImmediate(readable); return full; }
        }
        catch { if (readable != null) Object.DestroyImmediate(readable); return full; }

        Rect r = sprite.rect;
        int rx = Mathf.RoundToInt(r.x), ry = Mathf.RoundToInt(r.y);
        int rw = Mathf.RoundToInt(r.width), rh = Mathf.RoundToInt(r.height);
        if (rx < 0 || ry < 0 || rx + rw > readable.width || ry + rh > readable.height)
        {
            Object.DestroyImmediate(readable);
            return full;
        }

        Color32[] px = readable.GetPixels32();
        int texW = readable.width;
        int minX = rw, minY = rh, maxX = -1, maxY = -1;
        for (int y = 0; y < rh; y++)
            for (int x = 0; x < rw; x++)
                if (px[(ry + y) * texW + (rx + x)].a > AlphaThreshold)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }

        Object.DestroyImmediate(readable);
        if (maxX < 0) return full;

        return new Rect(minX / (float)rw, minY / (float)rh, (maxX - minX + 1) / (float)rw, (maxY - minY + 1) / (float)rh);
    }
}
#endif
