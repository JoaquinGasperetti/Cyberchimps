using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool ActionPressedThisFrame { get; private set; }

    public void OnMove(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started)
            JumpPressed = true;

        if (context.canceled)
            JumpPressed = false;
    }

    public void OnAction(InputAction.CallbackContext context)
    {
        if (context.performed)
            ActionPressedThisFrame = true;
    }

    private void LateUpdate()
    {
        ActionPressedThisFrame = false;
    }
}