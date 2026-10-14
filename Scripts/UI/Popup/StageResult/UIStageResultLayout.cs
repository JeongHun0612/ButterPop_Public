using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class UIStageResultLayout : MonoBehaviour
{
    [Header("부모 영역 여백")]
    [Tooltip("배너와 안전 영역이 적용된 부모 안에서 추가로 비울 캔버스 단위 여백")]
    [SerializeField] private RectOffset padding = new RectOffset();

    private RectTransform _rect;
    private RectTransform _parent;

    private void OnEnable()
    {
        _rect = GetComponent<RectTransform>();
        CacheParent();
        RefreshLayout();
    }

    private void LateUpdate() => RefreshLayout();

    private void OnTransformParentChanged()
    {
        if (_rect == null) return;
        CacheParent();
        RefreshLayout();
    }

    private void CacheParent()
    {
        _parent = _rect.parent as RectTransform;
        if (_parent != null) return;

        Debug.LogError("[UIStageResultLayout] 부모 RectTransform이 필요합니다.", this);
        enabled = false;
    }

    private void RefreshLayout()
    {
        if (_rect == null || _parent == null) return;

        var size = _rect.rect.size;
        if (size.x <= 0f || size.y <= 0f) return;

        var available = _parent.rect.size - new Vector2(padding.horizontal, padding.vertical);
        float scale = Mathf.Clamp01(Mathf.Min(available.x / size.x, available.y / size.y));
        var targetScale = new Vector3(scale, scale, 1f);
        var targetPosition = new Vector2((padding.left - padding.right) * 0.5f,
            (padding.bottom - padding.top) * 0.5f);

        if (_rect.localScale != targetScale) _rect.localScale = targetScale;
        if (_rect.anchoredPosition != targetPosition) _rect.anchoredPosition = targetPosition;
    }
}
