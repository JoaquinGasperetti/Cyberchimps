using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Maneja toda la interacción del jugador con el mundo.
///
/// FIX PRINCIPAL — solo el host podía interactuar:
///   El bug era que ActionInteractable hereda de NetworkBehaviour, y los ServerRpc
///   solo pueden llamarse si el objeto tiene RequireOwnership = false.
///   Todos los Interact/ServerRpc ya tienen RequireOwnership = false —
///   el fix real aquí es que enabled = false en clientes remotos bloqueaba
///   correctamente el input del otro jugador, PERO el problema estaba en que
///   PlayerInputHandler también desactivaba ActionPressedThisFrame para no-owners.
///   Revisado: el interactor ahora usa IsOwner correctamente y el ActionPressedThisFrame
///   solo se lee en el owner — el cliente no-owner de ESTE player no interactúa,
///   pero el owner del OTRO player sí puede llamar ServerRpc en cualquier objeto.
///
/// LÓGICA TAP / HOLD para GrabbableObject:
///   - Al presionar Acción con objeto en mano → empieza a contar holdTime
///   - Al soltar Acción:
///       < holdThreshold → Throw (lanzar)
///       >= holdThreshold → Place (colocar enfrente)
/// </summary>
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

    // ── Estado tap/hold ───────────────────────────────────────────────────
    private bool isHoldingAction;   // botón de acción está siendo mantenido
    private float holdTimer;

    // ── NetworkVariables ──────────────────────────────────────────────────
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

    // ── API pública ───────────────────────────────────────────────────────
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
            enabled = false; // este componente no corre lógica para jugadores remotos
                             // pero sus NetworkVariables siguen siendo accesibles
    }

    // =========================================================
    // LOOP
    // =========================================================

    private void Update()
    {
        if (!IsOwner) return;

        HandleActionInput();
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        if (_isPushingLocal && activePushable != null)
            activePushable.ApplyPush(input.MoveInput, mainCamera);
    }

    // =========================================================
    // INPUT DE ACCIÓN — lógica tap/hold
    // =========================================================

    private void HandleActionInput()
    {
        // ── Inicio de pulsación ───────────────────────────────────────────
        if (input.ActionPressedThisFrame)
        {
            // Si NO sostiene objeto → interactuar inmediatamente (botones, push, etc.)
            if (heldInteractable == null)
            {
                HandleRegularInteraction();
                return;
            }

            // Si sostiene objeto → empezar a contar para tap/hold
            isHoldingAction = true;
            holdTimer = 0f;
        }

        // ── Mantener pulsación ────────────────────────────────────────────
        if (isHoldingAction)
        {
            holdTimer += Time.deltaTime;
        }

        // ── Soltar pulsación ──────────────────────────────────────────────
        if (isHoldingAction && input.ActionReleased)
        {
            isHoldingAction = false;

            if (heldInteractable is GrabbableObject grabbable)
            {
                if (holdTimer < holdThreshold)
                    grabbable.Throw(this);   // tap → lanzar
                else
                    grabbable.Place(this);   // hold → colocar enfrente
            }
        }
    }

    private void HandleRegularInteraction()
    {
        // Empujando → salir del modo push
        if (_isPushingLocal && activePushable != null)
        {
            activePushable.Interact(this);
            return;
        }

        // Buscar objeto más cercano
        ActionInteractable target = FindBestInteractable();
        target?.Interact(this);
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
        isHoldingAction = false;
        holdTimer = 0f;
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
    // SERVER RPCs
    // =========================================================

    [ServerRpc]
    private void SyncHoldingServerRpc(bool holding)
        => netIsHolding.Value = holding;

    [ServerRpc]
    private void SyncPushingServerRpc(bool pushing)
        => netIsPushing.Value = pushing;
}