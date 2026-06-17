using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BananaThrowController : MonoBehaviour
{
    [Header("발사")]
    [SerializeField] private float maxDragDistance = 3.0f;
    [SerializeField] private float minLaunchPower = 8.0f;
    [SerializeField] private float maxLaunchPower = 12.0f;
    [SerializeField] private float torquePower = 2.0f;

    [Header("취소")]
    [Tooltip("시작점 이 거리 이내로 돌아오면 발사 취소")]
    [SerializeField] private float cancelDistance = 0.3f;

    [Header("물리")]
    [SerializeField] private float gravityScale = 1.5f;
    [SerializeField] private float linearDrag = 0.1f;
    [SerializeField] private float angularDrag = 0.2f;
    [SerializeField] private float mass = 1.0f;

    [Header("시각 효과")]
    [SerializeField] private TrajectoryRenderer trajectoryRenderer;
    [SerializeField] private OriginMarker originMarker;

    private Rigidbody2D _rb;
    private Collider2D _collider;
    private Camera _mainCamera;

    private Vector2 _restPosition;
    private bool _isDragging;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _mainCamera = Camera.main;

        _restPosition = transform.position;
        SetKinematic();
    }

    private void Update()
    {
        if (StageManager.Instance == null) return;
        if (StageManager.Instance.CurrentState != GameState.Ready &&
            StageManager.Instance.CurrentState != GameState.Dragging)
            return;

        Vector2 pointerScreenPos = GetPointerScreenPosition();

        if (WasPointerPressed() && !_isDragging)
        {
            TryStartDrag(pointerScreenPos);
        }
        else if (IsPointerHeld() && _isDragging)
        {
            UpdateDrag(pointerScreenPos);
        }
        else if (WasPointerReleased() && _isDragging)
        {
            ReleaseDrag();
        }
    }

    private bool WasPointerPressed()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return true;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;
        return false;
    }

    private bool IsPointerHeld()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return true;
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            return true;
        return false;
    }

    private bool WasPointerReleased()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
            return true;
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            return true;
        return false;
    }

    private Vector2 GetPointerScreenPosition()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return Touchscreen.current.primaryTouch.position.ReadValue();
        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();
        return Vector2.zero;
    }

    private void TryStartDrag(Vector2 screenPos)
    {
        Vector2 worldPos = ScreenToWorld(screenPos);
        if (!_collider.OverlapPoint(worldPos)) return;

        _isDragging = true;
        _restPosition = transform.position;

        StageManager.Instance.SetState(GameState.Dragging);
    }

    private void UpdateDrag(Vector2 screenPos)
    {
        Vector2 worldPos = ScreenToWorld(screenPos);
        Vector2 dragVector = worldPos - _restPosition;

        if (dragVector.magnitude > maxDragDistance)
            dragVector = dragVector.normalized * maxDragDistance;

        transform.position = (Vector3)(_restPosition + dragVector);

        bool isNearOrigin = dragVector.magnitude <= cancelDistance;

        // 시작점 근처면 궤적 숨김, 아니면 방향·세기 표시
        if (isNearOrigin)
        {
            if (trajectoryRenderer != null) trajectoryRenderer.Hide();
        }
        else
        {
            Vector2 launchVelocity = CalculateLaunchVelocity(dragVector);
            float dragRatio = Mathf.Clamp01(dragVector.magnitude / maxDragDistance);
            if (trajectoryRenderer != null)
                trajectoryRenderer.ShowTrajectory(_restPosition, launchVelocity, dragRatio);
        }

        if (originMarker != null)
            originMarker.Show(_restPosition, (Vector2)transform.position, cancelDistance);
    }

    private void ReleaseDrag()
    {
        Vector2 dragVector = (Vector2)transform.position - _restPosition;

        // 시작점 근처에서 놓으면 발사하지 않고 준비 상태로 복귀
        if (dragVector.magnitude <= cancelDistance)
        {
            CancelDrag();
            return;
        }

        Vector2 launchVelocity = CalculateLaunchVelocity(dragVector);

        transform.position = (Vector3)_restPosition;
        SetDynamic();

        _rb.linearVelocity = launchVelocity;

        float torqueSign = launchVelocity.x >= 0 ? -1f : 1f;
        _rb.AddTorque(torquePower * torqueSign, ForceMode2D.Impulse);

        HideVisuals();

        _isDragging = false;

        StageManager.Instance.SetState(GameState.Flying);
    }

    private void CancelDrag()
    {
        transform.position = (Vector3)_restPosition;
        HideVisuals();

        _isDragging = false;

        if (StageManager.Instance.CurrentState == GameState.Dragging)
            StageManager.Instance.SetState(GameState.Ready);
    }

    private Vector2 CalculateLaunchVelocity(Vector2 dragVector)
    {
        float dragMag = dragVector.magnitude;
        float t = Mathf.Clamp01(dragMag / maxDragDistance);
        float power = Mathf.Lerp(minLaunchPower, maxLaunchPower, t);
        return -dragVector.normalized * power;
    }

    private void SetKinematic()
    {
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
    }

    private void SetDynamic()
    {
        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.gravityScale = gravityScale;
        _rb.linearDamping = linearDrag;
        _rb.angularDamping = angularDrag;
        _rb.mass = mass;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void HideVisuals()
    {
        if (trajectoryRenderer != null) trajectoryRenderer.Hide();
        if (originMarker != null) originMarker.Hide();
    }

    private Vector2 ScreenToWorld(Vector2 screenPos)
    {
        Vector3 worldPos = _mainCamera.ScreenToWorldPoint(new Vector3(
            screenPos.x, screenPos.y, -_mainCamera.transform.position.z));
        return worldPos;
    }

    public void ResetBanana(Vector2 spawnPosition)
    {
        SetKinematic();
        transform.position = (Vector3)spawnPosition;
        transform.rotation = Quaternion.identity;
        _restPosition = spawnPosition;

        StageManager.Instance.SetState(GameState.Ready);
    }
}
