using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 불씨 기믹: 스퀴시가 불꽃에 닿아 있는 동안 점점 타는 연출
// 트리거 감지는 자식 FireContact가 담당하고, 이 스크립트는 태우기 로직만 보유한다.
public class Fire : MonoBehaviour, IStageContextReceiver
{
    [Header("타는 연출")]
    [Tooltip("완전히 타기까지 걸리는 시간(초)")]
    [SerializeField] private float burnDuration = 2f;

    [Header("사운드")]
    [Tooltip("타기 시작할 때 한 번 재생할 클립")]
    [SerializeField] private AudioClip burnClip;

    private StageContext _context;
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
    }

    public void StartBurn()
    {
        if (_context?.Squish == null || _context.Squish.Mesh == null) return;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        RunBurn(_cts.Token).Forget();

        PlayBurnSound();
    }

    public void StopBurn()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _burnProgress = 0f;
    }

    private async UniTaskVoid RunBurn(CancellationToken token)
    {
        var mesh = _context.Squish.Mesh;

        while (_burnProgress < 1f && !token.IsCancellationRequested)
        {
            _burnProgress = Mathf.Clamp01(_burnProgress + Time.deltaTime / burnDuration);
            // 정점 컬러를 흰색→검정으로 낮춰 스퀴시 전체를 균일하게 태운다(원복은 RespawnAt).
            mesh.SetTint(Color.Lerp(Color.white, Color.black, _burnProgress));

            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }

    private void PlayBurnSound()
    {
        if (burnClip == null) return;

        if (SoundManager.TryGetInstance(out var sound))
            sound.PlaySfx(burnClip);
    }
}
