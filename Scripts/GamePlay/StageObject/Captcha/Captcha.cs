using UnityEngine;

// 캡차 체크박스. 평소엔 로딩 애니메이션을 반복하다가 스퀴시가 부딪히면 체크 스프라이트로 굳고,
// 실제 접촉이 유예 시간 이상 끊기면 다시 로딩으로 돌아간다.
public class Captcha : MonoBehaviour, ISquishHittable
{
    [Header("스프라이트")]
    [Tooltip("로딩 프레임과 체크 스프라이트를 바꿔 그릴 SpriteRenderer")]
    [SerializeField] private SpriteRenderer boxRenderer;
    [Tooltip("로딩 프레임을 재생할 애니메이터. 재생 시점은 이 스크립트가 정하므로 Play On Enable은 꺼둔다")]
    [SerializeField] private SpriteAnimator loadingAnimator;
    [Tooltip("체크된 상태로 표시할 스프라이트")]
    [SerializeField] private Sprite checkedSprite;

    [Header("접촉")]
    [Tooltip("스퀴시의 실제 접촉과 이탈 유예를 관리하는 센서. 비어 있으면 같은 오브젝트에서 찾는다")]
    [SerializeField] private SquishContactSensor contactSensor;

    private bool _isChecked;

    private void Awake()
    {
        if (contactSensor == null)
            contactSensor = GetComponent<SquishContactSensor>();

        if (contactSensor == null)
            Debug.LogError("[Captcha] SquishContactSensor를 찾지 못했습니다", this);
    }

    // 컴포넌트 간 Awake 순서가 보장되지 않아, 애니메이터가 렌더러를 캐싱한 뒤인 Start에서 켠다.
    private void Start() => ShowLoading();

    private void OnEnable()
    {
        if (contactSensor != null) contactSensor.OnSquishExited += HandleSquishExited;

        if (!StageManager.TryGetInstance(out var stageManager)) return;

        stageManager.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (contactSensor != null) contactSensor.OnSquishExited -= HandleSquishExited;

        if (!StageManager.TryGetInstance(out var stageManager)) return;

        stageManager.OnStateChanged -= HandleStateChanged;
    }

    public void OnSquishHit(Collision2D collision) => Check();

    public void Check()
    {
        if (_isChecked) return;

        _isChecked = true;

        // 먼저 멈추지 않으면 다음 로딩 프레임이 체크 스프라이트를 덮어쓴다.
        if (loadingAnimator != null) loadingAnimator.Stop();
        if (boxRenderer != null && checkedSprite != null) boxRenderer.sprite = checkedSprite;
    }

    public void ShowLoading()
    {
        _isChecked = false;
        if (loadingAnimator != null) loadingAnimator.Play();
    }

    // 체크되지 않았다면 스퀴시가 그냥 지나간 것이라, 재생 중인 로딩을 프레임 0으로 되감지 않는다.
    private void HandleSquishExited()
    {
        if (!_isChecked) return;

        ShowLoading();
    }

    // Ready 진입 = 스테이지 시작 또는 실패 후 재도전.
    private void HandleStateChanged(GameState state)
    {
        if (state != GameState.Ready) return;

        ShowLoading();
    }
}
