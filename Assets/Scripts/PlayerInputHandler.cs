using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

/// <summary>
/// Captura y expone el input del jugador local.
/// Solo está activo en el owner — se desactiva en jugadores remotos.
///
/// FIX aplicado: agrega ConsumeJump() que PlayerController llama al saltar,
/// evitando que JumpPressed quede true más de un frame y cause saltos dobles.
/// </summary>
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
    public static PlayerInputHandler LocalInstance { get; private set; }

    // =========================================================
    // NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            LocalInstance = this;

            if (movementJoystick == null)
                movementJoystick = FindAnyObjectByType<Joystick>();

            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
                playerInput.enabled = true;
        }
        else
        {
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

    // =========================================================
    // INPUT SYSTEM CALLBACKS (teclado / gamepad)
    // =========================================================

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
        if (context.performed) TriggerAction();
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        // El joystick móvil tiene prioridad sobre el teclado
        if (movementJoystick != null && movementJoystick.Direction.sqrMagnitude > 0.01f)
            MoveInput = movementJoystick.Direction;
        else
            MoveInput = keyboardInput;
    }

    private void LateUpdate()
    {
        ActionPressedThisFrame = false;
    }

    // =========================================================
    // API — llamada por PlayerController y botones del Canvas
    // =========================================================

    /// <summary>
    /// Consume el flag de salto. Llamado por PlayerController justo
    /// después de aplicar la fuerza, para evitar saltos dobles.
    /// </summary>
    public void ConsumeJump() => JumpPressed = false;

    // Botones móviles del Canvas
    public void MobileJumpDown() => JumpPressed = true;
    public void MobileJumpUp() => JumpPressed = false;
    public void TriggerAction() => ActionPressedThisFrame = true;

    // Métodos estáticos para MobileUIConnector (no necesita referencia directa)
    public static void StaticMobileJumpDown() => LocalInstance?.MobileJumpDown();
    public static void StaticMobileJumpUp() => LocalInstance?.MobileJumpUp();
    public static void StaticTriggerAction() => LocalInstance?.TriggerAction();
}