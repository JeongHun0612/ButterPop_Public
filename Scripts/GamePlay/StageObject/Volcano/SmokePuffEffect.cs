using DG.Tweening;
using UnityEngine;

// 화산 연기 연출: 연기 조각들이 분화구에서 커지며 떠오르다 흩어지는 것을 시차를 두고 반복해 연기가 끊이지 않게 한다.
public class SmokePuffEffect : MonoBehaviour
{
    [Header("연기")]
    [Tooltip("같은 연기 스프라이트를 그리는 렌더러들. 피벗을 분화구 쪽(아래)에 두고 분화구 위치에 배치한다. 개수만큼 주기를 나눠 시차를 둔다")]
    [SerializeField] private SpriteRenderer[] puffs;

    [Header("타이밍")]
    [Tooltip("연기 하나가 피어올라 사라지기까지 걸리는 시간(초)")]
    [Min(0.1f)]
    [SerializeField] private float cycleDuration = 2.4f;

    [Header("움직임")]
    [Tooltip("한 주기 동안 위로 떠오르는 거리(로컬)")]
    [SerializeField] private float riseDistance = 0.8f;
    [Tooltip("피어오르기 시작할 때의 크기 배율")]
    [Min(0f)]
    [SerializeField] private float startScale = 0.6f;
    [Tooltip("사라질 때의 크기 배율")]
    [Min(0f)]
    [SerializeField] private float endScale = 1.3f;

    [Header("페이드")]
    [Tooltip("주기 중 완전히 나타나기까지의 비율(0~1)")]
    [Range(0f, 1f)]
    [SerializeField] private float fadeInRatio = 0.2f;
    [Tooltip("주기 중 사라지기 시작하는 비율(0~1)")]
    [Range(0f, 1f)]
    [SerializeField] private float fadeOutStartRatio = 0.55f;

    private Vector3[] _originPositions;
    private Vector3[] _originScales;
    private Color[] _originColors;
    private Tween[] _tweens;

    private void Awake()
    {
        if (puffs == null || puffs.Length == 0)
        {
            Debug.LogError("[SmokePuffEffect] puffs가 비어 있습니다", this);
            enabled = false;
            return;
        }

        _originPositions = new Vector3[puffs.Length];
        _originScales = new Vector3[puffs.Length];
        _originColors = new Color[puffs.Length];
        _tweens = new Tween[puffs.Length];

        for (int i = 0; i < puffs.Length; i++)
        {
            if (puffs[i] == null) continue;

            _originPositions[i] = puffs[i].transform.localPosition;
            _originScales[i] = puffs[i].transform.localScale;
            _originColors[i] = puffs[i].color;
        }
    }

    private void OnEnable()
    {
        for (int i = 0; i < puffs.Length; i++)
        {
            if (puffs[i] == null) continue;

            int index = i;
            // 위치·크기·투명도를 진행도 하나로 계산해, 루프가 다시 시작될 때 시작값이 어긋나지 않게 한다.
            _tweens[i] = DOTween.To(() => 0f, t => ApplyPuff(index, t), 1f, cycleDuration)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetLink(puffs[i].gameObject);
            _tweens[i].Goto(cycleDuration * i / puffs.Length, true);
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < puffs.Length; i++)
        {
            _tweens[i]?.Kill();
            _tweens[i] = null;

            if (puffs[i] == null) continue;

            var tr = puffs[i].transform;
            tr.localPosition = _originPositions[i];
            tr.localScale = _originScales[i];
            puffs[i].color = _originColors[i];
        }
    }

    private void ApplyPuff(int index, float t)
    {
        var tr = puffs[index].transform;
        float rise = Mathf.Sin(t * Mathf.PI * 0.5f);
        tr.localPosition = _originPositions[index] + Vector3.up * (riseDistance * rise);
        tr.localScale = _originScales[index] * Mathf.Lerp(startScale, endScale, t);

        var color = _originColors[index];
        color.a *= GetAlpha(t);
        puffs[index].color = color;
    }

    private float GetAlpha(float t)
    {
        if (fadeInRatio > 0f && t < fadeInRatio) return t / fadeInRatio;
        if (t > fadeOutStartRatio) return 1f - Mathf.InverseLerp(fadeOutStartRatio, 1f, t);
        return 1f;
    }
}
