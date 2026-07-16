using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

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

    // una vez abierta queda abierta
    private NetworkVariable<bool> isSolved = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Vector3 targetPosition;

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

    private void Update()
    {
        if (doorMesh == null) return;

        doorMesh.localPosition = Vector3.Lerp(
            doorMesh.localPosition,
            targetPosition,
            Time.deltaTime * moveSpeed
        );
    }

    public void EvaluateButtons()
    {
        if (!IsServer) return;
        if (isSolved.Value) return; // ya resuelto, ignorar

        if (requiredButtons.Count == 0) return;

        // tienen que estar todos presionados a la vez
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
    }

    private void OnDrawGizmosSelected()
    {
        if (doorMesh == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.TransformPoint(openPosition), Vector3.one * 0.3f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.TransformPoint(closedPosition), Vector3.one * 0.3f);
    }
}