using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// 스테이지 오브젝트 프리팹의 PolygonCollider2D와 스프라이트 Custom Physics Shape를 서로 맞춘다.
// Sprite Editor는 저장된 Custom Physics Shape만 보여주고, Apply해도 프리팹 콜라이더는 갱신되지 않기 때문이다.
public static class StageColliderShapeSync
{
    private const string MenuRoot = "Assets/ButterPop/";
    private const string ColliderToShapeMenu = MenuRoot + "콜라이더 → Physics Shape 저장";
    private const string ShapeToColliderMenu = MenuRoot + "Physics Shape → 콜라이더 반영";

    private class ShapeWrite
    {
        public Sprite sprite;
        public List<Vector2[]> outlines;
    }

    [MenuItem(ColliderToShapeMenu, true)]
    [MenuItem(ShapeToColliderMenu, true)]
    private static bool ValidateSelection() => GetSelectedPrefabPaths().Any();

    // 프리팹 콜라이더 모양을 스프라이트 Custom Physics Shape로 저장해 Sprite Editor에서 보고 고칠 수 있게 한다.
    [MenuItem(ColliderToShapeMenu)]
    private static void SaveColliderToPhysicsShape()
    {
        var writes = new List<ShapeWrite>();
        foreach (string prefabPath in GetSelectedPrefabPaths())
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            foreach (var collider in prefab.GetComponentsInChildren<PolygonCollider2D>(true))
            {
                var sprite = GetColliderSprite(collider, prefabPath);
                if (sprite == null) continue;
                if (writes.Any(w => w.sprite == sprite)) continue;

                writes.Add(new ShapeWrite { sprite = sprite, outlines = ToOutlines(collider, sprite) });
            }
        }

        if (writes.Count == 0) return;

        var overwritten = writes.Where(w => HasCustomShape(w.sprite)).ToList();
        if (overwritten.Count > 0 && !EditorUtility.DisplayDialog(
                "Physics Shape 덮어쓰기",
                $"이미 Custom Physics Shape가 있는 스프라이트 {overwritten.Count}개를 콜라이더 모양으로 덮어씁니다.\n{string.Join("\n", overwritten.Select(w => w.sprite.name))}",
                "덮어쓰기", "취소"))
            return;

        foreach (var group in writes.GroupBy(w => AssetDatabase.GetAssetPath(w.sprite)))
            WriteOutlines(group.Key, group.ToList());

        Debug.Log($"[StageColliderShapeSync] Physics Shape 저장 완료: {string.Join(", ", writes.Select(w => w.sprite.name))}");
    }

    // 스프라이트 Physics Shape(Custom 또는 자동 생성)로 프리팹 PolygonCollider2D 경로를 다시 채운다.
    [MenuItem(ShapeToColliderMenu)]
    private static void ApplyPhysicsShapeToCollider()
    {
        var updated = new List<string>();
        foreach (string prefabPath in GetSelectedPrefabPaths())
        {
            using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
            foreach (var collider in scope.prefabContentsRoot.GetComponentsInChildren<PolygonCollider2D>(true))
            {
                var sprite = GetColliderSprite(collider, prefabPath);
                if (sprite == null) continue;

                int count = sprite.GetPhysicsShapeCount();
                if (count == 0)
                {
                    Debug.LogWarning($"[StageColliderShapeSync] {sprite.name}에 Physics Shape가 없습니다. 임포트 설정의 Generate Physics Shape를 확인하세요.");
                    continue;
                }

                var points = new List<Vector2>();
                collider.offset = Vector2.zero;
                collider.pathCount = count;
                for (int i = 0; i < count; i++)
                {
                    sprite.GetPhysicsShape(i, points);
                    collider.SetPath(i, points);
                }

                updated.Add($"{collider.name} ({count}개 경로, {collider.GetTotalPointCount()}개 꼭짓점)");
            }
        }

        if (updated.Count > 0)
            Debug.Log($"[StageColliderShapeSync] 콜라이더 반영 완료:\n{string.Join("\n", updated)}");
    }

    private static IEnumerable<string> GetSelectedPrefabPaths()
        => Selection.GetFiltered<GameObject>(SelectionMode.Assets)
            .Select(AssetDatabase.GetAssetPath)
            .Where(path => path.EndsWith(".prefab"));

    private static Sprite GetColliderSprite(PolygonCollider2D collider, string prefabPath)
    {
        var renderer = collider.GetComponent<SpriteRenderer>();
        if (renderer != null && renderer.sprite != null) return renderer.sprite;

        Debug.LogWarning($"[StageColliderShapeSync] 같은 오브젝트에 스프라이트가 없어 건너뜁니다: {prefabPath} / {collider.name}");
        return null;
    }

    // 콜라이더 로컬 좌표를 Physics Shape 좌표(스프라이트 Rect 중심 기준 픽셀)로 바꾼다.
    private static List<Vector2[]> ToOutlines(PolygonCollider2D collider, Sprite sprite)
    {
        Vector2 toCenter = sprite.pivot - sprite.rect.size * 0.5f;
        var outlines = new List<Vector2[]>();
        for (int i = 0; i < collider.pathCount; i++)
        {
            outlines.Add(collider.GetPath(i)
                .Select(p => (p + collider.offset) * sprite.pixelsPerUnit + toCenter)
                .ToArray());
        }
        return outlines;
    }

    private static bool HasCustomShape(Sprite sprite)
    {
        var provider = CreateDataProvider(AssetDatabase.GetAssetPath(sprite));
        var rect = FindSpriteRect(provider, sprite);
        return rect != null && provider.GetDataProvider<ISpritePhysicsOutlineDataProvider>().GetOutlines(rect.spriteID).Count > 0;
    }

    private static void WriteOutlines(string texturePath, List<ShapeWrite> writes)
    {
        var provider = CreateDataProvider(texturePath);
        if (provider == null)
        {
            Debug.LogError($"[StageColliderShapeSync] 스프라이트 데이터를 열지 못했습니다: {texturePath}");
            return;
        }

        var physics = provider.GetDataProvider<ISpritePhysicsOutlineDataProvider>();
        foreach (var write in writes)
        {
            var rect = FindSpriteRect(provider, write.sprite);
            if (rect == null)
            {
                Debug.LogError($"[StageColliderShapeSync] SpriteRect를 찾지 못했습니다: {write.sprite.name}");
                continue;
            }
            physics.SetOutlines(rect.spriteID, write.outlines);
        }

        provider.Apply();
        (provider.targetObject as AssetImporter)?.SaveAndReimport();
    }

    private static ISpriteEditorDataProvider CreateDataProvider(string texturePath)
    {
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(AssetImporter.GetAtPath(texturePath));
        provider?.InitSpriteEditorDataProvider();
        return provider;
    }

    private static SpriteRect FindSpriteRect(ISpriteEditorDataProvider provider, Sprite sprite)
        => provider?.GetSpriteRects().FirstOrDefault(r => r.name == sprite.name);
}
