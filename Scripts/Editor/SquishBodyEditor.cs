#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SquishBody))]
public class SquishBodyEditor : Editor
{
    private int _autoTargetPoints = 6;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var body = (SquishBody)target;
        var mesh = body.GetComponent<SquishMesh>();
        Sprite sprite = (mesh != null && mesh.SkinSO != null) ? mesh.SkinSO.sprite : null;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("스파인 저작 도구", EditorStyles.boldLabel);

        if (sprite != null)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("스킨 스프라이트", sprite, typeof(Sprite), false);
        }
        else
        {
            EditorGUILayout.HelpBox("스프라이트를 찾을 수 없습니다. SquishMesh의 skin에 SquishSkinSO를 지정하세요.", MessageType.Warning);
        }

        // Auto 생성
        _autoTargetPoints = EditorGUILayout.IntSlider("Auto 목표 점 수", _autoTargetPoints, 2, 16);
        using (new EditorGUI.DisabledScope(sprite == null))
        {
            if (GUILayout.Button("⚡ 스프라이트에서 Auto 생성", GUILayout.Height(24)))
            {
                if (SquishSpineAutoGenerator.TryGenerate(sprite, _autoTargetPoints, out var points))
                {
                    var so = new SerializedObject(body);
                    var cpProp = so.FindProperty("controlPoints");
                    cpProp.arraySize = points.Length;
                    for (int i = 0; i < points.Length; i++)
                    {
                        var elem = cpProp.GetArrayElementAtIndex(i);
                        elem.FindPropertyRelative("position").vector2Value = points[i].position;
                        elem.FindPropertyRelative("halfWidth").floatValue = points[i].halfWidth;
                    }
                    so.ApplyModifiedProperties();

                    if (mesh != null && mesh.SkinSO != null)
                    {
                        mesh.SkinSO.opaqueUVRect = SquishSpriteUtil.ComputeOpaqueUVRect(sprite);
                        EditorUtility.SetDirty(mesh.SkinSO);
                    }
                }
            }
        }

        // Shape Maker 열기
        if (GUILayout.Button("Shape Maker에서 편집", GUILayout.Height(24)))
            SquishShapeMakerWindow.Open(body);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("비주얼 미리 배치", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "rest 메쉬 + 재질을 자산으로 구워 SquishVisual 자식에 배치하고 SquishMesh.visualRenderer에 배선합니다. " +
            "프리팹에서 실제 렌더 결과를 보며 SquishVisual의 Transform으로 크기·배치를 조절하세요. " +
            "컨트롤 포인트나 스프라이트를 바꾸면 다시 누르세요.",
            MessageType.Info);
        using (new EditorGUI.DisabledScope(sprite == null))
        {
            if (GUILayout.Button("🧈 비주얼 굽기 / 갱신", GUILayout.Height(24)))
                SquishVisualBaker.Bake(body);
        }
    }
}
#endif
