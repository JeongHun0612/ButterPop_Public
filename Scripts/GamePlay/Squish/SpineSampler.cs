using System;
using System.Collections.Generic;
using UnityEngine;

// 스파인 제어점: 중심선 위 한 점 + 그 지점의 반폭(중심선→표면 거리). 형태 정의의 최소 단위.
[Serializable]
public struct SpinePoint
{
    public Vector2 position;
    public float halfWidth;
}

// 리샘플 결과 한 점: 위치 + 접선각(뼈 rest 회전 / 메쉬 법선 기준) + 반폭.
public struct SpineSample
{
    public Vector2 position;
    public float angleDeg;
    public float halfWidth;
}

// 스파인(제어점 폴리라인)을 호 길이 기준으로 균등 리샘플링하는 순수 지오메트리 유틸.
// - 같은 스파인에서 물리 뼈(boneCount)와 비주얼 격자 열(meshCols)을 서로 다른 해상도로 뽑는다.
// - 호 길이 균등이라 제어점 분포가 치우쳐 있어도 결과는 곡선을 따라 고르게 분포하고,
//   정규화 인덱스 = 호 길이 비율이 되어 뼈와 정점을 arc 파라미터로 매칭해 스키닝할 수 있다.
public static class SpineSampler
{
    private const int SubdivPerSegment = 16;   // Catmull-Rom 한 구간당 세분화 밀도

    // control을 count개(>=2)로 호 길이 균등 리샘플링한다.
    // startInset/endInset: 양 끝에서 호 길이만큼 안으로 당겨 샘플링(월드 단위).
    //   물리 뼈는 끝단 반폭만큼 인셋해 원형 캡이 실루엣 밖으로 안 튀게 한다(캡슐 모델).
    //   비주얼 격자는 인셋 0으로 스파인 전체를 덮는다.
    public static SpineSample[] Resample(SpinePoint[] control, int count, bool smooth,
        float startInset = 0f, float endInset = 0f)
    {
        count = Mathf.Max(count, 2);

        if (control == null || control.Length < 2)
            return Fallback(count);

        // 1) 제어점을 조밀 폴리라인으로 펼친다(Catmull-Rom 곡선 또는 직선).
        BuildFineLine(control, smooth, out var pts, out var widths);

        // 2) 조밀선의 누적 호 길이(cum[i] = 시작부터 pts[i]까지의 곡선 길이).
        int fn = pts.Count;
        var cum = new float[fn];
        cum[0] = 0f;
        for (int i = 1; i < fn; i++)
            cum[i] = cum[i - 1] + Vector2.Distance(pts[i - 1], pts[i]);

        float total = cum[fn - 1];
        var result = new SpineSample[count];

        // 길이 ≈ 0(제어점 뭉침): 분배할 길이가 없으니 실제 위치·폭을 보존해 한 점에 모아 반환.
        if (total < 1e-5f)
        {
            for (int j = 0; j < count; j++)
                result[j] = new SpineSample { position = pts[0], angleDeg = 0f, halfWidth = widths[0] };
            return result;
        }

        // 3) 인셋을 반영한 샘플링 구간 [lo, hi]. 인셋 합이 전체 길이를 넘으면 중점으로 수렴시켜 뒤집힘 방지.
        float lo = Mathf.Clamp(startInset, 0f, total);
        float hi = Mathf.Clamp(total - endInset, 0f, total);
        if (hi < lo)
        {
            float mid = 0.5f * (lo + hi);
            lo = hi = mid;
        }
        float span = hi - lo;

        // 4) [lo, hi]를 균등한 거리 target으로 나누고, 각 target이 놓인 세그먼트를 찾아 값을 보간한다.
        int k = 0;   // 단조 전진 커서 → 전체 O(fn+count) 한 번 훑기
        for (int j = 0; j < count; j++)
        {
            float target = lo + (j / (float)(count - 1)) * span;

            while (k < fn - 2 && cum[k + 1] < target) k++;

            float segLen = cum[k + 1] - cum[k];
            float frac = segLen > 1e-6f ? (target - cum[k]) / segLen : 0f;

            Vector2 pos = Vector2.Lerp(pts[k], pts[k + 1], frac);
            float width = Mathf.Lerp(widths[k], widths[k + 1], frac);
            Vector2 dir = (pts[k + 1] - pts[k]).normalized;   // 세그먼트 접선
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            result[j] = new SpineSample { position = pos, angleDeg = angle, halfWidth = width };
        }

        return result;
    }

    // 제어점을 촘촘한 폴리라인(pts)과 짝지어진 반폭(widths)으로 펼친다.
    // smooth면 Catmull-Rom 세분화, 아니면(또는 점 3개 미만) 제어점을 그대로 직선 연결.
    private static void BuildFineLine(SpinePoint[] c, bool smooth,
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

        // 구간 [c[i], c[i+1]]마다 세분화. 접선은 이웃(p0, p3)으로 잡고, 양 끝은 이웃이 없어 자기 클램프.
        for (int i = 0; i < n - 1; i++)
        {
            Vector2 p0 = c[Mathf.Max(i - 1, 0)].position;
            Vector2 p1 = c[i].position;
            Vector2 p2 = c[i + 1].position;
            Vector2 p3 = c[Mathf.Min(i + 2, n - 1)].position;

            // 이음새 중복 방지: 중간 구간은 끝점(t=1)을 다음 구간에 양보하고, 마지막 구간만 포함한다.
            int steps = (i == n - 2) ? SubdivPerSegment : SubdivPerSegment - 1;
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)SubdivPerSegment;
                pts.Add(CatmullRom(p0, p1, p2, p3, t));
                widths.Add(Mathf.Lerp(c[i].halfWidth, c[i + 1].halfWidth, t));   // 폭은 선형 보간
            }
        }
    }

    // uniform Catmull-Rom(3차 Hermite 스플라인). t=0→p1, t=1→p2를 지나며 제어점을 통과한다.
    // 접선: p1에서 (p2-p0)/2, p2에서 (p3-p1)/2. 계수는 표준 기저행렬 값(튜닝 대상 아님).
    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2f * p1)
            + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    // 유효한 제어점이 없을 때(2개 미만)의 방어용 기본 형태: 길이 1·직선의 밋밋한 막대.
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