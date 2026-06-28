using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Puerta de puzzle sincronizada en red.
///
/// Se abre cuando TODOS los botones de su lista están presionados al mismo tiempo.
/// Una vez abierta, permanece abierta (puzzle resuelto).
///
/// SETUP:
///   - NetworkObject ✓
///   - Asignar doorMesh: el Transform del mesh que se mueve (hijo de la puerta)
///   - Asignar openPosition y closedPosition como posiciones locales del doorMesh
///   - Asignar la lista de PuzzleButtons en el Inspector
///   - Este script no necesita que los botones sean hijos — pueden estar en cualquier lugar
/// </summary>
public class PuzzleDoor : NetworkBehaviour
{
    [Header("Botones requeridos")]
    [Tooltip("Lista de botones que deben estar todos presionados para abrir la puerta")]
    [SerializeField] private List<PuzzleButton> requiredButtons = new List<PuzzleButton>();

    [Header("Movimiento de la puerta")]
    [SerializeField] private Transform doorMesh;
    [SerializeField] private Vector3 openPosition;
    [SerializeField] private Vector3 closedPosition;
    [SerializeField] private float moveSpeed = 2f;

    // Una vez abierta, no se vuelve a cerrar (puzzle resuelto permanentemente)
    private NetworkVariable<bool> isSolved = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Vector3 targetPosition;

    // =========================================================
    // INIT
    // =========================================================

    public override void OnNetworkSpawn()
    {
        targetPosition = isSolved.Value ? openPosition : closedPosition;
        isSolved.OnValueChanged += OnSolvedChanged;
    }

    public override void OnNetworkDespawn()
    {
        isSolved.OnValueChanged -= OnSolvedChanged;
    }

    private void OnSolvedChanged(bool previous, bool current)
    {
        if (current)
            targetPosition = openPosition;
    }

    // =========================================================
    // UPDATE — mover la puerta suavemente en todos los clientes
    // =========================================================

    private void Update()
    {
        if (doorMesh == null) return;

        doorMesh.localPosition = Vector3.Lerp(
            doorMesh.localPosition,
            targetPosition,
            Time.deltaTime * moveSpeed
        );
    }

    // =========================================================
    // EVALUACIÓN — llamado por cada PuzzleButton cuando cambia su estado
    // Solo corre en el servidor.
    // =========================================================

    public void EvaluateButtons()
    {
        if (!IsServer) return;
        if (isSolved.Value) return; // ya resuelto, ignorar

        // Verificar que haya al menos un botón configurado
        if (requiredButtons.Count == 0) return;

        // Todos los botones deben estar presionados simultáneamente
        bool allPressed = true;
        foreach (PuzzleButton button in requiredButtons)
        {
            if (button == null || !button.IsPressed)
            {
                allPressed = false;
                break;
            }
        }

        if (allPressed)
        {
            isSolved.Value = true;
            targetPosition = openPosition;
        }
        // Si no están todos presionados y no está resuelta, la puerta permanece cerrada.
        // No se cierra si se resuelve — isSolved es permanente.
    }

    // =========================================================
    // GIZMOS — visualizar posiciones open/closed en el editor
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        if (doorMesh == null) return;

        // Posición abierta en verde
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.TransformPoint(openPosition), Vector3.one * 0.3f);

        // Posición cerrada en rojo
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.TransformPoint(closedPosition), Vector3.one * 0.3f);
    }
}