#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// SquishSkinSO 인스펙터 — 기본 필드 + 스프라이트 불투명영역(opaqueUVRect) 자동계산 버튼.
[CustomEditor(typeof(SquishSkinSO))]
public class SquishSkinSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var skin = (SquishSkinSO)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("불투명 영역 (UV 정합)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("opaqueUVRect", skin.opaqueUVRect.ToString(), EditorStyles.miniLabel);

        using (new EditorGUI.DisabledScope(skin.sprite == null))
            if (GUILayout.Button("불투명영역 자동계산 (스프라이트에서)"))
            {
                Undo.RecordObject(skin, "Compute Opaque UV Rect");
                skin.opaqueUVRect = SquishSpriteUtil.ComputeOpaqueUVRect(skin.sprite);
                EditorUtility.SetDirty(skin);
                Debug.Log($"[SquishSkin] opaqueUVRect = {skin.opaqueUVRect}");
            }
    }
}
#endif
