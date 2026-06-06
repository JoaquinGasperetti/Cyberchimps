using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundMask;

    private Rigidbody rb;
    private PlayerInputHandler input;
    private PlayerInteractor interactor;

    private bool isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        input = GetComponent<PlayerInputHandler>();
        interactor = GetComponent<PlayerInteractor>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            // El NetworkTransform maneja la posición del jugador remoto.
            // Ponemos el Rigidbody en kinematic para que no interfiera.
            rb.isKinematic = true;
        }
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
        if (interactor != null && interactor.IsPushing)
            return;

        Vector3 move = new Vector3(input.MoveInput.x, 0, input.MoveInput.y);
        Vector3 velocity = move * moveSpeed;

        rb.linearVelocity = new Vector3(
            velocity.x,
            rb.linearVelocity.y,
            velocity.z
        );

        if (move.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(move);
        }
    }

    private void HandleJump()
    {
        if (interactor != null && interactor.IsPushing)
            return;

        if (input.JumpPressed && isGrounded)
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
        Ray ray = new Ray(transform.position, Vector3.down);
        isGrounded = Physics.Raycast(ray, groundCheckDistance, groundMask);
    }
}
