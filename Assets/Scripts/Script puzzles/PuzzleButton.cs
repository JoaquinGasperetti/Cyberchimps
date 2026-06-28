using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Botón de puzzle sincronizado en red.
///
/// Activado por: Player (tag "Player") o GrabbableObject (tag "Box")
/// Cuando todos los botones de la puerta están activos simultáneamente → puerta se abre.
///
/// SETUP:
///   - NetworkObject ✓
///   - Collider con Is Trigger ✓
///   - Tag del Player: "Player"
///   - Tag de cajas lanzables: "Box"
///   - Asignar referencia a PuzzleDoor en el Inspector
///   - Opcional: asignar pressMesh y releaseMesh para feedback visual
/// </summary>
public class PuzzleButton : NetworkBehaviour
{
    [Header("Referencia")]
    [Tooltip("La puerta que controla este botón")]
    [SerializeField] private PuzzleDoor door;

    [Header("Feedback visual (opcional)")]
    [SerializeField] private GameObject pressedVisual;   // estado presionado
    [SerializeField] private GameObject releasedVisual;  // estado suelto

    // Cuántos activadores están encima del botón (Player o Box)
    // Usamos un contador en lugar de bool para manejar el caso donde
    // tanto un jugador como una caja están sobre el mismo botón.
    private NetworkVariable<int> activatorCount = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool IsPressed => activatorCount.Value > 0;

    // =========================================================
    // INIT
    // =========================================================

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

        // Notificar a la puerta en todos los clientes
        // (la puerta evalúa su estado internamente)
        door?.EvaluateButtons();
    }

    // =========================================================
    // TRIGGER — detecta Player y Box
    // Solo se procesa en el servidor para evitar duplicados.
    // =========================================================

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

    // =========================================================
    // VISUAL
    // =========================================================

    private void UpdateVisual(bool pressed)
    {
        if (pressedVisual != null) pressedVisual.SetActive(pressed);
        if (releasedVisual != null) releasedVisual.SetActive(!pressed);
    }
}