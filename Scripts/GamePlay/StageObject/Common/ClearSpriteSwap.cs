using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 클리어 시 스프라이트를 클리어 상태 이미지로 바꿔, 결과 화면 캡처에 바뀐 모습이 찍히게 한다.
public class ClearSpriteSwap : MonoBehaviour, IStageClearEffect
{
    [Header("스프라이트")]
    [Tooltip("스프라이트를 바꿔 그릴 SpriteRenderer. 비어 있으면 같은 오브젝트에서 찾는다")]
    [SerializeField] private SpriteRenderer targetRenderer;
    [Tooltip("클리어 시 표시할 스프라이트")]
    [SerializeField] private Sprite clearSprite;

    [Header("연출")]
    [Tooltip("바뀐 모습을 보여준 뒤 결과 화면으로 넘어가기 전까지 머무는 시간(초)")]
    [Min(0f)]
    [SerializeField] private float holdDuration = 0.4f;

    [Header("사운드")]
    [Tooltip("스프라이트가 바뀔 때 재생할 클립. 비어 있으면 재생하지 않는다")]
    [SerializeField] private AudioClip clearClip;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();

        if (targetRenderer == null || clearSprite == null)
            Debug.LogError("[ClearSpriteSwap] targetRenderer / clearSprite 참조가 비어 있습니다", this);
    }

    public async UniTask PlayClearEffectAsync(CancellationToken token)
    {
        if (targetRenderer == null || clearSprite == null) return;

        targetRenderer.sprite = clearSprite;
        PlayClearSound();

        await UniTask.Delay(TimeSpan.FromSeconds(holdDuration), cancellationToken: token);
    }

    private void PlayClearSound()
    {
        if (clearClip == null) return;

        if (SoundManager.TryGetInstance(out var sound))
            sound.PlaySfx(clearClip);
    }
}
