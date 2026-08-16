using UnityEngine;
using UnityEngine.InputSystem;

// 버터 스퀴시 테스트 조작기.
// 우클릭 드래그: 잡아서 던지기 / R: 리셋 / Space: 위로 팝(회전 포함)
public class ButterTester : MonoBehaviour
{
    [Header("리셋")]
    [SerializeField] private Vector2 startPosition = new(0f, 3f);
    [SerializeField] private float startRotation = 0f;

    [Header("팝 (Space)")]
    [SerializeField] private float popForce = 6f;
    [SerializeField] private float popTorque = 8f;

    [Header("던지기 (우클릭 드래그)")]
    [SerializeField] private float throwSpeedLimit = 20f;

    [Header("원숭이 팔 (선택)")]
    [Tooltip("배치되어 있으면 좌클릭/터치로 팔을 올려쳐 버터를 날린다. (InputManager의 Raise/Lower와 동일)")]
    [SerializeField] private MonkeyArm monkeyArm;

    private ButterBody _body;
    private ButterSquishMesh _mesh;
    private Camera _cam;

    private bool _dragging;
    private Vector2 _lastMouseWorld;
    private Vector2 _dragVelocity;

    private void Awake()
    {
        _body = GetComponent<ButterBody>();
        _mesh = GetComponent<ButterSquishMesh>();
        _cam = Camera.main;
    }

    private void Start() => ResetButter();

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.rKey.wasPressedThisFrame) ResetButter();
            if (keyboard.spaceKey.wasPressedThisFrame) Pop();
        }

        HandleArm();
        HandleDrag();
    }

    private void ResetButter()
    {
        _body.ResetLayout(startPosition, startRotation);
        if (_mesh != null) _mesh.ResetShape();
        if (monkeyArm != null) monkeyArm.ResetArm();
        _dragging = false;
    }

    // 좌클릭/터치 = 팔 올려치기 (InputManager의 원숭이 제어와 동일한 호출)
    private void HandleArm()
    {
        if (monkeyArm == null) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame) monkeyArm.Raise();
        else if (mouse.leftButton.wasReleasedThisFrame) monkeyArm.Lower();
    }

    private void Pop() => _body.AddImpulseAll(Vector2.up * popForce, popTorque);

    private void HandleDrag()
    {
        var mouse = Mouse.current;
        if (mouse == null || _cam == null) return;

        Vector2 world = _cam.ScreenToWorldPoint(mouse.position.ReadValue());

        if (mouse.rightButton.wasPressedThisFrame)
        {
            var hit = Physics2D.OverlapPoint(world);
            if (hit != null && IsOwnSegment(hit))
            {
                _dragging = true;
                _body.SetKinematic(true);
                _lastMouseWorld = world;
            }
        }

        if (!_dragging) return;

        _dragVelocity = (world - _lastMouseWorld) / Mathf.Max(Time.deltaTime, 1e-4f);
        _body.MoveAll(world - _lastMouseWorld);
        _lastMouseWorld = world;

        if (mouse.rightButton.wasReleasedThisFrame)
        {
            _dragging = false;
            _body.SetKinematic(false);
            _body.SetLinearVelocityAll(Vector2.ClampMagnitude(_dragVelocity, throwSpeedLimit));
        }
    }

    private bool IsOwnSegment(Collider2D hit)
    {
        var rb = hit.attachedRigidbody;
        if (rb == null) return false;

        var segments = _body.Segments;
        for (int i = 0; i < segments.Count; i++)
            if (segments[i] == rb) return true;
        return false;
    }
}
