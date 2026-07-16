using Unity.Netcode;
using UnityEngine;

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

    // cache del pushable que empuja este jugador cuando es remoto
    private PushableObject remotePushable;

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

    private void Update()
    {
        if (animator == null) return;

        UpdateLocomotion();
        UpdateGrounded();
        UpdatePushing();
        UpdateHolding();
    }

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

    private void UpdateGrounded()
    {
        bool grounded = playerController != null && playerController.IsGrounded;
        animator.SetBool(IsGroundedHash, grounded);
    }

    private void UpdatePushing()
    {
        bool pushing = interactor != null && interactor.IsPushing;
        animator.SetBool(IsPushingHash, pushing);

        if (!pushing)
        {
            animator.SetFloat(PushSpeedHash, 0f, pushSpeedDampTime, Time.deltaTime);
            return;
        }

        float pushSpeed = 0f;

        if (interactor != null && interactor.ActivePushable != null)
        {
            pushSpeed = interactor.ActivePushable.SyncedPushSpeed;
        }
        else
        {
            // en el remoto ActivePushable es null: buscamos el pushable que
            // este empujando este jugador
            if (remotePushable == null || remotePushable.PusherClientId != OwnerClientId)
                remotePushable = FindPushableBy(OwnerClientId);

            if (remotePushable != null)
                pushSpeed = remotePushable.SyncedPushSpeed;
        }

        animator.SetFloat(PushSpeedHash, pushSpeed, pushSpeedDampTime, Time.deltaTime);
    }

    private static PushableObject FindPushableBy(ulong clientId)
    {
        foreach (var pushable in FindObjectsByType<PushableObject>(FindObjectsSortMode.None))
        {
            if (pushable.PusherClientId == clientId)
                return pushable;
        }
        return null;
    }

    private void UpdateHolding()
    {
        bool holding = interactor != null && interactor.IsHolding;
        animator.SetBool(IsHoldingHash, holding);
    }
}