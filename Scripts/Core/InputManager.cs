using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [SerializeField] private MonkeyArm monkeyArm;

    private BananaThrowController _bananaController;

    private void Update()
    {
        if (StageManager.Instance == null) 
            return;

        if (_bananaController == null)
            _bananaController = StageManager.Instance.BananaController;

        if (_bananaController == null) 
            return;

        var state = StageManager.Instance.CurrentState;

        if (state == GameState.Ready && WasPressed())
        {
            _bananaController.DropBanana();
        }
        else if (state == GameState.Falling)
        {
            if (WasPressed() && monkeyArm != null)
                monkeyArm.Raise();
            else if (WasReleased() && monkeyArm != null)
                monkeyArm.Lower();
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
