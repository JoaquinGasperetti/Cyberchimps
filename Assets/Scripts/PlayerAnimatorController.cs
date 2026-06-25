using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Maneja los parámetros del Animator del jugador en red.
/// IsGrounded viene del NetworkVariable del PlayerController — fiable en todos los clientes.
/// Speed se calcula por delta de posición para el jugador remoto.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimatorController : NetworkBehaviour
{
    private static readonly int SpeedHash      = Animator.StringToHash("Speed");
    private static readonly int YVelocityHash  = Animator.StringToHash("YVelocity");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsPushingHash  = Animator.StringToHash("IsPushing");
    private static readonly int IsHoldingHash  = Animator.StringToHash("estaSosteniendoCaja");

    [SerializeField] private float speedDampTime = 0.1f;

    private Animator         animator;
    private Rigidbody        rb;
    private PlayerInteractor interactor;
    private PlayerController playerController;

    private Vector3 previousPosition;

    private void Awake()
    {
        animator         = GetComponent<Animator>();
        rb               = GetComponent<Rigidbody>();
        interactor       = GetComponent<PlayerInteractor>();
        playerController = GetComponent<PlayerController>();
    }

    public override void OnNetworkSpawn()
    {
        previousPosition = transform.position;
    }

    private void Update()
    {
        if (animator == null) return;

        // ── Velocidad ────────────────────────────────────────────────────
        Vector3 vel;
        if (IsOwner)
        {
            vel = rb != null ? rb.linearVelocity : Vector3.zero;
        }
        else
        {
            // Jugador remoto: derivar velocidad del delta de posición
            vel = (transform.position - previousPosition) / Time.deltaTime;
        }
        previousPosition = transform.position;

        float speed = new Vector3(vel.x, 0f, vel.z).magnitude;
        animator.SetFloat(SpeedHash, speed, speedDampTime, Time.deltaTime);
        animator.SetFloat(YVelocityHash, vel.y);

        // ── IsGrounded — viene del NetworkVariable, fiable en todos los clientes ─
        bool grounded = playerController != null && playerController.IsGrounded;
        animator.SetBool(IsGroundedHash, grounded);

        // ── IsPushing ────────────────────────────────────────────────────
        bool pushing = interactor != null && interactor.IsPushing;
        animator.SetBool(IsPushingHash, pushing);

        // ── estaSosteniendoCaja ──────────────────────────────────────────
        bool holding = interactor != null && interactor.IsHolding;
        animator.SetBool(IsHoldingHash, holding);
    }
}
