using UnityEngine;

// 우주선 하단 빔. 스퀴시가 빔 영역에 진입하면 부모 UFO에 알려 어브덕션을 시작한다.
[RequireComponent(typeof(Collider2D))]
public class UfoBeam : MonoBehaviour
{
    [Tooltip("비워두면 Awake에서 부모의 UFO를 자동으로 찾는다")]
    [SerializeField] private UFO ufo;

    private Collider2D _collider;

    private void Awake()
    {
        if (ufo == null)
            ufo = GetComponentInParent<UFO>();

        // 빔은 스퀴시가 통과하며 감지되는 영역이므로 트리거로 강제한다.
        _collider = GetComponent<Collider2D>();
        _collider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (ufo == null) return;
        if (other.GetComponentInParent<Squish>() == null) return;

        ufo.Abduct();
    }
}
