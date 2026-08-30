using System;
using System.Collections.Generic;
using UnityEngine;

// 스퀴시가 이 콜라이더에 닿아 있는지 추적하는 감지 베이스.
// 스퀴시는 뼈마다 콜라이더가 따로라 콜백이 뼈 수만큼 오므로, 닿은 콜라이더를 모아
// 비었음↔찼음 전이에서만 알린다. 집합을 채우는 콜백은 파생 클래스가 정한다.
[RequireComponent(typeof(Collider2D))]
public abstract class SquishSensor : MonoBehaviour
{
    [Header("접촉 안정화")]
    [Tooltip("마지막 스퀴시 콜라이더가 떨어진 뒤 실제 이탈로 확정하기까지 기다리는 시간(초)")]
    [Min(0f)]
    [SerializeField] private float exitGraceDuration;

    // 파괴·비활성된 콜라이더는 이탈 콜백이 오지 않아 집합에 영구히 남는다.
    private static readonly Predicate<Collider2D> IsGone =
        col => col == null || !col.enabled || !col.gameObject.activeInHierarchy;

    private readonly HashSet<Collider2D> _tracked = new();
    private bool _hasLogicalSquish;
    private bool _isExitPending;
    private float _exitStartedTime;

    // 이탈 유예 중에는 실제 접촉 수가 0이어도 아직 접촉 중인 것으로 간주한다.
    public bool HasSquish => _hasLogicalSquish;

    private void Update()
    {
        if (_tracked.Count > 0 && _tracked.RemoveWhere(IsGone) > 0 && _tracked.Count == 0)
            BeginSquishEnd();

        if (!_isExitPending || _tracked.Count > 0) return;
        if (Time.time - _exitStartedTime < exitGraceDuration) return;

        CompleteSquishEnd();
    }

    private void OnDisable()
    {
        _tracked.Clear();
        _hasLogicalSquish = false;
        _isExitPending = false;
    }

    // 마지막 뼈까지 빠져 스퀴시가 완전히 떠났을 때 1회.
    protected virtual void OnSquishEnded() { }

    // 스퀴시의 콜라이더면 집합에 넣는다. 비어 있던 집합이 찼으면 true.
    protected bool TryAddSquish(Collider2D other)
    {
        if (other.GetComponentInParent<Squish>() == null) return false;
        if (!_tracked.Add(other)) return false;

        // 유예 시간 안에 돌아온 접촉은 기존 접촉이 이어진 것으로 처리한다.
        if (_hasLogicalSquish)
        {
            _isExitPending = false;
            return false;
        }

        _hasLogicalSquish = true;
        return true;
    }

    // 집합에서 빼고 마지막 하나였으면 이탈 유예를 시작한다.
    protected void RemoveSquish(Collider2D other)
    {
        if (_tracked.Remove(other) && _tracked.Count == 0)
            BeginSquishEnd();
    }

    private void BeginSquishEnd()
    {
        if (!_hasLogicalSquish || _isExitPending) return;

        if (exitGraceDuration <= 0f)
        {
            CompleteSquishEnd();
            return;
        }

        _isExitPending = true;
        _exitStartedTime = Time.time;
    }

    private void CompleteSquishEnd()
    {
        if (!_hasLogicalSquish || _tracked.Count > 0) return;

        _hasLogicalSquish = false;
        _isExitPending = false;
        OnSquishEnded();
    }
}
