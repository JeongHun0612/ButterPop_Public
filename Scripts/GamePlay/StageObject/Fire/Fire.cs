using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 불씨 기믹: 바나나가 불꽃에 닿아 있는 동안 점점 타는 연출
// 트리거 감지는 자식 FireContact가 담당하고, 이 스크립트는 태우기 로직만 보유한다.
public class Fire : MonoBehaviour, IStageContextReceiver
{
    [Header("타는 연출")]
    [Tooltip("완전히 타기까지 걸리는 시간(초)")]
    [SerializeField] private float burnDuration = 2f;

    private StageContext _context;
    private Color _baseColor = Color.white;
    private float _burnProgress;
    private CancellationTokenSource _cts;

    private void OnDisable()
    {
        StopBurn();
    }

    public void ReceiveStageContext(StageContext context)
    {
        _context = context;
        _burnProgress = 0f;

        // 태우기 기준색은 바나나 원본 색. 재접촉 시 이어서 타도록 진행도는 유지한다.
        if (_context?.Banana != null && _context.Banana.BananaRenderer != null)
            _baseColor = _context.Banana.BananaRenderer.color;
    }

    public void StartBurn()
    {
        if (_context?.Banana == null || _context.Banana.BananaRenderer == null) return;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        RunBurn(_cts.Token).Forget();
    }

    public void StopBurn()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async UniTaskVoid RunBurn(CancellationToken token)
    {
        // TODO: 타는 효과음 재생

        var renderer = _context.Banana.BananaRenderer;

        while (_burnProgress < 1f && !token.IsCancellationRequested)
        {
            _burnProgress = Mathf.Clamp01(_burnProgress + Time.deltaTime / burnDuration);
            renderer.color = Color.Lerp(_baseColor, Color.black, _burnProgress);

            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // TODO: 완전히 탔을 때 효과음 or 처리
    }
}
