using DG.Tweening;
using UnityEngine;

// 우주인처럼 무중력 공간에 떠 있는 느낌을 주는 모션.
// 상하 이동과 좌우 기울임(회전)을 서로 다른 주기로 겹쳐 자연스러운 부유감을 낸다.
public class FloatingMotion : MonoBehaviour
{
    [Header("상하 이동")]
    [Tooltip("위아래로 떠오르는 진폭(로컬 단위)")]
    [SerializeField] private float bobDistance = 0.3f;
    [Tooltip("상하로 한 번 왕복하는 데 걸리는 시간(초)")]
    [SerializeField] private float bobDuration = 2f;

    [Header("좌우 기울임")]
    [Tooltip("좌우로 기우는 최대 각도(도). 0이면 회전하지 않음")]
    [SerializeField] private float swayAngle = 4f;
    [Tooltip("좌우로 한 번 왕복하는 데 걸리는 시간(초). 상하와 다르게 두면 더 자연스럽다")]
    [SerializeField] private float swayDuration = 3.2f;

    private Vector3 _originPosition;
    private Vector3 _originRotation;
    private Sequence _sequence;

    private void Awake()
    {
        _originPosition = transform.localPosition;
        _originRotation = transform.localEulerAngles;
    }

    private void OnEnable() => Play();

    private void OnDisable()
    {
        _sequence?.Kill();
        _sequence = null;

        transform.localPosition = _originPosition;
        transform.localEulerAngles = _originRotation;
    }

    private void Play()
    {
        // 시작 위치를 기준으로 위/아래로 떠오르도록 먼저 아래로 반 진폭 내린다.
        transform.localPosition = _originPosition - new Vector3(0f, bobDistance * 0.5f, 0f);

        _sequence = DOTween.Sequence().SetLink(gameObject);

        _sequence.Join(
            transform.DOLocalMoveY(_originPosition.y + bobDistance * 0.5f, bobDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo));

        if (!Mathf.Approximately(swayAngle, 0f))
        {
            transform.localEulerAngles = _originRotation - new Vector3(0f, 0f, swayAngle);
            _sequence.Join(
                transform.DOLocalRotate(_originRotation + new Vector3(0f, 0f, swayAngle), swayDuration)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo));
        }
    }
}
