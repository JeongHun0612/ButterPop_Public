using System;
using System.Collections.Generic;
using UnityEngine;

// 스퀴시가 이 트리거 영역 안에 있는지 추적하는 공용 감지 존.
// 스퀴시는 뼈마다 콜라이더가 따로라 진입/이탈 콜백이 뼈 수만큼 오므로,
// 안에 있는 콜라이더를 모아 비었음↔찼음 전이에서만 이벤트를 낸다.
[RequireComponent(typeof(Collider2D))]
public class SquishTriggerZone : MonoBehaviour
{
    // 파괴·비활성된 콜라이더는 Exit 콜백이 오지 않아 집합에 영구히 남는다.
    private static readonly Predicate<Collider2D> IsGone =
        col => col == null || !col.enabled || !col.gameObject.activeInHierarchy;

    private readonly HashSet<Collider2D> _inside = new();

    private Collider2D _collider;

    public event Action OnSquishEntered;   // 첫 뼈가 들어왔을 때 1회
    public event Action OnSquishExited;    // 마지막 뼈가 나갔을 때 1회

    public bool HasSquish => _inside.Count > 0;

    private void Awake()
    {
        // 감지 전용이므로 트리거로 강제한다.
        _collider = GetComponent<Collider2D>();
        _collider.isTrigger = true;
    }

    private void Update()
    {
        if (_inside.Count == 0) return;

        // 콜백 없이 사라진 콜라이더를 걷어낸다. 빠뜨리면 영역이 영영 비지 않는다.
        if (_inside.RemoveWhere(IsGone) > 0 && _inside.Count == 0)
            OnSquishExited?.Invoke();
    }

    private void OnDisable() => _inside.Clear();

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<Squish>() == null) return;

        bool wasEmpty = _inside.Count == 0;
        if (_inside.Add(other) && wasEmpty)
            OnSquishEntered?.Invoke();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (_inside.Remove(other) && _inside.Count == 0)
            OnSquishExited?.Invoke();
    }
}
