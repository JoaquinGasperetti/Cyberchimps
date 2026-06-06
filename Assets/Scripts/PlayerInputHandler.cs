using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerInputHandler : NetworkBehaviour
{
    [Header("UI Móvil")]
    [Tooltip("Joystick del Canvas — se auto-busca si no está asignado")]
    [SerializeField] private Joystick movementJoystick;

    public Vector2 MoveInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool ActionPressedThisFrame { get; private set; }

    private Vector2 keyboardInput;

    // Referencia estática al handler del jugador LOCAL en esta máquina.
    // El Canvas y MobileUIConnector usan esto para redirigir el input
    // sin importar cuántos jugadores haya en escena.
    public static PlayerInputHandler LocalInstance { get; private set; }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            LocalInstance = this;

            // Auto-buscar joystick si no está asignado en el prefab
            if (movementJoystick == null)
                movementJoystick = FindAnyObjectByType<Joystick>();

            // Habilitamos el PlayerInput (Input System) solo para el owner
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
                playerInput.enabled = true;
        }
        else
        {
            // Desactivar el PlayerInput en jugadores remotos para que no
            // capturen input del teclado/gamepad local
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
                playerInput.enabled = false;

            enabled = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && LocalInstance == this)
            LocalInstance = null;
    }

    // --- NEW INPUT SYSTEM callbacks (PC / Gamepad) ---
    public void OnMove(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        keyboardInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        if (context.started)  JumpPressed = true;
        if (context.canceled) JumpPressed = false;
    }

    public void OnAction(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        if (context.performed) TriggerAction();
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
        ActionPressedThisFrame = false;
    }

    // -------------------------------------------------------
    // MÉTODOS PARA LOS BOTONES MÓVILES DEL CANVAS
    // Los botones llaman a MobileUIConnector, que llama a los
    // métodos estáticos de abajo. Así no necesitan referencia al Player.
    // -------------------------------------------------------
    public void MobileJumpDown()  => JumpPressed = true;
    public void MobileJumpUp()    => JumpPressed = false;
    public void TriggerAction()   => ActionPressedThisFrame = true;

    public static void StaticMobileJumpDown()  => LocalInstance?.MobileJumpDown();
    public static void StaticMobileJumpUp()    => LocalInstance?.MobileJumpUp();
    public static void StaticTriggerAction()   => LocalInstance?.TriggerAction();
}
