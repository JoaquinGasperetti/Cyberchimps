using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Objeto agarrable y lanzable sincronizado en red.
/// REQUERIDO en el prefab: NetworkObject.
/// 
/// IMPORTANTE: NO usa SetParent porque Netcode no permite poner un NetworkObject
/// bajo un transform que no tiene NetworkObject (el HoldPoint es un transform simple).
/// En cambio, mientras es sostenido, se mueve a la posición del HoldPoint cada frame.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GrabbableObject : ActionInteractable
{
    [SerializeField] private float throwForce = 8f;

    private Rigidbody rb;

    // ulong.MaxValue = nadie lo sostiene
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
        rb.isKinematic = isHeld;
        rb.detectCollisions = !isHeld;
    }

    // Seguir al HoldPoint cada frame (en todos los clientes que ven al holder)
    private void Update()
    {
        if (holderClientId.Value == ulong.MaxValue) return;

        PlayerInteractor holder = FindInteractorByClientId(holderClientId.Value);
        if (holder != null && holder.HoldPoint != null)
        {
            transform.position = holder.HoldPoint.position;
            transform.rotation = holder.HoldPoint.rotation;
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
        else if (holderClientId.Value == interactor.OwnerClientId)
            ThrowServerRpc(interactor.transform.forward);
    }

    [ServerRpc(RequireOwnership = false)]
    private void GrabServerRpc(ulong clientId)
    {
        PlayerInteractor interactor = FindInteractorByClientId(clientId);
        if (interactor == null || interactor.HoldPoint == null) return;

        holderClientId.Value = clientId;

        // Notificar al owner del interactor que registre el held
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

        rb.isKinematic = false;
        rb.detectCollisions = true;
        rb.linearVelocity = Vector3.zero;
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
        // Solo llega al cliente que agarró el objeto
        PlayerInteractor local = PlayerInputHandler.LocalInstance?.GetComponent<PlayerInteractor>();
        local?.SetHeldInteractable(this);
    }

    [ClientRpc]
    private void NotifyThrowClientRpc(ClientRpcParams rpcParams = default)
    {
        // Solo llega al cliente que soltó el objeto
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
