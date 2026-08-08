using UnityEngine;

// 콜라이더가 붙은 자식에 붙여, 바나나 충돌을 부모의 IBananaHittable로 위임하는 범용 릴레이.
[RequireComponent(typeof(Collider2D))]
public class BananaContactRelay : MonoBehaviour
{
    [Tooltip("비워두면 Awake에서 부모의 IBananaHittable을 자동으로 찾는다")]
    [SerializeField] private MonoBehaviour target;

    private IBananaHittable _receiver;

    private void Awake()
    {
        _receiver = target as IBananaHittable ?? GetComponentInParent<IBananaHittable>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_receiver == null) return;
        if (collision.gameObject.GetComponentInParent<BananaThrowController>() == null) return;

        _receiver.OnBananaHit(collision);
    }
}
