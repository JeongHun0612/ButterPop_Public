using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SquishBody))]
[CanEditMultipleObjects]
public class SquishBodyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
            {
                string label = GetLabel(property.propertyPath);
                if (label == null)
                    EditorGUILayout.PropertyField(property, true);
                else
                    EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
            }

            DrawHelp(property.propertyPath);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static string GetLabel(string propertyPath) => propertyPath switch
    {
        "totalMass" => "질량 (Total Mass)",
        "gravityScale" => "중력 (Gravity Scale)",
        "linearDamping" => "이동 감쇠 (Linear Damping)",
        "angularDamping" => "회전 감쇠 (Angular Damping)",
        "bounciness" => "반발력 (Bounciness)",
        "friction" => "마찰 (Friction)",
        "bendStiffness" => "굽힘 강성 (Bend Stiffness)",
        "bendDamping" => "굽힘 감쇠 (Bend Damping)",
        "maxBendAnglePerJoint" => "관절 각도 한도 (Max Bend Angle Per Joint)",
        "grabbedBoneCount" => "고정 뼈 수 (Grabbed Bone Count)",
        "headSwingLimit" => "머리 회전 한도 (Head Swing Limit)",
        _ => null
    };

    private static void DrawHelp(string propertyPath)
    {
        string help = propertyPath switch
        {
            "friction" =>
                "더 무겁게: 질량 ↑\n"
                + "더 강하게 떨어지게: 중력 ↑\n"
                + "이동을 빨리 멈추게: 이동 감쇠 ↑\n"
                + "회전을 빨리 멈추게: 회전 감쇠 ↑\n"
                + "더 잘 튀게: 반발력 ↑\n"
                + "덜 미끄러지게: 마찰 ↑\n"
                + "플레이 시작 시 적용",

            "maxBendAnglePerJoint" =>
                "더 강하게 펴지게: 굽힘 강성 ↑\n"
                + "굽힘이 덜 출렁이게: 굽힘 감쇠 ↑\n"
                + "더 깊게 접히게: 관절 각도 한도 ↑\n"
                + "강성·감쇠는 실시간, 각도 한도는 플레이 시작 시 적용",

            _ => null
        };

        if (help != null)
            EditorGUILayout.HelpBox(help, MessageType.None);
    }
}
