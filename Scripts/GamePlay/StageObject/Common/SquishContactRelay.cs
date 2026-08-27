using System.Collections.Generic;
using UnityEngine;

// 콜라이더가 붙은 자식에 붙여, 스퀴시 충돌을 ISquishHittable 수신자들에게 위임하는 범용 릴레이.
[RequireComponent(typeof(Collider2D))]
public class SquishContactRelay : MonoBehaviour
{
    [Tooltip("비워두면 Awake에서 부모의 ISquishHittable을 전부 자동으로 찾는다")]
    [SerializeField] private MonoBehaviour[] targets;

    private readonly List<ISquishHittable> _receivers = new();

    private void Awake()
    {
        CollectReceivers();

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

    // 명시 지정이 있으면 그것만, 비어 있으면 부모 체인의 ISquishHittable을 전부 수집한다.
    private void CollectReceivers()
    {
        _receivers.Clear();

        if (targets != null && targets.Length > 0)
        {
            foreach (var target in targets)
                if (target is ISquishHittable receiver)
                    _receivers.Add(receiver);

            return;
        }

        _receivers.AddRange(GetComponentsInParent<ISquishHittable>(true));
    }
}
