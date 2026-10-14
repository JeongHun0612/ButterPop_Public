using UnityEngine;

// 렌더러 바운드 관련 공용 유틸.
public static class RendererUtils
{
    // 오브젝트와 그 자식들의 렌더러 바운드를 모두 합친 월드 바운드를 구한다.
    // 렌더러가 하나도 없으면 오브젝트 위치만 담은 크기 0 바운드를 돌려준다.
    public static Bounds GetWorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.zero);

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    // 렌더러 바운드를 합쳐 오브젝트의 시각적 중앙(월드 좌표)을 구한다.
    public static Vector3 GetVisualCenter(GameObject go) => GetWorldBounds(go).center;
}
