using UnityEngine;

// 캡차 체크박스. 평소엔 로딩 애니메이션을 반복하다가 스퀴시가 부딪히면 체크 스프라이트로 굳고,
// 스퀴시가 영역을 벗어나면 다시 로딩으로 돌아간다.
// 체크는 실제 충돌로, 해제는 영역 이탈로 판정한다. 안착 중 잠깐 튕기는 것까지 실패로 보지 않기 위해서다.
public class Captcha : MonoBehaviour, ISquishHittable
{
    [Header("스프라이트")]
    [Tooltip("로딩 프레임과 체크 스프라이트를 바꿔 그릴 SpriteRenderer")]
    [SerializeField] private SpriteRenderer boxRenderer;
    [Tooltip("로딩 프레임을 재생할 애니메이터. 재생 시점은 이 스크립트가 정하므로 Play On Enable은 꺼둔다")]
    [SerializeField] private SpriteAnimator loadingAnimator;
    [Tooltip("체크된 상태로 표시할 스프라이트")]
    [SerializeField] private Sprite checkedSprite;

    [Header("영역")]
    [Tooltip("스퀴시가 벗어나면 로딩으로 되돌릴 트리거 존. 안착 중 튕겨도 유지되도록 안착면보다 넉넉하게 잡는다")]
    [SerializeField] private SquishTriggerZone exitZone;

    private bool _isChecked;

    // 컴포넌트 간 Awake 순서가 보장되지 않아, 애니메이터가 렌더러를 캐싱한 뒤인 Start에서 켠다.
    private void Start() => ShowLoading();

    private void OnEnable()
    {
        if (exitZone != null) exitZone.OnSquishExited += HandleSquishExited;

        if (!StageManager.TryGetInstance(out var stageManager)) return;

        stageManager.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (exitZone != null) exitZone.OnSquishExited -= HandleSquishExited;

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
