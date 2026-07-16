using UnityEngine;
using Unity.Netcode;

public class PlayerInteractor : NetworkBehaviour
{
    [Header("Detección")]
    [SerializeField] private float interactionRadius = 2f;
    [SerializeField] private LayerMask interactableMask;
    [SerializeField] private Transform holdPoint;

    [Header("Tap / Hold")]
    [Tooltip("Segundos para distinguir tap (lanzar) de hold (colocar)")]
    [SerializeField] private float holdThreshold = 0.4f;

    private PlayerInputHandler input;
    private Camera mainCamera;

    private ActionInteractable heldInteractable;
    private PushableObject activePushable;

    private bool isHoldingAction;
    private float holdTimer;

    private NetworkVariable<bool> netIsPushing = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> netIsHolding = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool _isPushingLocal;
    private bool _isHoldingLocal;

    public bool IsPushing => IsOwner ? _isPushingLocal : netIsPushing.Value;
    public bool IsHolding => IsOwner ? _isHoldingLocal : netIsHolding.Value;
    public Transform HoldPoint => holdPoint;
    public Camera MainCamera => mainCamera;
    public Vector2 MoveInput => input != null ? input.MoveInput : Vector2.zero;
    public PushableObject ActivePushable => activePushable;

    private void Awake()
    {
        input = GetComponent<PlayerInputHandler>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            mainCamera = Camera.main;
        else
            enabled = false; // para remotos no corre logica; las NetworkVariables se leen igual
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleActionInput();
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        if (_isPushingLocal && activePushable != null)
        {
            activePushable.ApplyPush(input.MoveInput, mainCamera);

            // el dueño se pega solo a la caja; el server no puede moverlo porque
            // el player es owner-authoritative y esa escritura peleaba con la sync
            Vector3 target = activePushable.transform.position
                           - activePushable.PushAxis * activePushable.SnapDistance;
            target.y = transform.position.y;
            transform.position = target;
        }
    }

    private void HandleActionInput()
    {
        if (input.ActionPressedThisFrame)
        {
            if (heldInteractable == null)
            {
                HandleRegularInteraction();
                return;
            }

            isHoldingAction = true;
            holdTimer = 0f;
        }

        if (isHoldingAction)
        {
            holdTimer += Time.deltaTime;
        }

        if (isHoldingAction && input.ActionReleased)
        {
            isHoldingAction = false;

            if (heldInteractable is GrabbableObject grabbable)
            {
                if (holdTimer < holdThreshold)
                    grabbable.Throw(this);   // tap = lanzar
                else
                    grabbable.Place(this);   // hold = apoyar enfrente
            }
        }
    }

    private void HandleRegularInteraction()
    {
        if (_isPushingLocal && activePushable != null)
        {
            activePushable.Interact(this);
            return;
        }

        ActionInteractable target = FindBestInteractable();
        target?.Interact(this);
    }

    private ActionInteractable FindBestInteractable()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position, interactionRadius, interactableMask
        );

        ActionInteractable best = null;
        float bestScore = float.MinValue;

        foreach (var hit in hits)
        {
            ActionInteractable interactable = hit.GetComponentInParent<ActionInteractable>();
            if (interactable == null || !interactable.CanInteract(this)) continue;

            float dist = Vector3.Distance(transform.position, hit.ClosestPoint(transform.position));
            float score = interactable.Priority * 10f - dist;

            if (score > bestScore)
            {
                bestScore = score;
                best = interactable;
            }
        }

        return best;
    }

    public void SetHeldInteractable(ActionInteractable interactable)
    {
        heldInteractable = interactable;
        _isHoldingLocal = true;
        SyncHoldingServerRpc(true);
    }

    public void ClearHeldInteractable(ActionInteractable interactable)
    {
        if (heldInteractable != interactable) return;
        heldInteractable = null;
        _isHoldingLocal = false;
        isHoldingAction = false;
        holdTimer = 0f;
        SyncHoldingServerRpc(false);
    }

    public void StartPush(PushableObject pushable, Vector3 snapPosition)
    {
        _isPushingLocal = true;
        activePushable = pushable;
        transform.position = snapPosition;
        SyncPushingServerRpc(true);
    }

    public void StopPush()
    {
        _isPushingLocal = false;
        activePushable = null;
        SyncPushingServerRpc(false);
    }

    [ServerRpc]
    private void SyncHoldingServerRpc(bool holding)
        => netIsHolding.Value = holding;

    [ServerRpc]
    private void SyncPushingServerRpc(bool pushing)
        => netIsPushing.Value = pushing;
}