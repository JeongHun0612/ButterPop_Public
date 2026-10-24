using UnityEngine;

// 스퀴시가 닿아 있는 동안 접촉 스프라이트로 바꿔 그리고, 실제 접촉이 유예 시간 이상 끊기면 원래 스프라이트로 돌아간다.
public class SquishSpriteSwap : MonoBehaviour, ISquishHittable
{
    [Header("스프라이트")]
    [Tooltip("스프라이트를 바꿔 그릴 SpriteRenderer. 비어 있으면 같은 오브젝트에서 찾는다. 처음 들고 있는 스프라이트가 기본 상태가 된다")]
    [SerializeField] private SpriteRenderer targetRenderer;
    [Tooltip("스퀴시가 닿아 있는 동안 표시할 스프라이트")]
    [SerializeField] private Sprite contactSprite;

    [Header("접촉")]
    [Tooltip("스퀴시의 실제 접촉과 이탈 유예를 관리하는 센서. 비어 있으면 같은 오브젝트에서 찾는다")]
    [SerializeField] private SquishContactSensor contactSensor;

    private Sprite _defaultSprite;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();
        if (contactSensor == null)
            contactSensor = GetComponent<SquishContactSensor>();

        if (targetRenderer == null || contactSensor == null || contactSprite == null)
        {
            Debug.LogError("[SquishSpriteSwap] targetRenderer / contactSensor / contactSprite 참조가 비어 있습니다", this);
            enabled = false;
            return;
        }

        _defaultSprite = targetRenderer.sprite;
    }

    private void OnEnable()
    {
        if (contactSensor == null) return;

        contactSensor.OnSquishExited += HandleSquishExited;

        if (contactSensor.HasSquish)
            ShowContact();

        if (!StageManager.TryGetInstance(out var stageManager)) return;

        stageManager.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (contactSensor != null) contactSensor.OnSquishExited -= HandleSquishExited;

        ShowDefault();

        if (!StageManager.TryGetInstance(out var stageManager)) return;

        stageManager.OnStateChanged -= HandleStateChanged;
    }

    // 초기화에 실패해 비활성화된 경우에도 센서는 콜백을 보내므로 enabled로 막는다.
    public void OnSquishHit(Collision2D collision)
    {
        if (enabled) ShowContact();
    }

    public void ShowContact() => targetRenderer.sprite = contactSprite;

    public void ShowDefault()
    {
        if (targetRenderer != null && _defaultSprite != null) targetRenderer.sprite = _defaultSprite;
    }

    // 클리어 시 스퀴시가 Kinematic으로 굳으면 정적 콜라이더와의 접촉이 끊겨 이탈이 오므로, Result에서는 접촉 스프라이트를 유지한다.
    private void HandleSquishExited()
    {
        if (StageManager.TryGetInstance(out var stageManager) && stageManager.CurrentState == GameState.Result) return;

        ShowDefault();
    }

    // Ready 진입 = 스테이지 시작 또는 실패 후 재도전.
    private void HandleStateChanged(GameState state)
    {
        if (state != GameState.Ready) return;

        ShowDefault();
    }
}
