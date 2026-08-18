#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// SquishBody의 물리 뼈 체인(Bone_0..N)을 에디터에서 프리팹에 구워 넣는다.
// 런타임 생성 경로는 없다 → SquishBody가 여기서 구운 뼈를 그대로 불러온다.
// 여기선 "형태"(위치·콜라이더 반지름·힌지 앵커)만 굽는다.
// 물성(질량·중력·마찰·탄성)은 SquishBody가 런타임에 배정한다 → 인스펙터에서 바꾸면 바로 반영된다.
public static class SquishBoneBaker
{
    private const float MinRadius = 0.05f;

    public static void Bake(SquishBody body)
    {
        if (body == null) return;
        if (body.Length <= 0f || body.Width <= 0f)
        {
            Debug.LogError("[SquishBoneBaker] length·width는 0보다 커야 합니다.");
            return;
        }

        Clear(body);

        var rest = BuildRest(body.Length, body.Width, body.BoneCount);
        int n = rest.Length;

        var created = new Rigidbody2D[n];
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject($"Bone_{i}");
            Undo.RegisterCreatedObjectUndo(go, "Bake Squish Bones");
            go.transform.SetParent(body.transform, false);
            go.transform.localPosition = new Vector3(rest[i].position.x, rest[i].position.y, 0f);

            created[i] = go.AddComponent<Rigidbody2D>();   // 물성은 런타임에 설정

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = Mathf.Max(rest[i].halfWidth, MinRadius);   // 반지름(형태)만 굽는다. 물성 재질은 런타임에 배정

            go.AddComponent<SquishSegmentRelay>();   // Init은 런타임에 수행
        }

        for (int i = 0; i < n - 1; i++)
        {
            var hinge = created[i].gameObject.AddComponent<HingeJoint2D>();
            ConfigureHinge(hinge, created[i + 1], rest[i], rest[i + 1]);
        }

        EditorUtility.SetDirty(body);
        Debug.Log($"[SquishBoneBaker] 뼈 {n}개를 구웠습니다.");
    }

    // 구운 뼈(SquishSegmentRelay 보유 자식)를 모두 제거한다.
    public static void Clear(SquishBody body)
    {
        if (body == null) return;

        var toDelete = new List<GameObject>();
        foreach (Transform child in body.transform)
        {
            if (child.GetComponent<SquishSegmentRelay>() != null)
                toDelete.Add(child.gameObject);
        }
        foreach (var go in toDelete)
            Undo.DestroyObjectImmediate(go);
    }

    // 중심선을 따라 뼈 rest 포즈를 균등 배치한다: x축 등간격 직선, 반폭 상수.
    // 양 끝은 반폭만큼 인셋 → 원형 콜라이더 캡이 비주얼 밖으로 튀지 않는다(캡슐 모델).
    private static SpineSample[] BuildRest(float length, float width, int boneCount)
    {
        int n = Mathf.Max(boneCount, 2);
        float half = width * 0.5f;

        float lo = -length * 0.5f + half;
        float hi = length * 0.5f - half;
        if (hi < lo)   // 길이가 캡보다 짧으면 뒤집힘 방지: 중점으로 수렴.
        {
            float mid = 0.5f * (lo + hi);
            lo = hi = mid;
        }

        var arr = new SpineSample[n];
        for (int i = 0; i < n; i++)
        {
            float x = Mathf.Lerp(lo, hi, i / (float)(n - 1));
            arr[i] = new SpineSample { position = new Vector2(x, 0f), halfWidth = half };
        }
        return arr;
    }

    // 이웃한 두 뼈를 힌지로 연결. 두 뼈 중점을 각 뼈 로컬 앵커로 지정 → rest 포즈에서 정확히 맞물린다.
    // 각도 제한은 런타임 SquishBody.ApplyBendLimits가 별도로 적용한다.
    private static void ConfigureHinge(HingeJoint2D hinge, Rigidbody2D connected, SpineSample a, SpineSample b)
    {
        Vector2 pa = a.position;
        Vector2 pb = b.position;
        Vector2 mid = 0.5f * (pa + pb);

        hinge.connectedBody = connected;
        hinge.autoConfigureConnectedAnchor = false;
        hinge.anchor = mid - pa;
        hinge.connectedAnchor = mid - pb;
        hinge.enableCollision = false;
        hinge.useLimits = true;
    }
}
#endif
