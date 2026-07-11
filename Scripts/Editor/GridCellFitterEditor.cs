using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(GridCellFitter))]
public class GridCellFitterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();

        if (EditorGUI.EndChangeCheck() || GUILayout.Button("Fit Cell Size"))
        {
            var fitter = (GridCellFitter)target;
            fitter.FitCellSize();
            EditorUtility.SetDirty(fitter);
        }
    }
}

[CustomEditor(typeof(GridLayoutGroup), true)]
public class GridLayoutGroupFitterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();

        if (EditorGUI.EndChangeCheck())
        {
            var fitter = ((Component)target).GetComponent<GridCellFitter>();
            if (fitter != null)
            {
                fitter.FitCellSize();
                EditorUtility.SetDirty(fitter);
            }
        }
    }
}
