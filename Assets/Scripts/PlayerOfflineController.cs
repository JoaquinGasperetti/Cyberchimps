using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador offline completo — movimiento, salto, interacción y animaciones.
/// Funciona 100% sin red, ideal para testear niveles en el editor.
///
/// SETUP del prefab PlayerOffline:
///   1. Duplicá el prefab Player → renombralo PlayerOffline
///   2. Quitá: NetworkObject, ClientNetworkTransform, NetworkAnimator,
///             PlayerInputHandler, PlayerInteractor, PlayerCameraTarget,
///             PlayerAnimatorController
///   3. Agregá este script (PlayerOfflineController)
///   4. Asigná el HoldPoint (Transform hijo) en el Inspector
///   5. En la CinemachineCamera asigná el PlayerOffline como Tracking Target
///   6. En el Canvas, el MobileUIConnectorOffline redirige los botones a este script
///
/// CONTROLES TECLADO: WASD → mover | Space → saltar | E → interactuar
/// CONTROLES CANVAS:  Joystick → mover | Botón Jump → saltar | Botón Action → interactuar
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerOfflineController : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed           = 5f;
    [SerializeField] private float jumpForce           = 7f;
    [SerializeField] private float groundCheckDistance = 0.25f;
    [SerializeField] private LayerMask groundMask;

    [Header("Interacción")]
    [SerializeField] private float interactionRadius = 2f;
    [SerializeField] private LayerMask interactableMask;
    [SerializeField] private Transform holdPoint;

    [Header("UI Móvil (opcional)")]
    [Tooltip("Joystick del Canvas — se auto-busca si no se asigna")]
    [SerializeField] private Joystick movementJoystick;

    [Header("Animación (opcional)")]
    [SerializeField] private Animator animator;

    // Parámetros del Animator
    private static readonly int SpeedHash      = Animator.StringToHash("Speed");
    private static readonly int YVelocityHash  = Animator.StringToHash("YVelocity");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsPushingHash  = Animator.StringToHash("IsPushing");
    private static readonly int IsHoldingHash  = Animator.StringToHash("estaSosteniendoCaja");

    // Estado interno
    private Rigidbody rb;
    private bool isGrounded;
    private Vector2 keyboardInput;
    private bool jumpPressed;
    private bool actionPressedThisFrame;

    // Interacción
    private ActionInteractable heldInteractable;
    private PushableObject activePushable;
    private bool isPushing;

    // Singleton local para que el Canvas lo encuentre
    public static PlayerOfflineController Instance { get; private set; }

    public Transform HoldPoint => holdPoint;
    public bool IsHolding => heldInteractable != null;
    public bool IsPushing => isPushing;
    public Vector2 MoveInput => GetMoveInput();

    private void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (movementJoystick == null)
            movementJoystick = FindAnyObjectByType<Joystick>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // -------------------------------------------------------
    // INPUT SYSTEM — callbacks del teclado/gamepad
    // -------------------------------------------------------
    private void OnEnable()
    {
        InputSystem.onAfterUpdate += ReadKeyboard;
    }

    private void OnDisable()
    {
        InputSystem.onAfterUpdate -= ReadKeyboard;
    }

    private void ReadKeyboard()
    {
        var kb = Keyboard.current;
        var gp = Gamepad.current;

        if (kb == null && gp == null) return;

        Vector2 input = Vector2.zero;

        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    input.y =  1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  input.y = -1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x =  1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  input.x = -1f;

            if (kb.spaceKey.wasPressedThisFrame) jumpPressed = true;
            if (kb.eKey.wasPressedThisFrame)     actionPressedThisFrame = true;
        }

        if (gp != null)
        {
            Vector2 stick = gp.leftStick.ReadValue();
            if (stick.sqrMagnitude > 0.01f) input = stick;

            if (gp.buttonSouth.wasPressedThisFrame)    jumpPressed = true;
            if (gp.buttonWest.wasPressedThisFrame)     actionPressedThisFrame = true;
        }

        keyboardInput = Vector2.ClampMagnitude(input, 1f);
    }

    private Vector2 GetMoveInput()
    {
        if (movementJoystick != null && movementJoystick.Direction.sqrMagnitude > 0.01f)
            return movementJoystick.Direction;
        return keyboardInput;
    }

    // -------------------------------------------------------
    // CANVAS BUTTONS — llamados por MobileUIConnectorOffline
    // -------------------------------------------------------
    public void MobileJumpDown()   => jumpPressed = true;
    public void MobileJumpUp()     => jumpPressed = false;
    public void TriggerAction()    => actionPressedThisFrame = true;

    // Métodos estáticos para que MobileUIConnectorOffline no necesite referencia
    public static void StaticJumpDown()    => Instance?.MobileJumpDown();
    public static void StaticJumpUp()      => Instance?.MobileJumpUp();
    public static void StaticAction()      => Instance?.TriggerAction();

    // -------------------------------------------------------
    // LOOP
    // -------------------------------------------------------
    private void Update()
    {
        CheckGround();
        HandleJump();
        HandleInteraction();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        Move();

        if (isPushing && activePushable != null)
            activePushable.ApplyPush(GetMoveInput(), Camera.main);
    }

    // -------------------------------------------------------
    // MOVIMIENTO
    // -------------------------------------------------------
    private void Move()
    {
        if (isPushing) return;

        Vector2 raw  = GetMoveInput();
        Vector3 move = new Vector3(raw.x, 0f, raw.y);

        rb.linearVelocity = new Vector3(
            move.x * moveSpeed,
            rb.linearVelocity.y,
            move.z * moveSpeed
        );

        if (move.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(move);
    }

    private void HandleJump()
    {
        if (isPushing) return;

        if (jumpPressed && isGrounded)
        {
            rb.linearVelocity = new Vector3(
                rb.linearVelocity.x,
                jumpForce,
                rb.linearVelocity.z
            );
            jumpPressed = false;
        }
    }

    private void CheckGround()
    {
        isGrounded = Physics.Raycast(
            transform.position, Vector3.down, groundCheckDistance, groundMask
        );
    }

    // -------------------------------------------------------
    // INTERACCIÓN
    // -------------------------------------------------------
    private void HandleInteraction()
    {
        if (!actionPressedThisFrame) return;

        if (heldInteractable != null)
        {
            heldInteractable.Interact(GetOfflineInteractor());
        }
        else if (isPushing && activePushable != null)
        {
            activePushable.Interact(GetOfflineInteractor());
        }
        else
        {
            ActionInteractable target = FindBestInteractable();
            if (target != null)
                target.Interact(GetOfflineInteractor());
        }

        actionPressedThisFrame = false;
    }

    private ActionInteractable FindBestInteractable()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, interactionRadius, interactableMask);

        ActionInteractable best = null;
        float bestScore = float.MinValue;

        foreach (var hit in hits)
        {
            ActionInteractable interactable = hit.GetComponentInParent<ActionInteractable>();
            if (interactable == null) continue;

            float distance = Vector3.Distance(transform.position, hit.ClosestPoint(transform.position));
            float score = interactable.Priority * 10f - distance;

            if (score > bestScore)
            {
                bestScore = score;
                best = interactable;
            }
        }

        return best;
    }

    // Wrapper para que ActionInteractable reciba un PlayerInteractor simulado.
    // Como los interactuables online usan ServerRpc, en offline
    // los llamamos directamente si tienen lógica local, o los ignoramos.
    // Para DoorInteractable y objetos simples funciona directo.
    private PlayerInteractor GetOfflineInteractor()
    {
        // Intentamos encontrar un PlayerInteractor en este GameObject por si
        // el diseñador lo dejó. Si no hay, devolvemos null (los ServerRpc
        // no se ejecutan offline pero la puerta y objetos simples sí responden).
        return GetComponent<PlayerInteractor>();
    }

    // Llamados por PushableObject / GrabbableObject para actualizar estado local
    public void StartPushOffline(PushableObject pushable, Vector3 snapPosition)
    {
        isPushing      = true;
        activePushable = pushable;
        transform.position = snapPosition;
    }

    public void StopPushOffline()
    {
        isPushing      = false;
        activePushable = null;
    }

    public void SetHeldInteractable(ActionInteractable interactable)
    {
        heldInteractable = interactable;
    }

    public void ClearHeldInteractable(ActionInteractable interactable)
    {
        if (heldInteractable == interactable)
            heldInteractable = null;
    }

    // -------------------------------------------------------
    // ANIMACIONES
    // -------------------------------------------------------
    private void UpdateAnimator()
    {
        if (animator == null) return;

        float speed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;
        animator.SetFloat(SpeedHash,     speed, 0.1f, Time.deltaTime);
        animator.SetFloat(YVelocityHash, rb.linearVelocity.y);
        animator.SetBool(IsGroundedHash, isGrounded);
        animator.SetBool(IsPushingHash,  isPushing);
        animator.SetBool(IsHoldingHash,  IsHolding);
    }
}
