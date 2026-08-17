using System.Collections.Generic;
using UnityEngine;

// 스파인(제어점 폴리라인)을 호 길이 기준으로 균등 리샘플링한다.
// - 물리 뼈(boneCount)와 비주얼 격자 열(meshCols)을 같은 스파인에서 각각 다른 해상도로 뽑는 데 쓴다.
// - 둘 다 "정규화 인덱스 = 호 길이 비율"이 되므로, 뼈와 정점을 arc 파라미터로 매칭해 스키닝할 수 있다.
public struct SpineSample
{
    public Vector2 position;
    public float angleDeg;   // 접선 방향 (뼈의 rest 회전)
    public float halfWidth;
}

public static class SpineSampler
{
    private const int SubdivPerSegment = 16;   // Catmull-Rom 세분화 밀도

    // control 제어점을 count개로 호 길이 균등 리샘플링. count>=2.
    // startInset/endInset: 양 끝에서 호 길이만큼 안으로 당겨 샘플링(월드 단위).
    //   물리 뼈는 끝단 반폭만큼 인셋해 원형 캡이 비주얼 밖으로 튀어나오지 않게 한다(캡슐 모델).
    //   비주얼 격자는 인셋 0으로 스파인 전체를 덮는다.
    public static SpineSample[] Resample(SquishShapeSO.SpinePoint[] control, int count, bool smooth,
        float startInset = 0f, float endInset = 0f)
    {
        count = Mathf.Max(count, 2);

        if (control == null || control.Length < 2)
            return Fallback(count);

        // 1) 미세 폴리라인 생성 (Catmull-Rom 또는 직선).
        BuildFineLine(control, smooth, out var pts, out var widths);

        // 2) 누적 호 길이.
        int fn = pts.Count;
        var cum = new float[fn];
        cum[0] = 0f;
        for (int i = 1; i < fn; i++)
            cum[i] = cum[i - 1] + Vector2.Distance(pts[i - 1], pts[i]);

        float total = cum[fn - 1];
        var result = new SpineSample[count];

        if (total < 1e-5f)   // 길이 0: 한 점에 뭉침
        {
            for (int j = 0; j < count; j++)
                result[j] = new SpineSample { position = pts[0], angleDeg = 0f, halfWidth = widths[0] };
            return result;
        }

        // 3) 인셋 적용한 샘플링 구간 [lo, hi] 산출. 인셋이 과하면 중점으로 수렴시켜 뒤집힘 방지.
        float lo = Mathf.Clamp(startInset, 0f, total);
        float hi = Mathf.Clamp(total - endInset, 0f, total);
        if (hi < lo)
        {
            float mid = 0.5f * (lo + hi);
            lo = hi = mid;
        }
        float span = hi - lo;

        // 4) 호 길이 균등 샘플링.
        int k = 0;
        for (int j = 0; j < count; j++)
        {
            float target = lo + (j / (float)(count - 1)) * span;

            while (k < fn - 2 && cum[k + 1] < target) k++;

            float segLen = cum[k + 1] - cum[k];
            float frac = segLen > 1e-6f ? (target - cum[k]) / segLen : 0f;

            Vector2 pos = Vector2.Lerp(pts[k], pts[k + 1], frac);
            float width = Mathf.Lerp(widths[k], widths[k + 1], frac);
            Vector2 dir = (pts[k + 1] - pts[k]).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            result[j] = new SpineSample { position = pos, angleDeg = angle, halfWidth = width };
        }

        return result;
    }

    private static void BuildFineLine(SquishShapeSO.SpinePoint[] c, bool smooth,
        out List<Vector2> pts, out List<float> widths)
    {
        pts = new List<Vector2>();
        widths = new List<float>();
        int n = c.Length;

        if (!smooth || n < 3)
        {
            for (int i = 0; i < n; i++)
            {
                pts.Add(c[i].position);
                widths.Add(c[i].halfWidth);
            }
            return;
        }

        // Catmull-Rom: 각 제어 구간을 SubdivPerSegment로 세분화. 양 끝은 이웃 클램프.
        for (int i = 0; i < n - 1; i++)
        {
            Vector2 p0 = c[Mathf.Max(i - 1, 0)].position;
            Vector2 p1 = c[i].position;
            Vector2 p2 = c[i + 1].position;
            Vector2 p3 = c[Mathf.Min(i + 2, n - 1)].position;

            int steps = (i == n - 2) ? SubdivPerSegment : SubdivPerSegment - 1; // 마지막만 끝점 포함
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)SubdivPerSegment;
                pts.Add(CatmullRom(p0, p1, p2, p3, t));
                widths.Add(Mathf.Lerp(c[i].halfWidth, c[i + 1].halfWidth, t));
            }
        }
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2f * p1)
            + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private static SpineSample[] Fallback(int count)
    {
        var r = new SpineSample[count];
        for (int j = 0; j < count; j++)
            r[j] = new SpineSample
            {
                position = new Vector2(Mathf.Lerp(-0.5f, 0.5f, j / (float)(count - 1)), 0f),
                angleDeg = 0f,
                halfWidth = 0.15f
            };
        return r;
    }
}
