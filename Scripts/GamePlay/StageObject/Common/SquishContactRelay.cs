using System.Collections.Generic;
using UnityEngine;

// 콜라이더가 붙은 자식에 붙여, 스퀴시 충돌을 부모 체인의 ISquishHittable 수신자들에게 위임하는 범용 릴레이.
[RequireComponent(typeof(Collider2D))]
public class SquishContactRelay : MonoBehaviour
{
    private readonly List<ISquishHittable> _receivers = new();

    private void Awake()
    {
        _receivers.AddRange(GetComponentsInParent<ISquishHittable>(true));

        if (_receivers.Count == 0)
            Debug.LogWarning($"[SquishContactRelay] 충돌을 전달할 ISquishHittable을 찾지 못했습니다: {name}", this);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_receivers.Count == 0) return;
        if (collision.gameObject.GetComponentInParent<Squish>() == null) return;

        foreach (var receiver in _receivers)
            receiver.OnSquishHit(collision);
    }
}
