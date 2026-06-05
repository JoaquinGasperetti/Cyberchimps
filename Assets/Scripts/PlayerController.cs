using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundMask;

    private Rigidbody rb;
    private PlayerInputHandler input;
    private PlayerInteractor interactor;
    private Animator anim;
    private bool isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        input = GetComponent<PlayerInputHandler>();
        interactor = GetComponent<PlayerInteractor>();
        anim = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (!LevelManager.Instance.CanPlay)
            return;
        CheckGround();
        HandleJump();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        Move();
    }

    private void Move()
    {
        if (!LevelManager.Instance.CanPlay)
            return;
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
        if (!LevelManager.Instance.CanPlay)
            return;
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
    private void UpdateAnimator()
    {
        if (anim == null) return;

        float currentSpeed = 0f;

       
        if (interactor != null && interactor.IsPushing)
        {
            // Como el Rigidbody está quieto, usamos la fuerza con la que mueves el joystick (0 a 1)
            // Multiplicado por moveSpeed para simular la velocidad y engañar al Animator
            currentSpeed = input.MoveInput.magnitude * moveSpeed;
        }
        else
        {
            // Movimiento normal libre
            Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            currentSpeed = flatVelocity.magnitude;
        }

        // Le pasamos la velocidad final calculada
        anim.SetFloat("Speed", currentSpeed);
        anim.SetFloat("YVelocity", rb.linearVelocity.y);
        anim.SetBool("IsGrounded", isGrounded);

        if (interactor != null)
        {
            anim.SetBool("IsPushing", interactor.IsPushing);
            anim.SetBool("estaSosteniendoCaja", interactor.IsHolding);
        }
    }

    private void CheckGround()
    {
        Ray ray = new Ray(transform.position, Vector3.down);
        isGrounded = Physics.Raycast(ray, groundCheckDistance, groundMask);
    }
}