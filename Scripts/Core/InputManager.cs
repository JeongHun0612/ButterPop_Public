using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private Squish _squish;
    private ArmController _armController;

    // 누르기 시작한 지점이 UI가 아니어서 게임플레이가 가져간 제스처인지.
    // 뗄 때는 손가락이 UI 위로 옮겨가 있어도 이 값으로 판단해야 팔이 올라간 채 굳지 않는다.
    private bool _gestureActive;

    private void Update()
    {
        var stageManager = StageManager.Instance;
        Debug.Assert(stageManager != null, "[InputManager] StageManager가 없습니다");

        if (_squish == null)
            _squish = stageManager.Squish;

        if (_armController == null)
            _armController = stageManager.ArmController;

        if (WasPressed())
        {
            // UI를 누른 제스처는 게임플레이가 받지 않는다. 판정은 누른 순간에만 한다.
            _gestureActive = !IsPointerOverUI();

            if (_gestureActive)
                BeginGesture(stageManager.CurrentState);
        }

        // 눌렀다 뗀 짧은 탭도 놓치지 않도록 위와 독립된 분기로 둔다.
        // 뗀 순간뿐 아니라 입력 자체가 사라진 경우(포커스 상실·광고 전환 등)도 여기서 걷힌다.
        if (_gestureActive && !IsPressed())
        {
            _gestureActive = false;

            // 내리는 동작은 상태·위치를 가리지 않는다. 한 번 놓치면 팔이 올라간 채 남는다.
            if (_armController != null)
                _armController.Lower();
        }
    }

    private void BeginGesture(GameState state)
    {
        if (state == GameState.Ready && _squish != null && _squish.CanDrop)
            _squish.Drop();

        if (state != GameState.Result && _armController != null)
            _armController.Raise();
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

    // 뗀 순간(edge)이 아니라 현재 눌려 있는지(level)를 본다. 릴리즈 이벤트를 놓쳐도 스스로 복구된다.
    private bool IsPressed()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return true;
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            return true;
        return false;
    }
}
