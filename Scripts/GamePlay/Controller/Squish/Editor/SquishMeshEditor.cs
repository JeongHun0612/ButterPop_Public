using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SquishMesh))]
[CanEditMultipleObjects]
public class SquishMeshEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        DrawPropertiesExcluding(serializedObject,
            "m_Script", "surfaceSubdivisions", "squishEnabled", "softness", "recoveryTime", "rebound", "impactStrength");

        EditorGUILayout.PropertyField(serializedObject.FindProperty("surfaceSubdivisions"));
        EditorGUILayout.HelpBox("뼈 구간 사이의 메시를 나누는 수입니다.\n높일수록 눌림 윤곽이 매끄러워지고 계산량이 늘어납니다.\n물리 뼈·충돌체 수는 유지되며, 플레이 시작 시 적용됩니다.", MessageType.None);

        EditorGUILayout.Space();
        Draw("squishEnabled", "스퀴시 사용");
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!serializedObject.FindProperty("squishEnabled").boolValue))
        {
            Draw("softness", "말랑함");
            Draw("recoveryTime", "복원 시간 (초)");
            Draw("rebound", "잔반동");
            Draw("impactStrength", "충돌 반응");
        }

        EditorGUILayout.HelpBox("더 잘 눌리게: 말랑함 ↑\n빨리 돌아오게: 복원 시간 ↓\n덜 출렁이게: 잔반동 ↓\n같은 충돌에 더 크게 눌리게: 충돌 반응 ↑ (0: 반응 없음, 1: 기본)", MessageType.None);

        serializedObject.ApplyModifiedProperties();
    }

    private void Draw(string propertyName, string label)
    {
        var property = serializedObject.FindProperty(propertyName);
        EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
    }
}
