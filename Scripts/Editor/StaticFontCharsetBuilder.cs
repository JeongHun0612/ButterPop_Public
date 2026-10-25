using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

// 스트링 테이블과 프리팹에서 쓰는 글자로 메인 폰트의 Static 아틀라스를 다시 만들고,
// 메인 폰트에 없는 글자(인앱결제 가격의 통화 기호 등)를 런타임에 채울 Dynamic 폴백을 연결한다.
// 씬에 직접 둔 텍스트는 수집하지 않는다.
public static class StaticFontCharsetBuilder
{
    public const string MainFontPath = "Assets/07. Fonts/강원교육튼튼 SDF.asset";
    private const string FallbackFontPath = "Assets/07. Fonts/강원교육튼튼 SDF Fallback.asset";
    private const string CharsetPath = "Assets/07. Fonts/강원교육튼튼 Charset.txt";
    private const string PrefabFolder = "Assets/03. Prefabs";
    private const int FallbackAtlasSize = 512;
    // 런타임에 조합되는 숫자·포맷 문자열에 쓰일 수 있어 항상 넣는 기호
    private const string AlwaysIncludedSymbols = "…·×÷±°₩€£¥←→↑↓★☆♥♡";

    [MenuItem("ButterPop/폰트/Static 폰트 글자셋 재생성")]
    public static void Rebuild()
    {
        var mainFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MainFontPath);
        if (mainFont == null)
        {
            Debug.LogError($"[StaticFontCharsetBuilder] 메인 폰트를 찾지 못했습니다: {MainFontPath}");
            return;
        }

        var sourceFont = GetSourceFont(mainFont);
        if (sourceFont == null)
        {
            Debug.LogError($"[StaticFontCharsetBuilder] {mainFont.name}의 원본 폰트 파일을 찾지 못했습니다");
            return;
        }

        var fallbackFont = GetOrCreateFallback(sourceFont);
        if (fallbackFont == null) return;

        if (!mainFont.fallbackFontAssetTable.Contains(fallbackFont))
            mainFont.fallbackFontAssetTable.Insert(0, fallbackFont);

        var codepoints = CollectCodepoints(mainFont).OrderBy(c => c).ToArray();
        RebuildStaticAtlas(mainFont, sourceFont, codepoints, out var missing);
        WriteCharsetFile(codepoints);

        AssetDatabase.SaveAssets();

        Debug.Log($"[StaticFontCharsetBuilder] {mainFont.name}: 글자 {codepoints.Length}개, 아틀라스 {mainFont.atlasTextures.Length}장으로 다시 만들었습니다");
        if (missing.Length > 0)
            Debug.LogWarning($"[StaticFontCharsetBuilder] 원본 폰트에 없어 넣지 못한 글자 {missing.Length}개: {ToText(missing)}");
    }

    public static HashSet<uint> CollectCodepoints(TMP_FontAsset mainFont)
    {
        var codepoints = new HashSet<uint>();
        for (uint c = 0x20; c <= 0x7E; c++)
            codepoints.Add(c);
        AddCodepoints(codepoints, AlwaysIncludedSymbols);

        foreach (var collection in LocalizationEditorSettings.GetStringTableCollections())
        foreach (var table in collection.StringTables)
        foreach (var entry in table.Values)
            AddCodepoints(codepoints, entry.Value);

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null) continue;

            foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font == mainFont)
                    AddCodepoints(codepoints, text.text);
            }
        }

        return codepoints;
    }

    public static string ToText(IEnumerable<uint> codepoints) =>
        string.Concat(codepoints.Select(c => char.ConvertFromUtf32((int)c)));

    private static Font GetSourceFont(TMP_FontAsset fontAsset)
    {
        string guid = new SerializedObject(fontAsset).FindProperty("m_SourceFontFileGUID").stringValue;
        return AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guid));
    }

    // 폴백은 메인 폰트 에셋을 복제해 렌더 설정·머티리얼을 그대로 쓰고, 빌드마다 비워지는 작은 Dynamic 아틀라스로 둔다.
    private static TMP_FontAsset GetOrCreateFallback(Font sourceFont)
    {
        var fallbackFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);
        if (fallbackFont != null) return fallbackFont;

        if (!AssetDatabase.CopyAsset(MainFontPath, FallbackFontPath))
        {
            Debug.LogError($"[StaticFontCharsetBuilder] 폴백 폰트를 만들지 못했습니다: {FallbackFontPath}");
            return null;
        }

        fallbackFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);
        SetDynamic(fallbackFont, sourceFont);

        var serialized = new SerializedObject(fallbackFont);
        serialized.FindProperty("m_AtlasWidth").intValue = FallbackAtlasSize;
        serialized.FindProperty("m_AtlasHeight").intValue = FallbackAtlasSize;
        serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
        serialized.FindProperty("m_FallbackFontAssetTable").ClearArray();
        serialized.ApplyModifiedPropertiesWithoutUndo();

        fallbackFont.ClearFontAssetData(true);
        fallbackFont.atlasTexture.name = $"{fallbackFont.name} Atlas";
        fallbackFont.material.name = $"{fallbackFont.atlasTexture.name} Material";
        fallbackFont.material.SetFloat(ShaderUtilities.ID_TextureWidth, FallbackAtlasSize);
        fallbackFont.material.SetFloat(ShaderUtilities.ID_TextureHeight, FallbackAtlasSize);
        EditorUtility.SetDirty(fallbackFont);

        Debug.Log($"[StaticFontCharsetBuilder] 폴백 폰트를 만들었습니다: {FallbackFontPath}");
        return fallbackFont;
    }

    private static void RebuildStaticAtlas(TMP_FontAsset fontAsset, Font sourceFont, uint[] codepoints, out uint[] missing)
    {
        SetDynamic(fontAsset, sourceFont);
        fontAsset.ClearFontAssetData(true);
        fontAsset.TryAddCharacters(codepoints, out missing, true);
        missing ??= new uint[0];

        // Static으로 바꾸면 원본 폰트 참조가 빠져 TTF가 빌드에 포함되지 않는다.
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

        // Static 아틀라스는 다시 쓰지 않으므로 CPU 사본을 남기지 않게 Readable을 끈다.
        foreach (var texture in fontAsset.atlasTextures)
        {
            if (texture == null) continue;

            texture.Apply(false, false);
            var serializedTexture = new SerializedObject(texture);
            serializedTexture.FindProperty("m_IsReadable").boolValue = false;
            serializedTexture.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(texture);
        }

        EditorUtility.SetDirty(fontAsset);
    }

    private static void SetDynamic(TMP_FontAsset fontAsset, Font sourceFont)
    {
        var serialized = new SerializedObject(fontAsset);
        serialized.FindProperty("m_AtlasPopulationMode").intValue = (int)AtlasPopulationMode.Dynamic;
        serialized.FindProperty("m_SourceFontFile").objectReferenceValue = sourceFont;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WriteCharsetFile(uint[] codepoints)
    {
        File.WriteAllText(CharsetPath, ToText(codepoints), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(CharsetPath);
    }

    private static void AddCodepoints(HashSet<uint> codepoints, string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        for (int i = 0; i < text.Length; i++)
        {
            int codepoint = text[i];
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                codepoint = char.ConvertToUtf32(text[i], text[++i]);
            else if (char.IsSurrogate(text[i]))
                continue;

            if (codepoint < 0x20) continue;
            codepoints.Add((uint)codepoint);
        }
    }
}
