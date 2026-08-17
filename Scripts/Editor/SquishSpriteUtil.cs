#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// 스프라이트 픽셀에서 불투명(아트) 영역을 계산하는 에디터 공용 유틸.
// SquishShapeMakerWindow · SquishSkinSOEditor가 공유한다.
public static class SquishSpriteUtil
{
    public const byte AlphaThreshold = 25;   // 불투명 판정 알파

    // 스프라이트 텍스처를 파일에서 읽어(임포트 설정 무관) sprite.rect 내 불투명 영역을 0~1로 계산.
    public static Rect ComputeOpaqueUVRect(Sprite sprite)
    {
        var full = new Rect(0f, 0f, 1f, 1f);
        if (sprite == null) return full;

        string path = AssetDatabase.GetAssetPath(sprite.texture);
        Texture2D readable = null;
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            readable = new Texture2D(2, 2);
            if (!readable.LoadImage(bytes)) { Object.DestroyImmediate(readable); return full; }
        }
        catch { if (readable != null) Object.DestroyImmediate(readable); return full; }

        Rect r = sprite.rect;
        int rx = Mathf.RoundToInt(r.x), ry = Mathf.RoundToInt(r.y);
        int rw = Mathf.RoundToInt(r.width), rh = Mathf.RoundToInt(r.height);
        if (rx < 0 || ry < 0 || rx + rw > readable.width || ry + rh > readable.height)
        {
            Object.DestroyImmediate(readable);
            return full;
        }

        Color32[] px = readable.GetPixels32();
        int texW = readable.width;
        int minX = rw, minY = rh, maxX = -1, maxY = -1;
        for (int y = 0; y < rh; y++)
            for (int x = 0; x < rw; x++)
                if (px[(ry + y) * texW + (rx + x)].a > AlphaThreshold)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }

        Object.DestroyImmediate(readable);
        if (maxX < 0) return full;

        return new Rect(minX / (float)rw, minY / (float)rh, (maxX - minX + 1) / (float)rw, (maxY - minY + 1) / (float)rh);
    }
}
#endif
