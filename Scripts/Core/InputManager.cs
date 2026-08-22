using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private Squish _squish;
    private MonkeyArm _monkeyArm;

    private void Update()
    {
        var stageManager = StageManager.Instance;
        Debug.Assert(stageManager != null, "[InputManager] StageManager가 없습니다");
        if (IsPointerOverUI()) return;

        if (_squish == null)
            _squish = stageManager.Squish;

        if (_monkeyArm == null)
            _monkeyArm = stageManager.MonkeyArm;

        if (_squish == null) return;

        var state = stageManager.CurrentState;

        if (state == GameState.Ready && _squish.CanDrop && WasPressed())
        {
            _squish.Drop();
            return;
        }

        if (state != GameState.Result && (state != GameState.Ready || _squish.IsInteractable))
        {
            if (WasPressed() && _monkeyArm != null)
                _monkeyArm.Raise();
            else if (WasReleased() && _monkeyArm != null)
                _monkeyArm.Lower();
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
