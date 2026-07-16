using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerInputHandler : NetworkBehaviour
{
    [Header("UI Móvil")]
    [SerializeField] private Joystick movementJoystick;

    public Vector2 MoveInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool ActionPressedThisFrame { get; private set; }

    public bool ActionReleased { get; private set; }

    private Vector2 keyboardInput;
    private bool actionHeld;

    public static PlayerInputHandler LocalInstance { get; private set; }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            LocalInstance = this;

            if (movementJoystick == null)
                movementJoystick = FindAnyObjectByType<Joystick>();

            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null) playerInput.enabled = true;
        }
        else
        {
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null) playerInput.enabled = false;
            enabled = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && LocalInstance == this)
            LocalInstance = null;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        keyboardInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        if (context.started) JumpPressed = true;
        if (context.canceled) JumpPressed = false;
    }

    public void OnAction(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        if (context.started)
        {
            ActionPressedThisFrame = true;
            actionHeld = true;
        }

        if (context.canceled)
        {
            actionHeld = false;
            ActionReleased = true;
        }
    }

    private void Update()
    {
        if (movementJoystick != null && movementJoystick.Direction.sqrMagnitude > 0.01f)
            MoveInput = movementJoystick.Direction;
        else
            MoveInput = keyboardInput;
    }

    private void LateUpdate()
    {
        // flags que duran un solo frame
        ActionPressedThisFrame = false;
        ActionReleased = false;
    }

    public void ConsumeJump() => JumpPressed = false;

    // botones del canvas movil
    public void MobileJumpDown() => JumpPressed = true;
    public void MobileJumpUp() => JumpPressed = false;
    public void TriggerAction() => ActionPressedThisFrame = true;
    public void ReleaseAction() { ActionReleased = true; actionHeld = false; }

    public static void StaticMobileJumpDown() => LocalInstance?.MobileJumpDown();
    public static void StaticMobileJumpUp() => LocalInstance?.MobileJumpUp();
    public static void StaticTriggerAction() => LocalInstance?.TriggerAction();
    public static void StaticReleaseAction() => LocalInstance?.ReleaseAction();
}