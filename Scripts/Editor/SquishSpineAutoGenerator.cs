#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 스프라이트 불투명 영역에서 스파인 제어점을 자동 생성하는 유틸리티.
// SquishBodyEditor(인스펙터)와 SquishShapeMakerWindow(캔버스)에서 공용으로 사용한다.
public static class SquishSpineAutoGenerator
{
    private const byte AlphaThreshold = 25;

    public static bool TryGenerate(Sprite sprite, int targetPoints, out SpinePoint[] result)
    {
        result = null;
        if (sprite == null) return false;

        if (!TryBuildMedial(sprite, out var medial, out var half))
        {
            Debug.LogWarning("[Squish] Auto 생성 실패: 불투명 픽셀을 찾지 못했습니다.");
            return false;
        }

        SmoothHalf(half, 2);

        float ppu = Mathf.Max(0.0001f, sprite.pixelsPerUnit);
        float axisLen = Vector2.Distance(medial[0], medial[medial.Count - 1]);
        float epsilon = Mathf.Max(1f / ppu, axisLen * 0.0075f);

        var keep = DecimateByCurvature(medial, targetPoints, epsilon);

        result = new SpinePoint[keep.Count];
        for (int i = 0; i < keep.Count; i++)
        {
            result[i] = new SpinePoint
            {
                position = medial[keep[i]],
                halfWidth = Mathf.Max(0.02f, half[keep[i]])
            };
        }

        Debug.Log($"[Squish] Auto 생성 완료: 중심선 {medial.Count}칼럼 → 제어점 {result.Length}개 (목표 {targetPoints}).");
        return true;
    }

    // 스프라이트 불투명 영역을 스캔해 긴 축을 따라 중심선(medial) 점 + 국소 반폭을 sprite-local로 뽑는다.
    internal static bool TryBuildMedial(Sprite sprite, out List<Vector2> medial, out List<float> half)
    {
        medial = new List<Vector2>();
        half = new List<float>();

        string path = AssetDatabase.GetAssetPath(sprite.texture);
        Texture2D readable = null;
        Color32[] px;
        int texW, texH;
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            readable = new Texture2D(2, 2);
            if (!readable.LoadImage(bytes)) { Object.DestroyImmediate(readable); return false; }
            px = readable.GetPixels32();
            texW = readable.width;
            texH = readable.height;
        }
        catch { if (readable != null) Object.DestroyImmediate(readable); return false; }
        Object.DestroyImmediate(readable);

        Rect r = sprite.rect;
        int rx = Mathf.RoundToInt(r.x), ry = Mathf.RoundToInt(r.y);
        int rw = Mathf.RoundToInt(r.width), rh = Mathf.RoundToInt(r.height);
        if (rw <= 1 || rh <= 1 || rx < 0 || ry < 0 || rx + rw > texW || ry + rh > texH) return false;

        var mask = new bool[rw * rh];
        int minX = rw, maxX = -1, minY = rh, maxY = -1;
        for (int y = 0; y < rh; y++)
            for (int x = 0; x < rw; x++)
            {
                bool op = px[(ry + y) * texW + (rx + x)].a > AlphaThreshold;
                mask[y * rw + x] = op;
                if (op)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        if (maxX < 0) return false;

        bool horizontal = (maxX - minX) >= (maxY - minY);
        float ppu = Mathf.Max(0.0001f, sprite.pixelsPerUnit);
        Vector2 pivot = sprite.pivot;

        if (horizontal)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int lo = -1, hi = -1;
                for (int y = minY; y <= maxY; y++)
                    if (mask[y * rw + x]) { if (lo < 0) lo = y; hi = y; }
                if (lo < 0) continue;
                medial.Add(new Vector2((x + 0.5f - pivot.x) / ppu, ((lo + hi) * 0.5f + 0.5f - pivot.y) / ppu));
                half.Add((hi - lo + 1) * 0.5f / ppu);
            }
        }
        else
        {
            for (int y = minY; y <= maxY; y++)
            {
                int lo = -1, hi = -1;
                for (int x = minX; x <= maxX; x++)
                    if (mask[y * rw + x]) { if (lo < 0) lo = x; hi = x; }
                if (lo < 0) continue;
                medial.Add(new Vector2(((lo + hi) * 0.5f + 0.5f - pivot.x) / ppu, (y + 0.5f - pivot.y) / ppu));
                half.Add((hi - lo + 1) * 0.5f / ppu);
            }
        }

        return medial.Count >= 2;
    }

    internal static void SmoothHalf(List<float> half, int window)
    {
        if (half.Count < 3 || window < 1) return;
        var src = new List<float>(half);
        for (int i = 0; i < half.Count; i++)
        {
            float sum = 0f; int cnt = 0;
            for (int k = -window; k <= window; k++)
            {
                int j = i + k;
                if (j >= 0 && j < src.Count) { sum += src[j]; cnt++; }
            }
            half[i] = sum / cnt;
        }
    }

    // 우선순위 Douglas-Peucker: 편차가 가장 큰 점부터 targetCount개까지 유지.
    internal static List<int> DecimateByCurvature(IReadOnlyList<Vector2> pts, int targetCount, float epsilon)
    {
        int n = pts.Count;
        var kept = new List<int> { 0, n - 1 };
        targetCount = Mathf.Clamp(targetCount, 2, n);
        if (n <= 2 || targetCount <= 2) return kept;

        var segments = new List<(int a, int b)> { (0, n - 1) };
        while (kept.Count < targetCount)
        {
            float best = -1f; int bestSeg = -1, bestPt = -1;
            for (int s = 0; s < segments.Count; s++)
            {
                var (a, b) = segments[s];
                if (b - a < 2) continue;
                MaxDeviation(pts, a, b, out int mi, out float md);
                if (md > best) { best = md; bestSeg = s; bestPt = mi; }
            }
            if (bestSeg < 0 || best < epsilon) break;

            var seg = segments[bestSeg];
            segments.RemoveAt(bestSeg);
            segments.Add((seg.a, bestPt));
            segments.Add((bestPt, seg.b));
            kept.Add(bestPt);
        }

        kept.Sort();
        return kept;
    }

    private static void MaxDeviation(IReadOnlyList<Vector2> pts, int a, int b, out int idx, out float dist)
    {
        idx = -1; dist = -1f;
        Vector2 pa = pts[a], pb = pts[b];
        Vector2 ab = pb - pa;
        float abLen = ab.magnitude;
        for (int i = a + 1; i < b; i++)
        {
            float d = abLen < 1e-6f
                ? Vector2.Distance(pts[i], pa)
                : Mathf.Abs((pts[i].x - pa.x) * ab.y - (pts[i].y - pa.y) * ab.x) / abLen;
            if (d > dist) { dist = d; idx = i; }
        }
    }
}
#endif
