using UnityEngine;

/// <summary>
/// Controlador completo para el prefab PlayerOffline.
/// Funciona 100% sin red — ideal para testear niveles en el editor.
///
/// SETUP del prefab PlayerOffline:
///   1. Duplicá el prefab Player → renombralo PlayerOffline
///   2. Quitá estos componentes (no son necesarios offline):
///        NetworkObject, ClientNetworkTransform, NetworkAnimator,
///        PlayerInputHandler, PlayerInteractor, PlayerCameraTarget,
///        PlayerAnimatorController
///   3. Agregá este script (PlayerOfflineController)
///   4. En la CinemachineCamera de la escena, asigná el transform del
///      PlayerOffline como Tracking Target manualmente
///   5. Poné el prefab en la escena y dale Play — listo
///
/// CONTROLES:
///   Teclado:  WASD / Flechas → mover  |  Space → saltar
///   Android:  no aplica (este prefab es solo para editor)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerOfflineController : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed          = 5f;
    [SerializeField] private float jumpForce          = 7f;
    [SerializeField] private float groundCheckDistance = 0.25f;
    [SerializeField] private LayerMask groundMask;

    [Header("Animación (opcional)")]
    [SerializeField] private Animator animator;

    // Hashes de parámetros del Animator
    private static readonly int SpeedHash      = Animator.StringToHash("Speed");
    private static readonly int YVelocityHash  = Animator.StringToHash("YVelocity");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsPushingHash  = Animator.StringToHash("IsPushing");
    private static readonly int IsHoldingHash  = Animator.StringToHash("estaSosteniendoCaja");

    private Rigidbody rb;
    private bool isGrounded;
    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        ReadInput();
        CheckGround();
        HandleJump();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        Move();
    }

    private void ReadInput()
    {
        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
    }

    private void Move()
    {
        Vector3 move = new Vector3(moveInput.x, 0f, moveInput.y);
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
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.linearVelocity = new Vector3(
                rb.linearVelocity.x,
                jumpForce,
                rb.linearVelocity.z
            );
        }
    }

    private void CheckGround()
    {
        isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundMask);
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        float speed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;
        animator.SetFloat(SpeedHash, speed, 0.1f, Time.deltaTime);
        animator.SetFloat(YVelocityHash, rb.linearVelocity.y);
        animator.SetBool(IsGroundedHash, isGrounded);
        animator.SetBool(IsPushingHash, false);
        animator.SetBool(IsHoldingHash, false);
    }
}
