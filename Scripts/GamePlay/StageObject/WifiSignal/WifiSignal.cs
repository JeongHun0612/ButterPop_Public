using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 와이파이 신호 기믹: 프레임을 순차 교체해 누적 점등하고, 마지막 프레임에서 일정 시간 콜라이더가 솔리드가 된다.
[RequireComponent(typeof(Collider2D))]
public class WifiSignal : MonoBehaviour
{
    [Header("신호 프레임")]
    [Tooltip("점등 순서대로 등록. 마지막 프레임이 전체 점등 상태")]
    [SerializeField] private SpriteRenderer signalRenderer;
    [SerializeField] private Sprite[] frames;

    [Header("타이밍")]
    [Tooltip("프레임이 하나 넘어가는 간격(초)")]
    [SerializeField] private float litInterval = 0.5f;
    [Tooltip("전체 점등 후 콜라이더 솔리드 유지 시간(초). 안착 판정(0.5s)보다 충분히 커야 클리어 가능")]
    [SerializeField] private float activeDuration = 5f;

    [Header("플랫폼")]
    [Tooltip("활성 창 동안만 솔리드가 되는 콜라이더 (StageObject와 같은 GameObject에 있어야 함). 평소에는 트리거로 두어 스퀴시가 통과한다")]
    [SerializeField] private Collider2D platformCollider;

    private CancellationTokenSource _cts;

    // 스퀴시는 뼈마다 콜라이더가 따로라 진입/이탈 콜백이 뼈 수만큼 온다.
    // bool로 받으면 뼈 하나만 빠져나가도 "전부 나갔다"가 되므로 안에 있는 콜라이더를 모아 센다.
    private readonly HashSet<Collider2D> _squishInside = new();

    private bool HasSquish => _squishInside.Count > 0;

    private void OnEnable()
    {
        if (signalRenderer == null || frames == null || frames.Length == 0 || platformCollider == null)
        {
            Debug.LogError("[WifiSignal] signalRenderer / frames / platformCollider 참조가 비어 있습니다");
            return;
        }

        // 콜라이더는 항상 켜둔 채 isTrigger로 통과/솔리드를 전환한다.
        platformCollider.enabled = true;

        _cts = new CancellationTokenSource();
        RunCycleLoop(_cts.Token).Forget();
    }

    private void OnDisable()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _squishInside.Clear();
        ResetState();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<Squish>() != null)
            _squishInside.Add(other);
    }

    private void OnTriggerExit2D(Collider2D other) => _squishInside.Remove(other);

    private async UniTaskVoid RunCycleLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            // 누적 점등: 첫 프레임부터 마지막까지 순차 교체.
            for (int i = 0; i < frames.Length; i++)
            {
                signalRenderer.sprite = frames[i];

                if (i == frames.Length - 1)
                {
                    // 마지막 = 전체 점등. 솔리드로 바꾸는 순간 스퀴시가 영역 안에 박혀 튕기지 않도록,
                    // 뼈가 전부 빠져나갈 때까지 대기한 뒤 활성 창을 연다.
                    await UniTask.WaitUntil(() => !HasSquish, cancellationToken: token);
                    platformCollider.isTrigger = false;
                    await UniTask.Delay(TimeSpan.FromSeconds(activeDuration), cancellationToken: token);
                }
                else
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(litInterval), cancellationToken: token);
                }
            }

            // 사이클 종료: 다시 통과 가능(트리거)하게 하고 첫 프레임으로 리셋.
            // 솔리드 구간에는 트리거 콜백이 오지 않아 집계가 낡으므로 여기서 버린다.
            platformCollider.isTrigger = true;
            _squishInside.Clear();
            signalRenderer.sprite = frames[0];
        }
    }

    private void ResetState()
    {
        if (platformCollider != null)
            platformCollider.isTrigger = true;

        if (signalRenderer != null && frames != null && frames.Length > 0)
            signalRenderer.sprite = frames[0];
    }
}
