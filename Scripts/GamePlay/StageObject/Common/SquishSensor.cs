using System;
using System.Collections.Generic;
using UnityEngine;

// 스퀴시가 이 콜라이더에 닿아 있는지 추적하는 감지 베이스.
// 스퀴시는 뼈마다 콜라이더가 따로라 콜백이 뼈 수만큼 오므로, 닿은 콜라이더를 모아
// 비었음↔찼음 전이에서만 알린다. 집합을 채우는 콜백은 파생 클래스가 정한다.
[RequireComponent(typeof(Collider2D))]
public abstract class SquishSensor : MonoBehaviour
{
    // 파괴·비활성된 콜라이더는 이탈 콜백이 오지 않아 집합에 영구히 남는다.
    private static readonly Predicate<Collider2D> IsGone =
        col => col == null || !col.enabled || !col.gameObject.activeInHierarchy;

    private readonly HashSet<Collider2D> _tracked = new();

    public bool HasSquish => _tracked.Count > 0;

    private void Update()
    {
        if (_tracked.Count == 0) return;

        if (_tracked.RemoveWhere(IsGone) > 0 && _tracked.Count == 0)
            OnSquishEnded();
    }

    private void OnDisable() => _tracked.Clear();

    // 마지막 뼈까지 빠져 스퀴시가 완전히 떠났을 때 1회.
    protected virtual void OnSquishEnded() { }

    // 스퀴시의 콜라이더면 집합에 넣는다. 비어 있던 집합이 찼으면 true.
    protected bool TryAddSquish(Collider2D other)
    {
        if (other.GetComponentInParent<Squish>() == null) return false;

        bool wasEmpty = _tracked.Count == 0;
        return _tracked.Add(other) && wasEmpty;
    }

    // 집합에서 빼고, 마지막 하나였으면 OnSquishEnded를 호출한다.
    protected void RemoveSquish(Collider2D other)
    {
        if (_tracked.Remove(other) && _tracked.Count == 0)
            OnSquishEnded();
    }
}
