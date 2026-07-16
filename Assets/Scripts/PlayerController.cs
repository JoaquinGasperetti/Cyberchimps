using UnityEngine;
using Unity.Netcode;

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

    private Rigidbody rb;
    private PlayerInputHandler input;
    private PlayerInteractor interactor;

    private bool localIsGrounded;
    private float coyoteTimer;

    // arranca en null para que el primer CheckGround sincronice siempre;
    // con bool, si spawneabas apoyado el primer estado no se mandaba y el
    // remoto quedaba con la animacion de caida hasta el primer salto
    private bool? lastSentGrounded;

    // true porque spawnean apoyados: el remoto no se ve caer al arrancar
    private NetworkVariable<bool> netIsGrounded = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool IsGrounded => IsOwner ? localIsGrounded : netIsGrounded.Value;

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

    private void HandleMove()
    {
        // mientras empuja no hay movimiento libre: PlayerInteractor lo pega
        // a la caja, aca solo frenamos la velocidad horizontal
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

    private void HandleJump()
    {
        // empujando tampoco se salta
        if (interactor != null && interactor.IsPushing) return;
        if (!input.JumpPressed || !localIsGrounded) return;

        // resetear la Y para que el salto salga siempre igual
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        // bajar IsGrounded ya mismo, asi el Animator arranca el salto este frame
        localIsGrounded = false;
        coyoteTimer = 0f;
        SyncGrounded();

        input.ConsumeJump();
    }

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

    private void OnDrawGizmosSelected()
    {
        Vector3 pos = groundCheckPoint != null
            ? groundCheckPoint.position
            : transform.position + Vector3.down * 0.05f;

        Gizmos.color = localIsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(pos, groundCheckRadius);
    }
}