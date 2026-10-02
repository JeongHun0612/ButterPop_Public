using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

// 클리어 시 스테이지 중앙에 도화지(배경 이미지)를 고정 크기로 놓고, 위에서부터 지그재그 획으로 칠해 나가듯 드러낸다.
// 배경 렌더러에는 _Progress를 가진 ButterPop/PaintReveal 머티리얼을 쓴다.
// 배경은 프리팹에서 자식으로 편집하되 런타임엔 이 오브젝트에서 떼어낸다.
// 자식으로 남으면 안착 판정이 스테이지 오브젝트의 렌더러 바운드로 결과 캡처 영역을 잡을 때 도화지 전체로 커진다.
public class ClearBackgroundReveal : MonoBehaviour, IStageContextReceiver, IStageClearEffect
{
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int SpriteSizeId = Shader.PropertyToID("_SpriteSize");

    [Header("배경")]
    [Tooltip("클리어 시 나타날 배경. 스퀴시·스테이지 오브젝트보다 뒤에 그려지도록 Sorting Order를 낮게 둔다")]
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [Tooltip("배경의 월드 스케일. 이미지 폭이 기준 화면 폭(카메라 세로 크기 × 2 × 9/19.5)보다 작아야 모든 기기에서 잘리지 않는다")]
    [Min(0.01f)]
    [SerializeField] private float backgroundScale = 0.5f;

    [Header("연출")]
    [Tooltip("맨 위에서 맨 아래까지 다 칠하는 데 걸리는 시간(초). 획 모양은 머티리얼에서 조절한다")]
    [Min(0f)]
    [SerializeField] private float paintDuration = 1.6f;
    [Tooltip("다 칠한 뒤 결과 화면으로 넘어가기 전까지 머무는 시간(초)")]
    [Min(0f)]
    [SerializeField] private float holdDuration = 0.4f;

    private MaterialPropertyBlock _propertyBlock;
    private float _progress;

    private void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();

        if (backgroundRenderer == null)
        {
            Debug.LogError("[ClearBackgroundReveal] backgroundRenderer가 비어 있습니다.", this);
            return;
        }

        var material = backgroundRenderer.sharedMaterial;
        if (material == null || !material.HasProperty(ProgressId))
            Debug.LogError("[ClearBackgroundReveal] 배경 머티리얼에 _Progress가 없어 칠하기 연출이 보이지 않습니다.", this);

        backgroundRenderer.transform.SetParent(transform.parent, true);
        backgroundRenderer.enabled = false;
    }

    // 떼어낸 배경은 이 오브젝트와 함께 파괴되지 않으므로 직접 정리한다.
    private void OnDestroy()
    {
        if (backgroundRenderer != null)
            Destroy(backgroundRenderer.gameObject);
    }

    public void ReceiveStageContext(StageContext context)
    {
        if (backgroundRenderer == null || context?.Data == null) return;

        PlaceAtStageCenter(context.Data);
    }

    public async UniTask PlayClearEffectAsync(CancellationToken token)
    {
        if (backgroundRenderer == null) return;

        ApplySpriteSize();
        SetProgress(0f);
        backgroundRenderer.enabled = true;

        await DOTween.To(() => _progress, SetProgress, 1f, paintDuration)
            .SetEase(Ease.Linear)
            .SetLink(backgroundRenderer.gameObject)
            .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
        await UniTask.Delay((int)(holdDuration * 1000), cancellationToken: token);
    }

    // 이 오브젝트의 배치와 무관하게 스테이지 중앙에 고정 크기로 놓는다.
    private void PlaceAtStageCenter(StageDataSO data)
    {
        var target = backgroundRenderer.transform;
        target.SetPositionAndRotation(
            new Vector3(data.cameraCenter.x, data.cameraCenter.y, target.position.z),
            Quaternion.identity);

        Vector3 parentScale = target.parent != null ? target.parent.lossyScale : Vector3.one;
        target.localScale = new Vector3(backgroundScale / parentScale.x, backgroundScale / parentScale.y, 1f);
    }

    // 획 비율과 크레용 결 크기가 실제 스프라이트 픽셀 크기를 따르도록 넘긴다.
    private void ApplySpriteSize()
    {
        var sprite = backgroundRenderer.sprite;
        if (sprite == null) return;

        backgroundRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetVector(SpriteSizeId, sprite.rect.size);
        backgroundRenderer.SetPropertyBlock(_propertyBlock);
    }

    // 렌더러에 이미 걸린 값(스프라이트 텍스처 등)을 유지하려고 기존 블록을 받아 진행도만 덮어쓴다.
    private void SetProgress(float progress)
    {
        _progress = progress;
        backgroundRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(ProgressId, progress);
        backgroundRenderer.SetPropertyBlock(_propertyBlock);
    }
}
