using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Movimiento, salto y ground check del jugador online.
///
/// CAMBIO respecto a versión anterior:
///   - HandleJump bloquea el salto cuando interactor.IsPushing es true.
///     Antes solo bloqueaba el movimiento horizontal, no el salto.
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

    // null = todavía no se envió ningún estado → el primer CheckGround SIEMPRE
    // sincroniza. Antes era bool y el "truco" lastSentGrounded = !localIsGrounded
    // fallaba cuando el jugador spawneaba apoyado en el suelo: el primer estado
    // real (true) coincidía con el invertido y nunca se enviaba → el jugador
    // remoto quedaba con la animación de caída hasta el primer salto.
    private bool? lastSentGrounded;

    // Arranca en true: los jugadores spawnean apoyados, así el remoto no se ve
    // cayendo durante los primeros frames antes de la primera sincronización.
    private NetworkVariable<bool> netIsGrounded = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

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
        rb.isKinematic = !IsOwner;
        lastSentGrounded = null;
    }

    // =========================================================
    // LOOP
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
        // El movimiento libre está bloqueado mientras empuja —
        // PlayerInteractor.FixedUpdate pega al jugador a la caja.
        // Se frena la velocidad horizontal para que la física no arrastre
        // al player mientras el glue le fija la posición.
        if (interactor != null && interactor.IsPushing)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        Vector2 raw = input.MoveInput;
        Vector3 move = new Vector3(raw.x, 0f, raw.y);

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
    // Bloqueado mientras empuja — el jugador debe soltar la roca
    // (presionar Acción) antes de poder saltar.
    // =========================================================

    private void HandleJump()
    {
        // Bloqueo explícito: ni mover ni saltar mientras se empuja
        if (interactor != null && interactor.IsPushing) return;
        if (!input.JumpPressed || !localIsGrounded) return;

        // Resetear Y antes del impulso para saltos consistentes
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        // Forzar IsGrounded false inmediatamente → Animator arranca Jumping Up este frame
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
    // GIZMOS
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