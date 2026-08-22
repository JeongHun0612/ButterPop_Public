using UnityEngine;

// 불꽃 영역(트리거)에 붙어, 스퀴시가 들어오면 부모 Fire에 태우기 시작/정지를 알린다.
[RequireComponent(typeof(Collider2D))]
public class FireContact : MonoBehaviour
{
    [Tooltip("비워두면 Awake에서 부모의 Fire를 자동으로 찾는다")]
    [SerializeField] private Fire fire;

    private Collider2D _collider;

    private void Awake()
    {
        if (fire == null)
            fire = GetComponentInParent<Fire>();

        // 불꽃은 스퀴시가 통과하며 타는 영역이므로 트리거로 강제한다.
        _collider = GetComponent<Collider2D>();
        _collider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (fire == null) return;
        if (other.GetComponentInParent<Squish>() == null) return;

        fire.StartBurn();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (fire == null) return;
        if (other.GetComponentInParent<Squish>() == null) return;

        fire.StopBurn();
    }
}
