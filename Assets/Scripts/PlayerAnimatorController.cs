using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Actualiza todos los parámetros del Animator del jugador.
///
/// PARÁMETROS REQUERIDOS EN EL ANIMATOR:
///   Float  "Speed"               → velocidad horizontal (Locomotion blend tree)
///   Float  "YVelocity"           → velocidad vertical (salto/caída)
///   Float  "PushSpeed"           → 0 = parado empujando / 1 = moviendo la roca
///   Bool   "IsGrounded"          → true = en el suelo
///   Bool   "IsPushing"           → true = en modo empujar
///   Bool   "estaSosteniendoCaja" → true = sosteniendo objeto agarrable
///
/// BLEND TREE "Pushing" — configuración recomendada:
///   Tipo: 1D
///   Parámetro: PushSpeed   ← CAMBIAR de Speed a PushSpeed
///   Motion 0 (threshold 0): Male Action Pose  (pose estática empujando)
///   Motion 1 (threshold 1): Pushing           (animación de empuje en movimiento)
///
/// De esta forma:
///   PushSpeed = 0 → pose de empuje estática (jugador apoya pero no mueve la roca)
///   PushSpeed = 1 → animación de pasos empujando
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimatorController : NetworkBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int YVelocityHash = Animator.StringToHash("YVelocity");
    private static readonly int PushSpeedHash = Animator.StringToHash("PushSpeed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsPushingHash = Animator.StringToHash("IsPushing");
    private static readonly int IsHoldingHash = Animator.StringToHash("estaSosteniendoCaja");

    [SerializeField] private float speedDampTime = 0.08f;
    [SerializeField] private float pushSpeedDampTime = 0.05f;

    private Animator animator;
    private Rigidbody rb;
    private PlayerInteractor interactor;
    private PlayerController playerController;

    private Vector3 previousPosition;

    // =========================================================
    // INIT
    // =========================================================

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        interactor = GetComponent<PlayerInteractor>();
        playerController = GetComponent<PlayerController>();
    }

    public override void OnNetworkSpawn()
    {
        previousPosition = transform.position;
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (animator == null) return;

        UpdateLocomotion();
        UpdateGrounded();
        UpdatePushing();
        UpdateHolding();
    }

    // ── Velocidad de locomoción ───────────────────────────────────────────

    private void UpdateLocomotion()
    {
        Vector3 vel;

        if (IsOwner)
            vel = rb != null ? rb.linearVelocity : Vector3.zero;
        else
            vel = (transform.position - previousPosition) / Time.deltaTime;

        previousPosition = transform.position;

        float speed = new Vector3(vel.x, 0f, vel.z).magnitude;
        animator.SetFloat(SpeedHash, speed, speedDampTime, Time.deltaTime);
        animator.SetFloat(YVelocityHash, vel.y);
    }

    // ── IsGrounded ────────────────────────────────────────────────────────

    private void UpdateGrounded()
    {
        bool grounded = playerController != null && playerController.IsGrounded;
        animator.SetBool(IsGroundedHash, grounded);
    }

    // ── Pushing + PushSpeed ───────────────────────────────────────────────

    private void UpdatePushing()
    {
        bool pushing = interactor != null && interactor.IsPushing;
        animator.SetBool(IsPushingHash, pushing);

        if (!pushing)
        {
            // Fuera de modo empujar: resetear PushSpeed suavemente a 0
            animator.SetFloat(PushSpeedHash, 0f, pushSpeedDampTime, Time.deltaTime);
            return;
        }

        // Obtener PushSpeed desde el PushableObject que se está empujando
        float pushSpeed = 0f;

        if (interactor != null && interactor.ActivePushable != null)
            pushSpeed = interactor.ActivePushable.SyncedPushSpeed;

        animator.SetFloat(PushSpeedHash, pushSpeed, pushSpeedDampTime, Time.deltaTime);
    }

    // ── Holding (objeto agarrado) ─────────────────────────────────────────

    private void UpdateHolding()
    {
        bool holding = interactor != null && interactor.IsHolding;
        animator.SetBool(IsHoldingHash, holding);
    }
}