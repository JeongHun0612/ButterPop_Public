using UnityEngine;

// 콜라이더가 붙은 자식에 붙여, 스퀴시 충돌을 부모의 ISquishHittable로 위임하는 범용 릴레이.
[RequireComponent(typeof(Collider2D))]
public class SquishContactRelay : MonoBehaviour
{
    [Tooltip("비워두면 Awake에서 부모의 ISquishHittable을 자동으로 찾는다")]
    [SerializeField] private MonoBehaviour target;

    private ISquishHittable _receiver;

    private void Awake()
    {
        _receiver = target as ISquishHittable ?? GetComponentInParent<ISquishHittable>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_receiver == null) return;
        if (collision.gameObject.GetComponentInParent<SquishThrowController>() == null) return;

        _receiver.OnSquishHit(collision);
    }
}
