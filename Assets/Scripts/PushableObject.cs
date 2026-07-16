using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class PushableObject : ActionInteractable
{
    [Header("Empuje")]
    [SerializeField] private float pushSpeed = 3f;
    [SerializeField] private float snapDistance = 1.2f;

    private Rigidbody rb;

    private NetworkVariable<ulong> pusherClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<Vector3> pushAxis = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // sincronizado para que el Animator del remoto tambien lo reciba
    private NetworkVariable<float> syncedPushSpeed = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public float SyncedPushSpeed => syncedPushSpeed.Value;

    public ulong PusherClientId => pusherClientId.Value;

    public Vector3 PushAxis => pushAxis.Value;

    public float SnapDistance => snapDistance;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
    }

    public override void OnNetworkSpawn()
    {
        pusherClientId.OnValueChanged += OnPusherChanged;

        // los clientes no simulan fisica: la posicion llega por el
        // NetworkTransform del server
        if (!IsServer)
            rb.isKinematic = true;
    }

    public override void OnNetworkDespawn()
    {
        pusherClientId.OnValueChanged -= OnPusherChanged;
    }

    private void OnPusherChanged(ulong previous, ulong current)
    {
        bool isPushed = current != ulong.MaxValue;

        // solo simula el server; en los clientes el rb queda kinematic
        if (IsServer)
        {
            rb.isKinematic = !isPushed;
            if (!isPushed)
                rb.linearVelocity = Vector3.zero;
        }

        PlayerInteractor local = PlayerInputHandler.LocalInstance?.GetComponent<PlayerInteractor>();
        if (local == null) return;

        if (isPushed && current == local.OwnerClientId)
        {
            Vector3 snapPos = transform.position - pushAxis.Value * snapDistance;
            snapPos.y = local.transform.position.y;
            local.StartPush(this, snapPos);

            Vector3 lookDir = transform.position - local.transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
                local.transform.rotation = Quaternion.LookRotation(lookDir);
        }
        else if (!isPushed && previous == local.OwnerClientId)
        {
            local.StopPush();
        }
    }

    public override bool CanInteract(PlayerInteractor interactor)
    {
        return pusherClientId.Value == ulong.MaxValue
            || pusherClientId.Value == interactor.OwnerClientId;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        InteractServerRpc(interactor.OwnerClientId, interactor.transform.position);
    }

    [ServerRpc(RequireOwnership = false)]
    private void InteractServerRpc(ulong clientId, Vector3 playerPosition)
    {
        if (pusherClientId.Value == clientId)
        {
            pusherClientId.Value = ulong.MaxValue;
            syncedPushSpeed.Value = 0f;
            pushAxis.Value = Vector3.zero;
        }
        else if (pusherClientId.Value == ulong.MaxValue)
        {
            Vector3 dir = (transform.position - playerPosition);
            dir.y = 0f;
            dir.Normalize();
            pushAxis.Value = SnapToCardinalAxis(dir);
            pusherClientId.Value = clientId;
        }
    }

    public void ApplyPush(Vector2 input, Camera cam)
    {
        if (cam == null) return;

        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
        Vector3 moveDir = right * input.x + forward * input.y;

        float projection = Vector3.Dot(moveDir, pushAxis.Value);

        // solo se empuja para adelante
        projection = Mathf.Max(0f, projection);

        Vector3 velocity = pushAxis.Value * (projection * pushSpeed);
        float speed = projection; // 0 = parado, 1 = empujando a fondo

        ApplyPushServerRpc(velocity, speed);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ApplyPushServerRpc(Vector3 velocity, float speed)
    {
        if (pusherClientId.Value == ulong.MaxValue) return;

        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;
        syncedPushSpeed.Value = speed; // 0 a 1, alimenta el blend tree del Animator

        // ojo: aca no se mueve al player desde el server; es owner-authoritative
        // y eso peleaba con la sync. El dueño se pega solo en PlayerInteractor.
    }

    private static Vector3 SnapToCardinalAxis(Vector3 dir)
    {
        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.z))
            return new Vector3(Mathf.Sign(dir.x), 0f, 0f);
        else
            return new Vector3(0f, 0f, Mathf.Sign(dir.z));
    }
}
