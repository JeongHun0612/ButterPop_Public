using UnityEngine;
using UnityEngine.InputSystem;

// 스퀴시 오브젝트(일반형) 테스트 조작기.
// 좌클릭: 원숭이 팔 올려치기 / 우클릭 드래그: 잡아서 던지기 / R: 리셋 / Space: 위로 팝
public class SquishTester : MonoBehaviour
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
    [SerializeField] private MonkeyArm monkeyArm;

    private SquishBody _body;
    private SquishMesh _mesh;
    private Camera _cam;

    private bool _dragging;
    private Vector2 _lastMouseWorld;
    private Vector2 _dragVelocity;

    private void Awake()
    {
        _body = GetComponent<SquishBody>();
        _mesh = GetComponent<SquishMesh>();
        _cam = Camera.main;
    }

    private void Start() => ResetSquish();

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.rKey.wasPressedThisFrame) ResetSquish();
            if (keyboard.spaceKey.wasPressedThisFrame) Pop();
        }

        HandleArm();
        HandleDrag();
    }

    private void ResetSquish()
    {
        _body.ResetLayout(startPosition, startRotation);
        if (_mesh != null) _mesh.ResetShape();
        if (monkeyArm != null) monkeyArm.ResetArm();
        _dragging = false;
    }

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
            if (hit != null && IsOwnBone(hit))
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

    private bool IsOwnBone(Collider2D hit)
    {
        var rb = hit.attachedRigidbody;
        if (rb == null) return false;

        var bones = _body.Bones;
        for (int i = 0; i < bones.Count; i++)
            if (bones[i] == rb) return true;
        return false;
    }
}
