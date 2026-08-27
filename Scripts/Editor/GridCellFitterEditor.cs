using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GridCellFitter))]
public class GridCellFitterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool changed = EditorGUI.EndChangeCheck();

        // 두 조건을 ||로 묶으면 값이 바뀐 프레임에 버튼이 그려지지 않아
        // Layout/Repaint 간 컨트롤 수가 어긋난다. 각각 따로 평가한다.
        bool pressed = GUILayout.Button("Fit Cell Size");

        if (!changed && !pressed) return;

        var fitter = (GridCellFitter)target;
        fitter.FitCellSize();
        EditorUtility.SetDirty(fitter);
    }
}
