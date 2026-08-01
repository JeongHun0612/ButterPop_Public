using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class SleepZEffect : MonoBehaviour
{
    [Header("풀")]
    [Tooltip("SleepZCanvas 하위에 미리 배치한 Z 오브젝트들 (아래→위 순서). 배열 개수 = 한 사이클에 나오는 Z 개수")]
    [SerializeField] private TMP_Text[] zTexts;

    [Header("타이밍")]
    [Tooltip("Z가 하나씩 나오는 간격(초)")]
    [SerializeField] private float spawnInterval = 0.35f;
    [Tooltip("한 사이클이 끝난 뒤 다음 사이클까지 대기 시간(초)")]
    [SerializeField] private float repeatDelay = 2f;
    [Tooltip("각 Z가 떠오르고 사라지기까지 걸리는 시간(초)")]
    [SerializeField] private float duration = 1.2f;

    [Header("움직임")]
    [Tooltip("원점에서 위로 떠오르는 거리(로컬)")]
    [SerializeField] private float moveUpDistance = 2f;
    [Tooltip("원점에서 오른쪽으로 이동하는 거리(로컬). 위 이동과 합쳐져 대각선이 됨")]
    [SerializeField] private float moveRightDistance = 1f;
    [Tooltip("애니메이션 종료 시 스케일 (시작은 항상 1)")]
    [SerializeField] private float endScale = 2f;
    [Tooltip("전체 시간 중 페이드아웃이 시작되는 지점(0~1)")]
    [Range(0f, 1f)]
    [SerializeField] private float fadeStartRatio = 0.4f;

    private CancellationTokenSource _cts;

    private void Awake()
    {
        // 시작 시 전부 숨김
        if (zTexts == null) return;
        foreach (var z in zTexts)
            if (z != null) z.alpha = 0f;
    }

    private void OnEnable()
    {
        _cts = new CancellationTokenSource();
        PlayLoop(_cts.Token).Forget();
    }

    private void OnDisable()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (zTexts == null) return;
        foreach (var z in zTexts)
        {
            if (z == null) continue;
            z.transform.DOKill();
            z.alpha = 0f;
        }
    }

    private async UniTaskVoid PlayLoop(CancellationToken ct)
    {
        if (zTexts == null || zTexts.Length == 0) return;

        while (!ct.IsCancellationRequested)
        {
            for (int i = 0; i < zTexts.Length; i++)
            {
                AnimateZ(zTexts[i], i);
                if (i < zTexts.Length - 1)
                    await UniTask.Delay((int)(spawnInterval * 1000), cancellationToken: ct);
            }

            await UniTask.Delay((int)((duration + repeatDelay) * 1000), cancellationToken: ct);
        }
    }

    private void AnimateZ(TMP_Text z, int index)
    {
        if (z == null) return;

        var tr = z.transform;
        tr.DOKill();

        // 원점(0,0)·스케일 1에서 시작 → 대각선 오른쪽 위로 이동하며 점점 커짐
        tr.localPosition = Vector3.zero;
        tr.localScale = Vector3.one;
        z.alpha = 1f;

        float fadeDelay = duration * fadeStartRatio;

        DOTween.Sequence()
            .Join(tr.DOLocalMove(new Vector3(moveRightDistance, moveUpDistance, 0f), duration).SetEase(Ease.OutSine))
            .Join(tr.DOScale(endScale, duration).SetEase(Ease.OutQuad))
            .Insert(fadeDelay, DOTween.To(() => z.alpha, a => z.alpha = a, 0f, duration - fadeDelay))
            .SetLink(z.gameObject);
    }
}
