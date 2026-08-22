using System;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public class HandRig : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("화면 밖에서 들어왔다가 드롭 후 빠져나갈 손 비주얼. Rigidbody2D가 붙어 물리 홀더 역할을 한다.")]
    [SerializeField] private Transform handVisual;
    [SerializeField] private Rigidbody2D handBody;

    [Header("입장 / 퇴장")]
    [SerializeField] private bool playIntro = true;
    [Tooltip("Spawn(대기) 위치 기준 화면 밖 Reset 위치 오프셋.")]
    [SerializeField] private Vector2 offScreenOffset = new(-4f, 0f);
    [SerializeField] private float moveDuration = 0.45f;
    [SerializeField] private Ease moveEase = Ease.InOutCubic;

    private Tween _tween;
    private Vector3 _handRestLocalPosition;
    private bool _isPlaying;

    public bool IsPlaying => _isPlaying;
    public Rigidbody2D Body => handBody;

    // Spawn(rest) 월드 위치. 부모가 움직여도 따라가도록 매번 로컬 기준으로 계산한다.
    private Vector2 SpawnWorld => handVisual.parent != null
        ? (Vector2)handVisual.parent.TransformPoint(_handRestLocalPosition)
        : (Vector2)_handRestLocalPosition;

    // 화면 밖 Reset 월드 위치.
    private Vector2 ResetWorld => SpawnWorld + offScreenOffset;

    private void Awake()
    {
        if (handVisual == null) handVisual = transform;
        if (handBody == null) handBody = handVisual.GetComponent<Rigidbody2D>();

        Debug.Assert(handBody != null, "[HandRig] handVisual에 Rigidbody2D가 필요합니다 (물리 홀더 역할)");
        handBody.bodyType = RigidbodyType2D.Kinematic;
        handBody.interpolation = RigidbodyInterpolation2D.Interpolate;

        _handRestLocalPosition = handVisual.localPosition;
    }

    private void OnDestroy()
    {
        _tween?.Kill();
    }

    public void PrepareEnter() => SnapTo(ResetWorld);

    public void PlayEnter(Action onComplete)
    {
        if (!playIntro)
        {
            SnapTo(SpawnWorld);
            onComplete?.Invoke();
            return;
        }

        PlayMove(SpawnWorld, onComplete);
    }

    public void PlayExit()
    {
        if (!playIntro) return;
        PlayMove(ResetWorld, null);
    }

    private void PlayMove(Vector2 target, Action onComplete)
    {
        _tween?.Kill();

        if (moveDuration <= 0f)
        {
            SnapTo(target);
            _isPlaying = false;
            onComplete?.Invoke();
            return;
        }

        _isPlaying = true;
        Vector2 start = handBody.position;
        float progress = 0f;

        _tween = DOTween
            .To(() => progress, value => progress = value, 1f, moveDuration)
            .SetEase(moveEase)
            .SetUpdate(UpdateType.Fixed)
            .SetLink(gameObject)
            .OnUpdate(() => handBody.MovePosition(Vector2.LerpUnclamped(start, target, progress)))
            .OnComplete(() =>
            {
                SnapTo(target);
                _isPlaying = false;
                onComplete?.Invoke();
            });
    }

    // 트윈 없이 목표 월드 위치로 즉시 이동.
    private void SnapTo(Vector2 world)
    {
        _tween?.Kill();
        _isPlaying = false;
        handVisual.position = world;
        Physics2D.SyncTransforms();
    }
}
