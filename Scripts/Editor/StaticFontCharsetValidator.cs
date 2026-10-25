using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// 빌드 직전에 스트링 테이블·프리팹 글자 중 Static 메인 폰트에 없는 글자를 찾아 경고한다.
// 없는 글자는 Dynamic 폴백이 런타임에 그리므로 빌드를 막지 않는다.
public class StaticFontCharsetValidator : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        var mainFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(StaticFontCharsetBuilder.MainFontPath);
        if (mainFont == null)
        {
            Debug.LogError($"[StaticFontCharsetValidator] 메인 폰트를 찾지 못했습니다: {StaticFontCharsetBuilder.MainFontPath}");
            return;
        }

        if (mainFont.atlasPopulationMode != AtlasPopulationMode.Static)
        {
            Debug.LogWarning($"[StaticFontCharsetValidator] {mainFont.name}이 Static이 아닙니다. 'ButterPop/폰트/Static 폰트 글자셋 재생성'을 실행하세요.");
            return;
        }

        var missing = StaticFontCharsetBuilder.CollectCodepoints(mainFont)
            .Where(c => !mainFont.HasCharacter((int)c))
            .OrderBy(c => c)
            .ToArray();
        if (missing.Length == 0) return;

        Debug.LogWarning($"[StaticFontCharsetValidator] {mainFont.name}에 없는 글자 {missing.Length}개는 폴백 폰트로 그려집니다: {StaticFontCharsetBuilder.ToText(missing)}. " +
                         "'ButterPop/폰트/Static 폰트 글자셋 재생성'을 실행하세요.");
    }
}
