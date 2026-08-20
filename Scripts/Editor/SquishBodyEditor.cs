#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SquishBody))]
public class SquishBodyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var body = (SquishBody)target;
        EditorGUILayout.LabelField("뼈 미리 생성", EditorStyles.boldLabel);
        if (CountBakedBones(body) < 2)
            EditorGUILayout.HelpBox(
                "구운 뼈가 없습니다. 아래 '🦴 뼈 굽기'로 프리팹에 뼈를 구우세요. " +
                "굽지 않으면 런타임에 SquishBody가 비활성화됩니다(런타임 생성 경로 없음).",
                MessageType.Warning);
        EditorGUILayout.HelpBox(
            "length/width/뼈 수 공식으로 물리 뼈 체인(Bone_0..N)을 프리팹에 구워 넣습니다. " +
            "구우면 런타임에 새로 만들지 않고 이 뼈들을 재사용합니다 → Fingertip 힌지를 특정 뼈에 직접 배선 가능. " +
            "length/width/뼈 수를 바꾸면 다시 구우세요(질량·중력·물성·굽힘은 런타임에 자동 반영). " +
            "프리팹 모드에서 실행하는 것을 권장합니다.",
            MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("🦴 뼈 굽기 / 갱신", GUILayout.Height(24)))
                SquishBoneBaker.Bake(body);
            if (GUILayout.Button("뼈 제거", GUILayout.Height(24), GUILayout.Width(80)))
                SquishBoneBaker.Clear(body);
        }
    }

    private static int CountBakedBones(SquishBody body)
    {
        int count = 0;
        foreach (Transform child in body.transform)
            if (child.GetComponent<SquishSegmentRelay>() != null) count++;
        return count;
    }

}
#endif
