using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;
using UnityEngine;

public class DefaultSuccessEffect : SuccessEffect
{
    [Header("카메라 이미지")]
    [SerializeField] private SpriteRenderer topCamera;
    [SerializeField] private SpriteRenderer bottomCamera;

    [Header("연출 설정")]
    [SerializeField] private float slideInDuration = 0.5f;
    [SerializeField] private float holdDuration = 1.0f;
    [SerializeField] private float slideOutDuration = 0.4f;
    [SerializeField] private Ease slideInEase = Ease.OutBack;
    [SerializeField] private Ease slideOutEase = Ease.InBack;

    // 카메라 이미지가 화면 안으로 얼마나 들어올지 (이미지 높이 비율, 0~1)
    [SerializeField] private float peekRatio = 0.3f;

    private void Awake()
    {
        topCamera.gameObject.SetActive(false);
        bottomCamera.gameObject.SetActive(false);
    }

    public override async UniTask PlayEffect(CancellationToken ct = default)
    {
        var cam = Camera.main;

        float depth = Mathf.Abs(cam.transform.position.z);

        Vector3 screenTop    = cam.ViewportToWorldPoint(new Vector3(0.5f, 1f, depth));
        Vector3 screenBottom = cam.ViewportToWorldPoint(new Vector3(0.5f, 0f, depth));

        // Pivot이 Bottom이므로 이미지 전체 높이만큼 밀어내면 화면 밖으로 완전히 사라짐
        float topHeight    = topCamera.bounds.size.y;
        float bottomHeight = bottomCamera.bounds.size.y;

        Vector3 topStart    = screenTop    + Vector3.up   * topHeight;
        Vector3 bottomStart = screenBottom + Vector3.down * bottomHeight;

        // 화면 끝에서 이미지 높이의 peekRatio 만큼만 들어옴
        Vector3 topEnd    = screenTop    - Vector3.up   * (topHeight    * peekRatio);
        Vector3 bottomEnd = screenBottom + Vector3.up   * (bottomHeight * peekRatio);

        topCamera.transform.position    = topStart;
        bottomCamera.transform.position = bottomStart;
        topCamera.gameObject.SetActive(true);
        bottomCamera.gameObject.SetActive(true);

        // 슬라이드 인
        await UniTask.WhenAll(
            topCamera.transform.DOMove(topEnd, slideInDuration).SetEase(slideInEase).WithCancellation(ct),
            bottomCamera.transform.DOMove(bottomEnd, slideInDuration).SetEase(slideInEase).WithCancellation(ct)
        );

        // TODO: 파티클 추가 후 여기서 Play
        await UniTask.Delay((int)(holdDuration * 1000), cancellationToken: ct);

        // 슬라이드 아웃
        await UniTask.WhenAll(
            topCamera.transform.DOMove(topStart, slideOutDuration).SetEase(slideOutEase).WithCancellation(ct),
            bottomCamera.transform.DOMove(bottomStart, slideOutDuration).SetEase(slideOutEase).WithCancellation(ct)
        );

        topCamera.gameObject.SetActive(false);
        bottomCamera.gameObject.SetActive(false);
    }
}
