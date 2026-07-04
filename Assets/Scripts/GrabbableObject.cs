using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Objeto agarrable sincronizado en red.
///
/// COMPORTAMIENTO DEL BOTÓN DE ACCIÓN:
///   - Tap (soltar rápido < holdThreshold): lanza la caja hacia adelante
///   - Hold (mantener >= holdThreshold): suelta la caja en el suelo enfrente del jugador
///
/// MODELO DE SINCRONIZACIÓN (fix de trayectorias divergentes):
///   - La física corre SOLO en el servidor. En los clientes el Rigidbody es
///     siempre kinematic y la posición llega por el NetworkTransform del prefab
///     (server-authoritative). Antes cada cliente simulaba su propia caída y el
///     lanzamiento se veía distinto en cada pantalla.
///   - Mientras está sostenida, TODOS los peers pegan la caja al HoldPoint del
///     jugador que la lleva en LateUpdate (pisa la interpolación del
///     NetworkTransform → la caja se ve pegada a la mano sin lag).
///
/// SETUP DE LA CAJA:
///   - NetworkObject ✓
///   - NetworkTransform (server-authoritative) ✓
///   - Rigidbody ✓
///   - Tag: "Box"  ← necesario para que PuzzleButton la detecte
///   - Collider (no trigger)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GrabbableObject : ActionInteractable
{
    [Header("Lanzamiento")]
    [SerializeField] private float throwForce = 8f;

    [Header("Colocación (hold)")]
    [Tooltip("Segundos que hay que mantener el botón para COLOCAR en vez de LANZAR")]
    [SerializeField] private float holdThreshold = 0.4f;
    [SerializeField] private float placeDistance = 1.5f; // distancia enfrente del jugador

    private Rigidbody rb;

    // Quién sostiene la caja (ulong.MaxValue = nadie)
    private NetworkVariable<ulong> holderClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Cache del interactor que sostiene la caja (se resuelve al cambiar holder,
    // no cada frame — FindObjectsByType por frame era muy caro en mobile)
    private PlayerInteractor holderInteractor;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        holderClientId.OnValueChanged += OnHolderChanged;

        // Los clientes NUNCA simulan física propia: el NetworkTransform
        // (server-authoritative) es la única fuente de posición.
        if (!IsServer)
            rb.isKinematic = true;

        // Reconexión / late join: si ya estaba sostenida, reflejar el estado
        if (holderClientId.Value != ulong.MaxValue)
            OnHolderChanged(ulong.MaxValue, holderClientId.Value);
    }

    public override void OnNetworkDespawn()
    {
        holderClientId.OnValueChanged -= OnHolderChanged;
    }

    // =========================================================
    // HOLDER CHANGED — se ejecuta en todos los clientes
    // =========================================================

    private void OnHolderChanged(ulong previous, ulong current)
    {
        bool isHeld = current != ulong.MaxValue;

        // Solo el servidor alterna la simulación; los clientes quedan kinematic
        if (IsServer)
            rb.isKinematic = isHeld;

        // En mano no colisiona (no empuja jugadores ni pisa botones)
        rb.detectCollisions = !isHeld;

        holderInteractor = isHeld ? FindInteractorByClientId(current) : null;
    }

    // =========================================================
    // UPDATE — safeguard del servidor
    // LATEUPDATE — seguir al HoldPoint mientras es sostenida
    // =========================================================

    private void Update()
    {
        // Si el jugador que la sostenía se desconectó, el servidor la suelta
        // (antes quedaba flotando para siempre).
        if (IsServer && holderClientId.Value != ulong.MaxValue && holderInteractor == null)
        {
            holderInteractor = FindInteractorByClientId(holderClientId.Value);
            if (holderInteractor == null)
                ReleaseOnServer();
        }
    }

    private void LateUpdate()
    {
        // LateUpdate para pisar la interpolación del NetworkTransform:
        // mientras está sostenida, la caja se ve pegada a la mano en todos
        // los clientes, sin lag de red.
        if (holderClientId.Value == ulong.MaxValue) return;

        if (holderInteractor == null)
            holderInteractor = FindInteractorByClientId(holderClientId.Value);

        if (holderInteractor != null && holderInteractor.HoldPoint != null)
        {
            transform.position = holderInteractor.HoldPoint.position;
            transform.rotation = Quaternion.identity;
        }
    }

    // =========================================================
    // INTERACCIÓN
    // =========================================================

    public override bool CanInteract(PlayerInteractor interactor)
    {
        return holderClientId.Value == ulong.MaxValue
            || holderClientId.Value == interactor.OwnerClientId;
    }

    /// <summary>
    /// Llamado por PlayerInteractor al presionar el botón de acción.
    /// Si no sostiene → agarrar.
    /// Si sostiene → PlayerInteractor maneja tap/hold y llama Throw o Place.
    /// </summary>
    public override void Interact(PlayerInteractor interactor)
    {
        if (holderClientId.Value == ulong.MaxValue)
            GrabServerRpc(interactor.OwnerClientId);
        // Si ya sostiene, la lógica tap/hold se maneja en PlayerInteractor
    }

    /// <summary>Lanzar la caja — llamado por PlayerInteractor en tap.</summary>
    public void Throw(PlayerInteractor interactor)
    {
        if (holderClientId.Value != interactor.OwnerClientId) return;
        ThrowServerRpc(interactor.OwnerClientId, interactor.transform.forward);
    }

    /// <summary>Colocar la caja enfrente — llamado por PlayerInteractor en hold.</summary>
    public void Place(PlayerInteractor interactor)
    {
        if (holderClientId.Value != interactor.OwnerClientId) return;

        Vector3 placePos = interactor.transform.position
                         + interactor.transform.forward * placeDistance;

        // Raycast hacia abajo para apoyar la caja en el suelo
        if (Physics.Raycast(placePos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f))
            placePos.y = hit.point.y + 0.5f; // 0.5 = mitad de la caja aprox

        PlaceServerRpc(interactor.OwnerClientId, placePos);
    }

    // =========================================================
    // SERVER RPCs
    // =========================================================

    [ServerRpc(RequireOwnership = false)]
    private void GrabServerRpc(ulong clientId)
    {
        // Ya la tiene otro jugador (dos agarres casi simultáneos) → ignorar
        if (holderClientId.Value != ulong.MaxValue) return;

        PlayerInteractor interactor = FindInteractorByClientId(clientId);
        if (interactor == null || interactor.HoldPoint == null) return;

        holderClientId.Value = clientId;

        NotifyGrabClientRpc(new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        });
    }

    [ServerRpc(RequireOwnership = false)]
    private void ThrowServerRpc(ulong clientId, Vector3 throwDirection)
    {
        if (holderClientId.Value != clientId) return;

        holderClientId.Value = ulong.MaxValue;
        rb.isKinematic = false;
        rb.detectCollisions = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(throwDirection * throwForce, ForceMode.Impulse);

        NotifyReleaseClientRpc(new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        });
    }

    [ServerRpc(RequireOwnership = false)]
    private void PlaceServerRpc(ulong clientId, Vector3 position)
    {
        if (holderClientId.Value != clientId) return;

        holderClientId.Value = ulong.MaxValue;
        rb.isKinematic = false;
        rb.detectCollisions = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = position;

        NotifyReleaseClientRpc(new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        });
    }

    /// <summary>Soltar forzado desde el servidor (ej: el holder se desconectó).</summary>
    private void ReleaseOnServer()
    {
        holderClientId.Value = ulong.MaxValue;
        rb.isKinematic = false;
        rb.detectCollisions = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    [ClientRpc]
    private void NotifyGrabClientRpc(ClientRpcParams rpcParams = default)
    {
        PlayerInteractor local = PlayerInputHandler.LocalInstance?.GetComponent<PlayerInteractor>();
        local?.SetHeldInteractable(this);
    }

    [ClientRpc]
    private void NotifyReleaseClientRpc(ClientRpcParams rpcParams = default)
    {
        PlayerInteractor local = PlayerInputHandler.LocalInstance?.GetComponent<PlayerInteractor>();
        local?.ClearHeldInteractable(this);
    }

    // =========================================================
    // HELPER
    // =========================================================

    private static PlayerInteractor FindInteractorByClientId(ulong clientId)
    {
        foreach (var obj in FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None))
        {
            if (obj.OwnerClientId == clientId)
                return obj;
        }
        return null;
    }
}
