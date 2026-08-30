using System;
using System.Collections.Generic;
using UnityEngine;

// 실제 충돌을 부모 체인의 ISquishHittable에게 위임한다. 안착면처럼 솔리드 콜라이더에 붙인다.
// 뼈 하나하나가 아니라 스퀴시가 떨어졌다 새로 닿는 순간에만 전달한다.
public class SquishContactSensor : SquishSensor
{
    public event Action OnSquishExited; // 마지막 뼈가 접촉면에서 떨어졌을 때 1회

    private readonly List<ISquishHittable> _receivers = new();

    private void Awake()
    {
        _receivers.AddRange(GetComponentsInParent<ISquishHittable>(true));

        if (_receivers.Count == 0)
            Debug.LogWarning($"[SquishContactSensor] 충돌을 전달할 ISquishHittable을 찾지 못했습니다: {name}", this);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_receivers.Count == 0) return;
        if (!TryAddSquish(collision.collider)) return;

        foreach (var receiver in _receivers)
            receiver.OnSquishHit(collision);
    }

    private void OnCollisionExit2D(Collision2D collision) => RemoveSquish(collision.collider);

    protected override void OnSquishEnded() => OnSquishExited?.Invoke();
}
