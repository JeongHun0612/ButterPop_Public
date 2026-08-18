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
        var mesh = body.GetComponent<SquishMesh>();
        Sprite sprite = (mesh != null && mesh.SkinSO != null) ? mesh.SkinSO.sprite : null;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("비주얼 미리 배치", EditorStyles.boldLabel);

        if (sprite == null)
            EditorGUILayout.HelpBox("스프라이트를 찾을 수 없습니다. SquishMesh의 skin에 SquishSkinSO를 지정하세요.", MessageType.Warning);

        EditorGUILayout.HelpBox(
            "rest 메쉬 + 재질을 자산으로 구워 SquishVisual 자식에 배치하고 SquishMesh.visualRenderer에 배선합니다. " +
            "프리팹에서 실제 렌더 결과를 보며 SquishVisual의 Transform으로 배치를 조절하세요. " +
            "length/width나 스프라이트를 바꾸면 다시 누르세요.",
            MessageType.Info);

        // 구운 메쉬 크기 vs 현재 length×width 비교 → 어긋나면 재굽기 경고(자동 프리뷰 제거 후 유일한 안전망).
        WarnIfVisualStale(body, mesh);

        using (new EditorGUI.DisabledScope(sprite == null))
        {
            if (GUILayout.Button("🧈 비주얼 굽기 / 갱신", GUILayout.Height(24)))
                SquishVisualBaker.Bake(body);
        }

        EditorGUILayout.Space(8);
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

    // 구운 비주얼 메쉬 bounds는 length×width와 같아야 한다(정점이 [-L/2,L/2]×[-W/2,W/2]를 span).
    // length/width를 바꾸고 다시 굽지 않으면 여기서 어긋나므로 경고한다.
    private static void WarnIfVisualStale(SquishBody body, SquishMesh mesh)
    {
        if (body.Length <= 0f || body.Width <= 0f) return;

        var vr = mesh != null ? mesh.VisualRenderer : null;
        var mf = vr != null ? vr.GetComponent<MeshFilter>() : null;
        var baked = mf != null ? mf.sharedMesh : null;
        if (baked == null) return;   // 아직 안 구움 → 위 안내로 충분

        Vector3 size = baked.bounds.size;
        bool stale = Mathf.Abs(size.x - body.Length) > body.Length * 0.02f
                  || Mathf.Abs(size.y - body.Width) > body.Width * 0.02f;
        if (stale)
            EditorGUILayout.HelpBox(
                $"구운 메쉬 크기({size.x:0.##}×{size.y:0.##})가 현재 length×width({body.Length:0.##}×{body.Width:0.##})와 다릅니다. " +
                "'비주얼 굽기'를 다시 누르세요.",
                MessageType.Warning);
    }
}
#endif
