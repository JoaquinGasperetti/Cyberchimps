using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [Header("UI Móvil")]
    [Tooltip("Joystick del Canvas")]
    [SerializeField] private Joystick movementJoystick;

    public Vector2 MoveInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool ActionPressedThisFrame { get; private set; }

    private Vector2 keyboardInput;

    // --- NEW INPUT SYSTEM (bombardeenlo) (PC / Gamepad) ---
    public void OnMove(InputAction.CallbackContext context)
    {
        keyboardInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started) JumpPressed = true;
        if (context.canceled) JumpPressed = false;
    }

    public void OnAction(InputAction.CallbackContext context)
    {
        if (context.performed) TriggerAction();
    }

    // --- LÓGICA HÍBRIDA ---
    private void Update()
    {
        // Prioriza el Joystick Móvil. Si no se está tocando, usa el teclado 🤓 ☝
        if (movementJoystick != null && movementJoystick.Direction.sqrMagnitude > 0.01f)
        {
            MoveInput = movementJoystick.Direction;
        }
        else
        {
            MoveInput = keyboardInput;
        }
    }

    // --- MÉTODOS PARA LOS BOTONES MÓVILES ---
    public void MobileJumpDown() => JumpPressed = true;
    public void MobileJumpUp() => JumpPressed = false;

    public void TriggerAction() => ActionPressedThisFrame = true;

    private void LateUpdate()
    {
        // Resetea el botón de acción al final del frame para evitar interacciones dobles 🤓 ☝
        ActionPressedThisFrame = false;
    }
}