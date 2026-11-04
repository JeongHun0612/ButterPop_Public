using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 최초 로딩 화면 인스펙터에 인트로 미리보기를 붙인다. 프리팹 모드에서 플레이 없이 재생·스크럽한다.
// 미리보기는 선택을 바꿔도 이어지므로 다른 오브젝트의 커브·변형 값을 고치면서 볼 수 있다.
// 바뀐 배치는 정지·프리팹 모드 종료·플레이 진입·스크립트 리로드 때 시작 전 상태로 되돌리고,
// 저장할 때는 시작 전 상태로 저장한 뒤 미리보기를 이어 간다.
[CustomEditor(typeof(UIInitialLoading))]
public class UIInitialLoadingEditor : Editor
{
    private const float LoopHold = 1f;
    private const double PausedRefreshInterval = 0.1;

    private static readonly float[] Speeds = { 1f, 0.5f, 0.25f, 0.1f };
    private static readonly string[] SpeedLabels = { "1x", "0.5x", "0.25x", "0.1x" };

    private static UIInitialLoading _previewTarget;
    private static bool _playing;
    private static bool _loop = true;
    private static bool _restoredForSave;
    private static int _speedIndex;
    private static float _time;
    private static double _lastTick;

    private UIInitialLoading Loading => (UIInitialLoading)target;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("인트로 미리보기", EditorStyles.boldLabel);

        if (PrefabStageUtility.GetPrefabStage(Loading.gameObject) == null)
        {
            EditorGUILayout.HelpBox("프리팹 모드에서 플레이 없이 미리볼 수 있습니다.", MessageType.Info);
            if (GUILayout.Button("프리팹 모드로 열기")) OpenPrefabMode();
            return;
        }

        bool previewingThis = _previewTarget == Loading;
        float duration = Loading.PreviewDuration;
        EditorGUI.BeginChangeCheck();
        float time = EditorGUILayout.Slider("시간(초)", previewingThis ? Mathf.Min(_time, duration) : 0f, 0f, duration);
        if (EditorGUI.EndChangeCheck())
        {
            _playing = false;
            Seek(Loading, time);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(previewingThis && _playing ? "일시정지" : "재생")) TogglePlay(Loading);
            using (new EditorGUI.DisabledScope(!previewingThis))
            {
                if (GUILayout.Button("정지")) StopPreview();
            }
        }

        _loop = EditorGUILayout.Toggle("반복", _loop);
        _speedIndex = EditorGUILayout.Popup("속도", _speedIndex, SpeedLabels);

        if (!previewingThis) return;

        EditorGUILayout.HelpBox("미리보기는 다른 오브젝트를 선택해도 이어집니다. 배치를 고치려면 먼저 정지하세요.", MessageType.None);
        if (_playing) Repaint();
    }

    private void OpenPrefabMode()
    {
        var go = Loading.gameObject;
        var source = PrefabUtility.IsPartOfPrefabAsset(go) ? go : PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
        if (source != null) AssetDatabase.OpenAsset(source);
    }

    private static void TogglePlay(UIInitialLoading loading)
    {
        if (_previewTarget == loading && _playing)
        {
            _playing = false;
            return;
        }

        if (_previewTarget != loading || _time >= loading.PreviewDuration) Seek(loading, 0f);
        _playing = true;
        _lastTick = EditorApplication.timeSinceStartup;
    }

    private static void Tick()
    {
        if (_previewTarget == null)
        {
            StopPreview();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (!_playing)
        {
            // 일시정지 중에도 인스펙터에서 바꾼 커브·변형 값이 보이도록 가끔 다시 적용한다.
            if (now - _lastTick < PausedRefreshInterval) return;

            _lastTick = now;
            Seek(_previewTarget, _time);
            return;
        }

        float time = _time + (float)(now - _lastTick) * Speeds[_speedIndex];
        _lastTick = now;

        // 반복할 때는 끝 장면을 잠시 보여 준 뒤 처음으로 돌아간다.
        float duration = _previewTarget.PreviewDuration;
        if (_loop && time >= duration + LoopHold)
        {
            time = 0f;
        }
        else if (!_loop && time >= duration)
        {
            time = duration;
            _playing = false;
        }

        Seek(_previewTarget, time);
    }

    private static void Seek(UIInitialLoading loading, float time)
    {
        if (_previewTarget != loading)
        {
            StopPreview();
            loading.BeginPreview();
            _previewTarget = loading;
            Subscribe();
        }

        _time = time;
        loading.PreviewAt(time);
        SceneView.RepaintAll();
    }

    private static void StopPreview()
    {
        Unsubscribe();
        _playing = false;
        _time = 0f;
        _restoredForSave = false;

        var previewed = _previewTarget;
        _previewTarget = null;
        if (previewed == null) return;

        previewed.EndPreview();
        SceneView.RepaintAll();
    }

    private static void Subscribe()
    {
        Unsubscribe();
        EditorApplication.update += Tick;
        PrefabStage.prefabSaving += OnPrefabSaving;
        PrefabStage.prefabSaved += OnPrefabSaved;
        PrefabStage.prefabStageClosing += OnPrefabStageClosing;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
    }

    private static void Unsubscribe()
    {
        EditorApplication.update -= Tick;
        PrefabStage.prefabSaving -= OnPrefabSaving;
        PrefabStage.prefabSaved -= OnPrefabSaved;
        PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
    }

    private static void OnPrefabSaving(GameObject root)
    {
        if (_previewTarget == null) return;

        _previewTarget.EndPreview();
        _restoredForSave = true;
    }

    private static void OnPrefabSaved(GameObject root)
    {
        if (!_restoredForSave) return;

        _restoredForSave = false;
        if (_previewTarget == null) return;

        _previewTarget.BeginPreview();
        _previewTarget.PreviewAt(_time);
    }

    private static void OnPrefabStageClosing(PrefabStage stage) => StopPreview();

    private static void OnSceneSaving(Scene scene, string path) => StopPreview();

    private static void OnPlayModeStateChanged(PlayModeStateChange change) => StopPreview();
}
