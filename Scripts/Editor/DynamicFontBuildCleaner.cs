using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// Clear Dynamic Data On Build가 켜진 동적 폰트 에셋을 Addressables 번들 빌드보다 먼저 비운다.
// TMP 자체 정리는 IPreprocessBuildWithReport에서 돌아 이미 만들어진 번들에는 에디터에서 쌓인 글리프가 그대로 들어간다.
public class DynamicFontBuildCleaner : BuildPlayerProcessor
{
    // AddressablesPlayerBuildProcessor(1)보다 먼저 실행한다.
    public override int callbackOrder => 0;

    public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (fontAsset == null || fontAsset.atlasPopulationMode == AtlasPopulationMode.Static) continue;
            if (!new SerializedObject(fontAsset).FindProperty("m_ClearDynamicDataOnBuild").boolValue) continue;
            if (fontAsset.atlasTexture != null && fontAsset.atlasTexture.width <= 1) continue;

            fontAsset.ClearFontAssetData(true);
            Debug.Log($"[DynamicFontBuildCleaner] {fontAsset.name} 동적 데이터를 비웠습니다");
        }
    }
}
