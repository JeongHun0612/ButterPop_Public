using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private BananaThrowController _bananaController;
    private MonkeyArm _monkeyArm;

    private void Update()
    {
        if (StageManager.Instance == null)
            return;

        if (_bananaController == null)
            _bananaController = StageManager.Instance.BananaController;

        if (_monkeyArm == null)
            _monkeyArm = StageManager.Instance.MonkeyArm;

        if (_bananaController == null)
            return;

        var state = StageManager.Instance.CurrentState;

        if (state == GameState.Ready && WasPressed())
        {
            _bananaController.DropBanana();
        }

        if (state == GameState.Falling || state == GameState.Flying || state == GameState.Settling)
        {
            if (WasPressed() && _monkeyArm != null)
                _monkeyArm.Raise();
            else if (WasReleased() && _monkeyArm != null)
                _monkeyArm.Lower();
        }
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
