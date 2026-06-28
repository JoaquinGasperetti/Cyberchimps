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

    private NetworkVariable<Vector3> syncedVelocity = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // PushSpeed sincronizado para que el Animator del remoto también lo reciba
    private NetworkVariable<float> syncedPushSpeed = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public float SyncedPushSpeed => syncedPushSpeed.Value;

    private PlayerInteractor activeInteractorServer;

    // =========================================================
    // INIT
    // =========================================================

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

    // =========================================================
    // CALLBACKS
    // =========================================================

    private void OnPusherChanged(ulong previous, ulong current)
    {
        bool isPushed = current != ulong.MaxValue;
        rb.isKinematic = !isPushed;

        if (!isPushed)
        {
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

    private void OnVelocityChanged(Vector3 previous, Vector3 current)
    {
        if (!IsServer)
            rb.linearVelocity = current;
    }

    // =========================================================
    // INTERACCIÓN
    // =========================================================

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
            activeInteractorServer = null;
            pusherClientId.Value = ulong.MaxValue;
            syncedVelocity.Value = Vector3.zero;
            syncedPushSpeed.Value = 0f;
            pushAxis.Value = Vector3.zero;
        }
        else if (pusherClientId.Value == ulong.MaxValue)
        {
            PlayerInteractor interactor = FindInteractorByClientId(clientId);
            if (interactor == null) return;

            activeInteractorServer = interactor;

            Vector3 dir = (transform.position - playerPosition);
            dir.y = 0f;
            dir.Normalize();
            pushAxis.Value = SnapToCardinalAxis(dir);
            pusherClientId.Value = clientId;
        }
    }

    // =========================================================
    // APLICAR EMPUJE
    // Solo hacia adelante (proyección positiva sobre el eje).
    // Si el jugador empuja hacia atrás o perpendicular → sin efecto.
    // =========================================================

    public void ApplyPush(Vector2 input, Camera cam)
    {
        if (cam == null) return;

        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
        Vector3 moveDir = right * input.x + forward * input.y;

        // Proyectar sobre el eje fijo
        float projection = Vector3.Dot(moveDir, pushAxis.Value);

        // Solo hacia adelante: ignorar input negativo (hacia atrás)
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
        syncedVelocity.Value = velocity;
        syncedPushSpeed.Value = speed; // 0–1, alimenta el Blend Tree del Animator

        if (activeInteractorServer != null)
        {
            Vector3 targetPos = transform.position - pushAxis.Value * snapDistance;
            targetPos.y = activeInteractorServer.transform.position.y;
            activeInteractorServer.transform.position = targetPos;
        }
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static Vector3 SnapToCardinalAxis(Vector3 dir)
    {
        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.z))
            return new Vector3(Mathf.Sign(dir.x), 0f, 0f);
        else
            return new Vector3(0f, 0f, Mathf.Sign(dir.z));
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