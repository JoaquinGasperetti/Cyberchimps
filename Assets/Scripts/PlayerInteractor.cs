using UnityEngine;
using Unity.Netcode;

public class PlayerInteractor : NetworkBehaviour
{
    [SerializeField] private float interactionRadius = 2f;
    [SerializeField] private LayerMask interactableMask;
    [SerializeField] private Transform holdPoint;

    private PlayerInputHandler input;
    private Camera mainCamera;

    private ActionInteractable heldInteractable;
    private PushableObject activePushable;

    // ── NetworkVariables — sincronizadas a todos los clientes ─────────────
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

    // ── Estado local ──────────────────────────────────────────────────────
    private bool _isPushingLocal;
    private bool _isHoldingLocal;

    // ── API pública ───────────────────────────────────────────────────────
    // Owner usa valor local (sin latencia).
    // Remoto usa NetworkVariable (sincronizado) → Animator del segundo jugador funciona.
    public bool IsPushing => IsOwner ? _isPushingLocal : netIsPushing.Value;
    public bool IsHolding => IsOwner ? _isHoldingLocal : netIsHolding.Value;
    public Transform HoldPoint => holdPoint;
    public Camera MainCamera => mainCamera;
    public Vector2 MoveInput => input != null ? input.MoveInput : Vector2.zero;
    public PushableObject ActivePushable => activePushable;

    // =========================================================
    // INIT
    // =========================================================

    private void Awake()
    {
        input = GetComponent<PlayerInputHandler>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            mainCamera = Camera.main;
        else
            enabled = false;
    }

    // =========================================================
    // LOOP
    // =========================================================

    private void Update()
    {
        if (!IsOwner) return;
        if (input == null || !input.ActionPressedThisFrame) return;

        if (heldInteractable != null)
        {
            heldInteractable.Interact(this);
            return;
        }

        if (_isPushingLocal && activePushable != null)
        {
            activePushable.Interact(this);
            return;
        }

        ActionInteractable target = FindBestInteractable();
        target?.Interact(this);
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        if (_isPushingLocal && activePushable != null)
            activePushable.ApplyPush(input.MoveInput, mainCamera);
    }

    // =========================================================
    // BÚSQUEDA
    // =========================================================

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

    // =========================================================
    // API — llamada por GrabbableObject
    // =========================================================

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
        SyncHoldingServerRpc(false);
    }

    // ── API — llamada por PushableObject ──────────────────────────────────

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

    // =========================================================
    // SERVER RPCs — sincronizar a todos los clientes
    // =========================================================

    [ServerRpc]
    private void SyncHoldingServerRpc(bool holding)
    {
        netIsHolding.Value = holding;
    }

    [ServerRpc]
    private void SyncPushingServerRpc(bool pushing)
    {
        netIsPushing.Value = pushing;
    }
}