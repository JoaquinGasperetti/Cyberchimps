using UnityEngine;
using Unity.Netcode;

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

    // quien tiene la caja (MaxValue = nadie)
    private NetworkVariable<ulong> holderClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // cacheado porque buscarlo cada frame era carisimo en mobile
    private PlayerInteractor holderInteractor;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        holderClientId.OnValueChanged += OnHolderChanged;

        // los clientes no simulan fisica: la posicion llega por el
        // NetworkTransform del server
        if (!IsServer)
            rb.isKinematic = true;

        // por si entramos tarde y la caja ya estaba agarrada
        if (holderClientId.Value != ulong.MaxValue)
            OnHolderChanged(ulong.MaxValue, holderClientId.Value);
    }

    public override void OnNetworkDespawn()
    {
        holderClientId.OnValueChanged -= OnHolderChanged;
    }

    private void OnHolderChanged(ulong previous, ulong current)
    {
        bool isHeld = current != ulong.MaxValue;

        // el server prende y apaga la fisica; los clientes quedan kinematic siempre
        if (IsServer)
            rb.isKinematic = isHeld;

        // en la mano no colisiona, asi no empuja gente ni pisa botones
        rb.detectCollisions = !isHeld;

        holderInteractor = isHeld ? FindInteractorByClientId(current) : null;
    }

    private void Update()
    {
        // si el que la tenia se desconecto, el server la suelta
        if (IsServer && holderClientId.Value != ulong.MaxValue && holderInteractor == null)
        {
            holderInteractor = FindInteractorByClientId(holderClientId.Value);
            if (holderInteractor == null)
                ReleaseOnServer();
        }
    }

    private void LateUpdate()
    {
        // en LateUpdate para pisarle la interpolacion al NetworkTransform:
        // la caja queda pegada a la mano sin lag en todas las pantallas
        if (holderClientId.Value == ulong.MaxValue) return;

        if (holderInteractor == null)
            holderInteractor = FindInteractorByClientId(holderClientId.Value);

        if (holderInteractor != null && holderInteractor.HoldPoint != null)
        {
            transform.position = holderInteractor.HoldPoint.position;
            transform.rotation = Quaternion.identity;
        }
    }

    public override bool CanInteract(PlayerInteractor interactor)
    {
        return holderClientId.Value == ulong.MaxValue
            || holderClientId.Value == interactor.OwnerClientId;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (holderClientId.Value == ulong.MaxValue)
            GrabServerRpc(interactor.OwnerClientId);
        // si ya la tiene, el tap/hold lo maneja PlayerInteractor
    }

    public void Throw(PlayerInteractor interactor)
    {
        if (holderClientId.Value != interactor.OwnerClientId) return;
        ThrowServerRpc(interactor.OwnerClientId, interactor.transform.forward);
    }

    public void Place(PlayerInteractor interactor)
    {
        if (holderClientId.Value != interactor.OwnerClientId) return;

        Vector3 placePos = interactor.transform.position
                         + interactor.transform.forward * placeDistance;

        // raycast para apoyarla en el piso
        if (Physics.Raycast(placePos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f))
            placePos.y = hit.point.y + 0.5f; // media caja aprox

        PlaceServerRpc(interactor.OwnerClientId, placePos);
    }

    [ServerRpc(RequireOwnership = false)]
    private void GrabServerRpc(ulong clientId)
    {
        // por si los dos la agarran casi al mismo tiempo
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
