using UnityEngine;

// 벨 부위(꼭지·몸통)에 붙어, 바나나가 부딪히면 부모 DeskBell에 알려 종을 울린다.
[RequireComponent(typeof(Collider2D))]
public class DeskBellContact : MonoBehaviour
{
    [Tooltip("비워두면 Awake에서 부모의 DeskBell을 자동으로 찾는다")]
    [SerializeField] private DeskBell deskBell;

    private void Awake()
    {
        if (deskBell == null)
            deskBell = GetComponentInParent<DeskBell>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (deskBell == null) return;
        if (collision.gameObject.GetComponentInParent<BananaThrowController>() == null) return;

        deskBell.RingBell();
    }
}
