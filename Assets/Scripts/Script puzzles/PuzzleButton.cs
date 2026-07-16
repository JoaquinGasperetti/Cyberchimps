using UnityEngine;
using Unity.Netcode;

public class PuzzleButton : NetworkBehaviour
{
    [Header("Referencia")]
    [Tooltip("La puerta que controla este botón")]
    [SerializeField] private PuzzleDoor door;

    [Header("Feedback visual (opcional)")]
    [SerializeField] private GameObject pressedVisual;
    [SerializeField] private GameObject releasedVisual;

    // contador en vez de bool: pueden estar el jugador y una caja a la vez
    private NetworkVariable<int> activatorCount = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool IsPressed => activatorCount.Value > 0;

    public override void OnNetworkSpawn()
    {
        activatorCount.OnValueChanged += OnActivatorCountChanged;
        UpdateVisual(IsPressed);
    }

    public override void OnNetworkDespawn()
    {
        activatorCount.OnValueChanged -= OnActivatorCountChanged;
    }

    private void OnActivatorCountChanged(int previous, int current)
    {
        UpdateVisual(current > 0);

        // avisar a la puerta en todos los clientes
        door?.EvaluateButtons();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!IsValidActivator(other)) return;

        activatorCount.Value++;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        if (!IsValidActivator(other)) return;

        activatorCount.Value = Mathf.Max(0, activatorCount.Value - 1);
    }

    private static bool IsValidActivator(Collider other)
    {
        return other.CompareTag("Player") || other.CompareTag("Box");
    }

    private void UpdateVisual(bool pressed)
    {
        if (pressedVisual != null) pressedVisual.SetActive(pressed);
        if (releasedVisual != null) releasedVisual.SetActive(!pressed);
    }
}