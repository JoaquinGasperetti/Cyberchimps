using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

/// <summary>
/// FIXES respecto a versión anterior:
///
///   1. El Rigidbody NUNCA se pone kinematic en el owner.
///      Solo el jugador REMOTO usa isKinematic = true.
///      Esto resuelve "Setting linear velocity of a kinematic body is not supported".
///
///   2. ClientNetworkTransform en conflicto:
///      Si el prefab tiene ClientNetworkTransform, ese componente
///      pone el Rigidbody en kinematic automáticamente en clientes no-owner.
///      El owner mantiene física real — eso es lo correcto.
///
///   3. El salto usa rb.AddForce en lugar de asignar linearVelocity directamente.
///      AddForce respeta el modo del Rigidbody y es más estable en Unity 6.
///
///   4. Coyote time mantenido para evitar flickering de IsGrounded.
///
/// SETUP DEL PREFAB (importante):
///   Rigidbody:
///     - Is Kinematic: FALSE (nunca activarlo en el prefab)
///     - Collision Detection: Continuous
///     - Freeze Rotation: X ✓  Y ✓  Z ✓  (evita que el personaje se caiga)
///     - Interpolation: Interpolate
///   Componentes de red:
///     - NetworkObject ✓
///     - ClientNetworkTransform ✓  (NO usar NetworkTransform — ese es server-auth)
///   Ground Check:
///     - Crear Transform hijo llamado "GroundCheck" en los pies del personaje
///     - Asignarlo en el Inspector de este script
///     - Layer del suelo debe estar en groundMask
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 7f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private LayerMask groundMask;

    [Tooltip("Segundos que IsGrounded sigue true tras perder contacto. " +
             "Evita flickering de animaciones. Recomendado: 0.08 - 0.12")]
    [SerializeField] private float coyoteTime = 0.10f;

    // ── Componentes ───────────────────────────────────────────────────────
    private Rigidbody rb;
    private PlayerInputHandler input;
    private PlayerInteractor interactor;

    // ── Ground state ──────────────────────────────────────────────────────
    private bool localIsGrounded;
    private float coyoteTimer;
    private bool lastSentGrounded;

    // Solo se actualiza cuando el valor CAMBIA — no satura la red
    private NetworkVariable<bool> netIsGrounded = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// Owner → valor local (sin latencia).
    /// Remoto → NetworkVariable (sincronizado).
    /// PlayerAnimatorController usa esta propiedad.
    /// </summary>
    public bool IsGrounded => IsOwner ? localIsGrounded : netIsGrounded.Value;

    // =========================================================
    // INIT
    // =========================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        input = GetComponent<PlayerInputHandler>();
        interactor = GetComponent<PlayerInteractor>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Owner: física activa, el Rigidbody mueve al personaje
            rb.isKinematic = false;
        }
        else
        {
            // Remoto: posición viene del ClientNetworkTransform, no de física
            rb.isKinematic = true;
        }

        // Forzar sincronización inicial
        lastSentGrounded = !localIsGrounded;
    }

    // =========================================================
    // LOOP — solo corre lógica en el owner
    // =========================================================

    private void Update()
    {
        if (!IsOwner) return;

        CheckGround();
        HandleJump();
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;
        HandleMove();
    }

    // =========================================================
    // MOVIMIENTO
    // =========================================================

    private void HandleMove()
    {
        if (interactor != null && interactor.IsPushing) return;

        Vector2 raw = input.MoveInput;
        Vector3 move = new Vector3(raw.x, 0f, raw.y);

        // Preservar velocidad Y para que la gravedad siga funcionando
        rb.linearVelocity = new Vector3(
            move.x * moveSpeed,
            rb.linearVelocity.y,
            move.z * moveSpeed
        );

        if (move.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(move);
    }

    // =========================================================
    // SALTO
    // =========================================================

    private void HandleJump()
    {
        if (interactor != null && interactor.IsPushing) return;
        if (!input.JumpPressed || !localIsGrounded) return;

        // Resetear velocidad Y antes de aplicar el impulso
        // para que saltos desde rampas sean consistentes
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        // Forzar IsGrounded = false inmediatamente
        // para que el Animator arranque Jumping Up en este mismo frame
        localIsGrounded = false;
        coyoteTimer = 0f;
        SyncGrounded();

        input.ConsumeJump();
    }

    // =========================================================
    // GROUND CHECK CON COYOTE TIME
    // =========================================================

    private void CheckGround()
    {
        Vector3 checkPos = groundCheckPoint != null
            ? groundCheckPoint.position
            : transform.position + Vector3.down * 0.05f;

        bool physics = Physics.CheckSphere(checkPos, groundCheckRadius, groundMask);

        if (physics)
        {
            localIsGrounded = true;
            coyoteTimer = coyoteTime;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
            if (coyoteTimer <= 0f)
                localIsGrounded = false;
        }

        SyncGrounded();
    }

    private void SyncGrounded()
    {
        if (localIsGrounded == lastSentGrounded) return;
        lastSentGrounded = localIsGrounded;

        if (IsServer)
            netIsGrounded.Value = localIsGrounded;
        else
            UpdateGroundedServerRpc(localIsGrounded);
    }

    [ServerRpc]
    private void UpdateGroundedServerRpc(bool grounded)
    {
        netIsGrounded.Value = grounded;
    }

    // =========================================================
    // GIZMOS — visualizar ground check en Scene View
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Vector3 pos = groundCheckPoint != null
            ? groundCheckPoint.position
            : transform.position + Vector3.down * 0.05f;

        Gizmos.color = localIsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(pos, groundCheckRadius);
    }
}