using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float groundCheckRadius = 0.3f;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private Transform groundCheckPoint;

    private Rigidbody rb;
    private PlayerInputHandler input;
    private PlayerInteractor interactor;

    // Sincronizado en red — solo se actualiza cuando CAMBIA el valor,
    // no cada frame, para no saturar la red con RPCs
    private NetworkVariable<bool> netIsGrounded = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool localIsGrounded;
    private bool lastSentGrounded; // último valor enviado al servidor

    public bool IsGrounded => IsOwner ? localIsGrounded : netIsGrounded.Value;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        input = GetComponent<PlayerInputHandler>();
        interactor = GetComponent<PlayerInteractor>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            rb.isKinematic = true;

        // Inicializar para que el primer cambio siempre se envíe
        lastSentGrounded = !localIsGrounded;
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
        Move();
    }

    private void Move()
    {
        if (interactor != null && interactor.IsPushing) return;

        Vector3 move = new Vector3(input.MoveInput.x, 0, input.MoveInput.y);
        Vector3 velocity = move * moveSpeed;

        rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);

        if (move.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(move);
    }

    private void HandleJump()
    {
        if (interactor != null && interactor.IsPushing) return;

        if (input.JumpPressed && localIsGrounded)
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
        Vector3 checkPos = groundCheckPoint != null
            ? groundCheckPoint.position
            : transform.position + Vector3.down * 0.5f;

        Collider[] hits = Physics.OverlapSphere(checkPos, groundCheckRadius, groundMask);
        localIsGrounded = hits.Length > 0;

        // Solo sincronizar cuando el valor cambia — evita flood de RPCs
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
}