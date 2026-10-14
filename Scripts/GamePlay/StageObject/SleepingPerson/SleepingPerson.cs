using UnityEngine;

// 스퀴시가 부딪히면 깨어난 스프라이트로 바뀌는 스테이지 오브젝트.
// 한 번 깨어나면 그 상태를 유지하고, 스퀴시가 떨어져 재도전(Ready 재진입)할 때만 다시 잠든다.
public class SleepingPerson : MonoBehaviour, ISquishHittable
{
    [Header("스프라이트")]
    [Tooltip("잠든 상태와 깨어난 상태를 바꿔 그릴 SpriteRenderer")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [Tooltip("잠든 상태(눈 감음) 스프라이트")]
    [SerializeField] private Sprite asleepSprite;
    [Tooltip("깨어난 상태(눈 뜸) 스프라이트")]
    [SerializeField] private Sprite awakeSprite;

    private bool _isAwake;

    private void Awake() => Sleep();

    private void OnEnable()
    {
        if (!StageManager.TryGetInstance(out var stageManager)) return;

        stageManager.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (!StageManager.TryGetInstance(out var stageManager)) return;

        stageManager.OnStateChanged -= HandleStateChanged;
    }

    public void OnSquishHit(Collision2D collision) => WakeUp();

    public void WakeUp()
    {
        if (_isAwake) return;

        _isAwake = true;
        ApplyState();
    }

    // 잠든 상태로 되돌린다.
    public void Sleep()
    {
        _isAwake = false;
        ApplyState();
    }

    private void ApplyState()
    {
        if (bodyRenderer != null)
        {
            var sprite = _isAwake ? awakeSprite : asleepSprite;
            if (sprite != null) bodyRenderer.sprite = sprite;
        }
    }

    // Ready 진입 = 스테이지 시작 또는 실패 후 재도전. 이때만 다시 잠든다.
    private void HandleStateChanged(GameState state)
    {
        if (state != GameState.Ready) return;

        Sleep();
    }
}
