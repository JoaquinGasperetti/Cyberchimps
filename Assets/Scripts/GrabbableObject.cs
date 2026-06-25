using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Objeto agarrable sincronizado en red.
/// La caja mantiene rotación Quaternion.identity mientras es sostenida
/// para evitar deformaciones visuales causadas por la rotación del player.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GrabbableObject : ActionInteractable
{
    [SerializeField] private float throwForce = 8f;

    private Rigidbody rb;

    private NetworkVariable<ulong> holderClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        holderClientId.OnValueChanged += OnHolderChanged;
    }

    public override void OnNetworkDespawn()
    {
        holderClientId.OnValueChanged -= OnHolderChanged;
    }

    private void OnHolderChanged(ulong previous, ulong current)
    {
        bool isHeld = current != ulong.MaxValue;
        rb.isKinematic      = isHeld;
        rb.detectCollisions = !isHeld;
    }

    private void Update()
    {
        if (holderClientId.Value == ulong.MaxValue) return;

        PlayerInteractor holder = FindInteractorByClientId(holderClientId.Value);
        if (holder == null || holder.HoldPoint == null) return;

        // Posición: seguir al HoldPoint
        transform.position = holder.HoldPoint.position;

        // Rotación: siempre identity — la caja no rota con el player.
        // Esto evita la deformación visual al girar a los lados.
        transform.rotation = Quaternion.identity;
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
        else if (holderClientId.Value == interactor.OwnerClientId)
            ThrowServerRpc(interactor.transform.forward);
    }

    [ServerRpc(RequireOwnership = false)]
    private void GrabServerRpc(ulong clientId)
    {
        PlayerInteractor interactor = FindInteractorByClientId(clientId);
        if (interactor == null || interactor.HoldPoint == null) return;

        holderClientId.Value = clientId;

        NotifyGrabClientRpc(new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        });
    }

    [ServerRpc(RequireOwnership = false)]
    private void ThrowServerRpc(Vector3 throwDirection)
    {
        ulong previousHolder = holderClientId.Value;
        holderClientId.Value = ulong.MaxValue;

        rb.isKinematic      = false;
        rb.detectCollisions = true;
        rb.linearVelocity   = Vector3.zero;
        rb.AddForce(throwDirection * throwForce, ForceMode.Impulse);

        if (previousHolder != ulong.MaxValue)
        {
            NotifyThrowClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { previousHolder } }
            });
        }
    }

    [ClientRpc]
    private void NotifyGrabClientRpc(ClientRpcParams rpcParams = default)
    {
        PlayerInteractor local = PlayerInputHandler.LocalInstance?.GetComponent<PlayerInteractor>();
        local?.SetHeldInteractable(this);
    }

    [ClientRpc]
    private void NotifyThrowClientRpc(ClientRpcParams rpcParams = default)
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
