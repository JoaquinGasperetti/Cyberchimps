using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Objeto empujable sincronizado en red.
/// REQUERIDO en el prefab: NetworkObject.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PushableObject : ActionInteractable
{
    [SerializeField] private float pushSpeed = 2f;
    [SerializeField] private float snapDistance = 1f;

    private Rigidbody rb;

    // ulong.MaxValue = nadie empujando
    private NetworkVariable<ulong> pusherClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<Vector3> syncedVelocity = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Solo relevante en el servidor
    private PlayerInteractor activeInteractorServer;
    private Vector3 pushOffset;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
    }

    public override void OnNetworkSpawn()
    {
        pusherClientId.OnValueChanged += OnPusherChanged;
        syncedVelocity.OnValueChanged += OnVelocityChanged;
    }

    public override void OnNetworkDespawn()
    {
        pusherClientId.OnValueChanged -= OnPusherChanged;
        syncedVelocity.OnValueChanged -= OnVelocityChanged;
    }

    private void OnPusherChanged(ulong previous, ulong current)
    {
        bool isPushed = current != ulong.MaxValue;
        rb.isKinematic = !isPushed;

        if (!isPushed)
            rb.linearVelocity = Vector3.zero;

        // Avisar al interactor LOCAL (si es que somos nosotros los que empujamos/dejamos)
        PlayerInteractor local = PlayerInputHandler.LocalInstance?.GetComponent<PlayerInteractor>();
        if (local == null) return;

        if (isPushed && current == local.OwnerClientId)
        {
            // Calculamos snap position localmente
            Vector3 dir = (local.transform.position - transform.position).normalized;
            Vector3 snap = transform.position + dir * snapDistance;
            local.StartPush(this, snap);

            Vector3 lookDir = transform.position - local.transform.position;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.001f)
                local.transform.rotation = Quaternion.LookRotation(lookDir);
        }
        else if (!isPushed && previous == local.OwnerClientId)
        {
            local.StopPush();
        }
    }

    private void OnVelocityChanged(Vector3 previous, Vector3 current)
    {
        // Los clientes aplican la velocidad recibida del servidor
        if (!IsServer)
            rb.linearVelocity = current;
    }

    public override bool CanInteract(PlayerInteractor interactor)
    {
        return pusherClientId.Value == ulong.MaxValue
            || pusherClientId.Value == interactor.OwnerClientId;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        InteractServerRpc(interactor.OwnerClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void InteractServerRpc(ulong clientId)
    {
        if (pusherClientId.Value == clientId)
        {
            // Ya estaba empujando → salir
            activeInteractorServer = null;
            pusherClientId.Value = ulong.MaxValue;
            syncedVelocity.Value = Vector3.zero;
        }
        else if (pusherClientId.Value == ulong.MaxValue)
        {
            // Nadie empujando → entrar
            PlayerInteractor interactor = FindInteractorByClientId(clientId);
            if (interactor == null) return;

            activeInteractorServer = interactor;
            Vector3 dir = (interactor.transform.position - transform.position).normalized;
            pushOffset = transform.position + dir * snapDistance - transform.position;

            pusherClientId.Value = clientId;
        }
    }

    /// <summary>
    /// Llamado desde PlayerInteractor.FixedUpdate del owner local.
    /// </summary>
    public void ApplyPush(Vector2 input, Camera cam)
    {
        if (cam == null) return;

        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        Vector3 right   = Vector3.ProjectOnPlane(cam.transform.right,   Vector3.up).normalized;
        Vector3 move    = right * input.x + forward * input.y;

        Vector3 velocity = new Vector3(move.x * pushSpeed, rb.linearVelocity.y, move.z * pushSpeed);
        ApplyPushServerRpc(velocity);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ApplyPushServerRpc(Vector3 velocity)
    {
        if (pusherClientId.Value == ulong.MaxValue) return;

        rb.linearVelocity = velocity;
        syncedVelocity.Value = velocity;

        // Mantener al player pegado (en el servidor, se propaga via NetworkTransform del player)
        if (activeInteractorServer != null)
        {
            Vector3 targetPos = transform.position + pushOffset;
            activeInteractorServer.transform.position = new Vector3(
                targetPos.x,
                activeInteractorServer.transform.position.y,
                targetPos.z
            );
        }
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
