using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 스프라이트 프레임을 순차 교체하는 범용 애니메이션 컴포넌트
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteAnimator : MonoBehaviour
{
    [Header("프레임")]
    [SerializeField] private Sprite[] frames;

    [Header("재생 설정")]
    [Tooltip("초당 프레임 수")]
    [SerializeField] private float fps = 8f;
    [SerializeField] private bool loop = true;
    [SerializeField] private bool playOnEnable = true;

    private SpriteRenderer _renderer;
    private CancellationTokenSource _cts;
    private int _currentFrame;
    private bool _isPlaying;

    public bool IsPlaying => _isPlaying;
    public int CurrentFrame => _currentFrame;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (playOnEnable)
            Play();
    }

    private void OnDisable()
    {
        Stop();
    }

    public void Play()
    {
        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning("[SpriteAnimator] frames가 비어 있습니다");
            return;
        }

        Stop();
        _isPlaying = true;
        _cts = new CancellationTokenSource();
        RunAnimation(_cts.Token).Forget();
    }

    public void Stop()
    {
        _isPlaying = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void SetFrame(int index)
    {
        if (frames == null || frames.Length == 0) return;
        _currentFrame = Mathf.Clamp(index, 0, frames.Length - 1);
        _renderer.sprite = frames[_currentFrame];
    }

    public void ResetToFirst()
    {
        SetFrame(0);
    }

    private async UniTaskVoid RunAnimation(CancellationToken token)
    {
        float interval = 1f / Mathf.Max(fps, 0.001f);
        _currentFrame = 0;
        _renderer.sprite = frames[0];

        while (!token.IsCancellationRequested)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: token);

            AdvanceForward();

            _renderer.sprite = frames[_currentFrame];

            if (!loop && _currentFrame == frames.Length - 1)
            {
                _isPlaying = false;
                return;
            }
        }
    }

    private void AdvanceForward()
    {
        _currentFrame = (_currentFrame + 1) % frames.Length;
    }
}
