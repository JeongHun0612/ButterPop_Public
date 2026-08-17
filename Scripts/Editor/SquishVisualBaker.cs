#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// 순수 Squish 프리팹의 비주얼(rest 메쉬 + 재질)을 에디터에서 미리 구워
// SquishVisual 자식 오브젝트에 배치하고 SquishMesh.visualRenderer에 배선한다.
// 목적: 프리팹에서 실제 렌더 결과를 보며 Transform으로 크기·배치를 조절할 수 있게 함.
// 런타임은 이 미리 배치된 렌더러/재질을 재사용한다(자동 생성·Shader.Find 제거).
public static class SquishVisualBaker
{
    private const string ChildName = "SquishVisual";
    private const string ShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";

    public static bool Bake(SquishBody body)
    {
        if (body == null) return false;

        var mesh = body.GetComponent<SquishMesh>();
        if (mesh == null)
        {
            Debug.LogError("[SquishVisualBaker] SquishMesh가 없습니다.");
            return false;
        }

        var skin = mesh.SkinSO;
        if (skin == null || skin.sprite == null)
        {
            Debug.LogError("[SquishVisualBaker] SquishMesh.skin 또는 스프라이트가 비어 있습니다.");
            return false;
        }

        var cp = body.ControlPoints;
        if (cp == null || cp.Length < 2)
        {
            Debug.LogError("[SquishVisualBaker] controlPoints가 2개 이상 필요합니다.");
            return false;
        }

        // 1) rest 메쉬 자산 (런타임과 동일한 지오메트리 생성기 사용 → 형태 일치 보장)
        var baked = SquishMesh.BuildPreviewMesh(cp, body.SmoothSpine, mesh.MeshCols, mesh.MeshRows,
            skin.sprite, skin.opaqueUVRect);
        var meshAsset = SaveOrUpdateMeshAsset(baked, skin);

        // 2) 재질 자산 (런타임 ApplyMaterial이 이 재질을 재사용)
        var mat = SaveOrUpdateMaterial(skin);

        // 3) SquishVisual 자식 + MeshFilter/MeshRenderer
        var visual = FindOrCreateChild(body.transform);
        var filter = GetOrAdd<MeshFilter>(visual);
        var renderer = GetOrAdd<MeshRenderer>(visual);
        filter.sharedMesh = meshAsset;
        renderer.sharedMaterial = mat;

        // 4) visualRenderer 배선
        var so = new SerializedObject(mesh);
        so.FindProperty("visualRenderer").objectReferenceValue = renderer;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(mesh);
        EditorUtility.SetDirty(visual);
        Debug.Log($"[SquishVisualBaker] 비주얼 굽기 완료: {AssetDatabase.GetAssetPath(meshAsset)}");
        return true;
    }

    private static string AssetDir(Object nearAsset)
    {
        string p = AssetDatabase.GetAssetPath(nearAsset);
        return string.IsNullOrEmpty(p) ? "Assets" : Path.GetDirectoryName(p).Replace('\\', '/');
    }

    private static Mesh SaveOrUpdateMeshAsset(Mesh newMesh, SquishSkinSO skin)
    {
        string path = $"{AssetDir(skin)}/{skin.name}_SquishMesh.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(newMesh, path);
            AssetDatabase.SaveAssets();
            return newMesh;
        }

        // 기존 자산 내용만 갱신 → MeshFilter 참조 유지.
        EditorUtility.CopySerialized(newMesh, existing);
        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssets();
        return existing;
    }

    private static Material SaveOrUpdateMaterial(SquishSkinSO skin)
    {
        string path = $"{AssetDir(skin)}/{skin.name}_SquishMat.mat";
        var shader = Shader.Find(ShaderName);
        if (shader == null) shader = Shader.Find("Sprites/Default");

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing == null)
        {
            var mat = new Material(shader) { name = $"{skin.name}_SquishMat", mainTexture = skin.sprite.texture };
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return mat;
        }

        existing.shader = shader;
        existing.mainTexture = skin.sprite.texture;
        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssets();
        return existing;
    }

    private static GameObject FindOrCreateChild(Transform parent)
    {
        var t = parent.Find(ChildName);
        if (t != null) return t.gameObject;

        var go = new GameObject(ChildName);
        Undo.RegisterCreatedObjectUndo(go, "Create SquishVisual");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }
}
#endif
