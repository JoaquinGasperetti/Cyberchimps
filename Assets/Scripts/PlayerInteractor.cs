using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Maneja toda la interacción del jugador con el mundo: agarrar objetos y empujar cajas.
/// Solo corre en el owner — se desactiva en jugadores remotos.
///
/// FIX aplicado: IsPushing ahora es una NetworkVariable en lugar de una propiedad local,
/// así PlayerAnimatorController puede leerla correctamente en todos los clientes
/// (antes el remoto siempre veía IsPushing = false).
/// </summary>
public class PlayerInteractor : NetworkBehaviour
{
    [SerializeField] private float interactionRadius = 2f;
    [SerializeField] private LayerMask interactableMask;
    [SerializeField] private Transform holdPoint;

    private PlayerInputHandler input;
    private Camera mainCamera;

    private ActionInteractable heldInteractable;
    private PushableObject activePushable;

    // NetworkVariable para que el Animator del jugador remoto también vea IsPushing
    private NetworkVariable<bool> netIsPushing = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool IsPushing => IsOwner ? _isPushingLocal : netIsPushing.Value;
    public bool IsHolding => heldInteractable != null;
    public Transform HoldPoint => holdPoint;
    public Camera MainCamera => mainCamera;
    public Vector2 MoveInput => input != null ? input.MoveInput : Vector2.zero;

    private bool _isPushingLocal;

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

        // Prioridad: objeto en mano → empujando → buscar nuevo
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
    // BÚSQUEDA DE INTERACTUABLES
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
    // API — llamada por PushableObject y GrabbableObject
    // =========================================================

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

    public void SetHeldInteractable(ActionInteractable interactable)
        => heldInteractable = interactable;

    public void ClearHeldInteractable(ActionInteractable interactable)
    {
        if (heldInteractable == interactable)
            heldInteractable = null;
    }

    // ── Sincronizar IsPushing a todos los clientes ────────────────────────
    [ServerRpc]
    private void SyncPushingServerRpc(bool pushing)
    {
        netIsPushing.Value = pushing;
    }
}