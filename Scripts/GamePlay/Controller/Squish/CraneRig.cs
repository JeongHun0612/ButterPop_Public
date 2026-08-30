using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class CraneRig : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("화면 밖에서 들어왔다가 드롭 후 빠져나갈 크레인 전체 비주얼.")]
    [FormerlySerializedAs("handVisual")]
    [SerializeField] private Transform craneVisual;
    [Tooltip("스퀴시를 붙잡는 Kinematic 물리 홀더. 일반적으로 CraneVisual의 Rigidbody2D를 사용한다.")]
    [FormerlySerializedAs("handBody")]
    [SerializeField] private Rigidbody2D holderBody;

    [Header("집게")]
    [Tooltip("왼쪽 집게가 회전할 피벗 Transform.")]
    [SerializeField] private Transform leftArmRoot;
    [Tooltip("오른쪽 집게가 회전할 피벗 Transform.")]
    [SerializeField] private Transform rightArmRoot;
    [Min(0f)]
    [SerializeField] private float openAngle = 25f;
    [Min(0f)]
    [SerializeField] private float openDuration = 0.18f;
    [SerializeField] private Ease openEase = Ease.OutCubic;

    [Header("입장 / 퇴장")]
    [SerializeField] private bool playIntro = true;
    [Tooltip("Spawn(대기) 위치 기준 화면 밖 Reset 위치 오프셋.")]
    [SerializeField] private Vector2 offScreenOffset = new(-4f, 0f);
    [SerializeField] private float moveDuration = 0.45f;
    [SerializeField] private Ease moveEase = Ease.InOutCubic;

    private Tween _tween;
    private Vector3 _craneRestLocalPosition;
    private Quaternion _leftClosedLocalRotation;
    private Quaternion _rightClosedLocalRotation;
    private bool _isPlaying;

    public bool IsPlaying => _isPlaying;
    public Rigidbody2D Body => holderBody;

    private Vector2 SpawnWorld => craneVisual.parent != null
        ? (Vector2)craneVisual.parent.TransformPoint(_craneRestLocalPosition)
        : (Vector2)_craneRestLocalPosition;

    private Vector2 ResetWorld => SpawnWorld + offScreenOffset;

    private void Awake()
    {
        if (craneVisual == null) craneVisual = transform;
        if (holderBody == null) holderBody = craneVisual.GetComponent<Rigidbody2D>();

        if (holderBody == null)
        {
            Debug.LogError("[CraneRig] CraneVisual에 Rigidbody2D가 필요합니다.", this);
            enabled = false;
            return;
        }

        holderBody.bodyType = RigidbodyType2D.Kinematic;
        holderBody.interpolation = RigidbodyInterpolation2D.Interpolate;

        _craneRestLocalPosition = craneVisual.localPosition;
        if (leftArmRoot != null) _leftClosedLocalRotation = leftArmRoot.localRotation;
        if (rightArmRoot != null) _rightClosedLocalRotation = rightArmRoot.localRotation;
    }

    private void OnDestroy() => KillTween();

    public void PrepareEnter()
    {
        KillTween();
        ResetArms();
        SnapTo(ResetWorld);
    }

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

    public void PlayRelease(Action onReleased)
    {
        KillTween();

        if (leftArmRoot == null || rightArmRoot == null || openDuration <= 0f)
        {
            ApplyOpenPose();
            onReleased?.Invoke();
            return;
        }

        _isPlaying = true;

        Vector3 leftTarget = _leftClosedLocalRotation.eulerAngles + Vector3.back * openAngle;
        Vector3 rightTarget = _rightClosedLocalRotation.eulerAngles + Vector3.forward * openAngle;

        _tween = DOTween.Sequence()
            .Join(leftArmRoot.DOLocalRotate(leftTarget, openDuration, RotateMode.Fast).SetEase(openEase))
            .Join(rightArmRoot.DOLocalRotate(rightTarget, openDuration, RotateMode.Fast).SetEase(openEase))
            .SetUpdate(UpdateType.Fixed)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                _tween = null;
                _isPlaying = false;
                onReleased?.Invoke();
            });
    }

    public void PlayExit()
    {
        if (!playIntro) return;
        PlayMove(ResetWorld, null);
    }

    private void PlayMove(Vector2 target, Action onComplete)
    {
        KillTween();

        if (moveDuration <= 0f)
        {
            SnapTo(target);
            onComplete?.Invoke();
            return;
        }

        _isPlaying = true;
        Vector2 start = holderBody.position;
        float progress = 0f;

        _tween = DOTween
            .To(() => progress, value => progress = value, 1f, moveDuration)
            .SetEase(moveEase)
            .SetUpdate(UpdateType.Fixed)
            .SetLink(gameObject)
            .OnUpdate(() => holderBody.MovePosition(Vector2.LerpUnclamped(start, target, progress)))
            .OnComplete(() =>
            {
                _tween = null;
                SnapTo(target);
                onComplete?.Invoke();
            });
    }

    private void ApplyOpenPose()
    {
        if (leftArmRoot != null)
            leftArmRoot.localRotation = _leftClosedLocalRotation * Quaternion.Euler(0f, 0f, -openAngle);
        if (rightArmRoot != null)
            rightArmRoot.localRotation = _rightClosedLocalRotation * Quaternion.Euler(0f, 0f, openAngle);
    }

    private void ResetArms()
    {
        if (leftArmRoot != null) leftArmRoot.localRotation = _leftClosedLocalRotation;
        if (rightArmRoot != null) rightArmRoot.localRotation = _rightClosedLocalRotation;
    }

    private void SnapTo(Vector2 world)
    {
        KillTween();
        craneVisual.position = world;
        Physics2D.SyncTransforms();
    }

    private void KillTween()
    {
        _tween?.Kill();
        _tween = null;
        _isPlaying = false;
    }
}
