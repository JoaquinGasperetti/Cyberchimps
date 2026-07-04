using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Objeto empujable sincronizado en red.
///
/// MODELO DE SINCRONIZACIÓN (fix de desincronización de cajas):
///   - La física corre SOLO en el servidor. En los clientes el Rigidbody es
///     siempre kinematic y la posición llega por el NetworkTransform del prefab
///     (server-authoritative). Antes cada cliente integraba la velocity
///     sincronizada por su cuenta y las posiciones divergían con el tiempo.
///   - El jugador que empuja se pega a la caja LOCALMENTE (ver
///     PlayerInteractor.FixedUpdate). Antes el servidor escribía la posición
///     del player, pero el Player usa ClientNetworkTransform (autoridad del
///     dueño) así que esa escritura peleaba con la sincronización y el
///     jugador cliente no acompañaba a la caja.
/// </summary>
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

    // PushSpeed sincronizado para que el Animator del remoto también lo reciba
    private NetworkVariable<float> syncedPushSpeed = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public float SyncedPushSpeed => syncedPushSpeed.Value;

    /// <summary>ClientId del jugador que está empujando (ulong.MaxValue = nadie).</summary>
    public ulong PusherClientId => pusherClientId.Value;

    /// <summary>Eje cardinal fijo del empuje actual (para el glue del jugador).</summary>
    public Vector3 PushAxis => pushAxis.Value;

    /// <summary>Distancia jugador-caja mientras empuja (para el glue del jugador).</summary>
    public float SnapDistance => snapDistance;

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

        // Los clientes nunca simulan física propia: la posición llega por el
        // NetworkTransform (server-authoritative) del prefab.
        if (!IsServer)
            rb.isKinematic = true;
    }

    public override void OnNetworkDespawn()
    {
        pusherClientId.OnValueChanged -= OnPusherChanged;
    }

    // =========================================================
    // CALLBACKS
    // =========================================================

    private void OnPusherChanged(ulong previous, ulong current)
    {
        bool isPushed = current != ulong.MaxValue;

        // Solo el servidor simula; en clientes el rb queda siempre kinematic
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
        syncedPushSpeed.Value = speed; // 0–1, alimenta el Blend Tree del Animator

        // NOTA: acá antes se movía el transform del jugador desde el servidor.
        // Se quitó porque el Player es owner-authoritative (ClientNetworkTransform)
        // y esa escritura peleaba con la posición que manda el dueño.
        // Ahora el dueño se pega solo a la caja en PlayerInteractor.FixedUpdate.
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
}
