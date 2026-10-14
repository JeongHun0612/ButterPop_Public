using UnityEngine;

// 불씨 기믹: 스퀴시가 불꽃 안에 있는 동안 점점 타고, 벗어나면 서서히 원래 색으로 식는다.
// 뼈 단위 트리거 콜백을 모으는 일은 자식 SquishTriggerSensor가 맡는다.
// 연출 전용이라 다 타도 별도 판정은 없다. 스테이지 리셋 시 원복은 Squish.RespawnAt이 담당.
public class Fire : MonoBehaviour, IStageContextReceiver
{
    [Header("감지")]
    [Tooltip("불꽃 영역. 비우면 Awake에서 자식에서 찾는다")]
    [SerializeField] private SquishTriggerSensor zone;

    [Header("타는 연출")]
    [Tooltip("불 안에서 완전히 타기까지 걸리는 시간(초)")]
    [Min(0.01f)]
    [SerializeField] private float burnDuration = 2f;
    [Tooltip("불에서 벗어난 뒤 원래 색으로 완전히 식기까지 걸리는 시간(초)")]
    [Min(0.01f)]
    [SerializeField] private float coolDuration = 1.5f;

    [Header("사운드")]
    [Tooltip("타기 시작할 때 한 번 재생할 클립")]
    [SerializeField] private AudioClip burnClip;

    private SquishMesh _mesh;
    private float _burnProgress;

    private void Awake()
    {
        if (zone == null)
            zone = GetComponentInChildren<SquishTriggerSensor>(true);

        if (zone == null)
            Debug.LogError("[Fire] SquishTriggerSensor를 찾지 못했습니다.", this);
    }

    private void OnEnable()
    {
        if (zone != null) zone.OnSquishEntered += OnSquishEntered;
    }

    private void OnDisable()
    {
        if (zone != null) zone.OnSquishEntered -= OnSquishEntered;

        _burnProgress = 0f;
    }

    // 불 안이면 태우고 밖이면 식힌다. 다 식은 뒤에는 아무것도 칠하지 않는다.
    private void Update()
    {
        if (_mesh == null || zone == null) return;

        bool inside = zone.HasSquish;
        if (!inside && _burnProgress <= 0f) return;

        float delta = inside
            ? Time.deltaTime / burnDuration
            : -Time.deltaTime / coolDuration;

        _burnProgress = Mathf.Clamp01(_burnProgress + delta);

        // 정점 컬러를 흰색→검정으로 낮춰 스퀴시 전체를 균일하게 태운다.
        _mesh.SetTint(Color.Lerp(Color.white, Color.black, _burnProgress));
    }

    public void ReceiveStageContext(StageContext context)
    {
        _mesh = context?.Squish != null ? context.Squish.Mesh : null;
        _burnProgress = 0f;
    }

    private void PlayBurnSound()
    {
        if (burnClip == null) return;

        if (SoundManager.TryGetInstance(out var sound))
            sound.PlaySfx(burnClip);
    }

    // 첫 뼈가 닿았을 때만 1회 울린다(뼈마다 울려 소리가 겹치던 것을 센서가 막아준다).
    private void OnSquishEntered() => PlayBurnSound();
}
