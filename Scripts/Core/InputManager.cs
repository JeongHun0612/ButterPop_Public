using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private Squish _squish;
    private ArmController _armController;

    private void Update()
    {
        var stageManager = StageManager.Instance;
        Debug.Assert(stageManager != null, "[InputManager] StageManager가 없습니다");
        if (IsPointerOverUI()) return;

        if (_squish == null)
            _squish = stageManager.Squish;

        if (_armController == null)
            _armController = stageManager.ArmController;

        if (_squish == null) return;

        var state = stageManager.CurrentState;

        if (state == GameState.Ready && _squish.CanDrop && WasPressed())
            _squish.Drop();

        if (state != GameState.Result)
        {
            if (WasPressed() && _armController != null)
                _armController.Raise();
            else if (WasReleased() && _armController != null)
                _armController.Lower();
        }
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return EventSystem.current.IsPointerOverGameObject(Touchscreen.current.primaryTouch.touchId.ReadValue());

        return EventSystem.current.IsPointerOverGameObject();
    }

    private bool WasPressed()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return true;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;
        return false;
    }

    private bool WasReleased()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
            return true;
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            return true;
        return false;
    }
}
