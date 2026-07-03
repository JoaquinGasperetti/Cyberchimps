using UnityEngine;
using Unity.Netcode;

public class GoalZone : NetworkBehaviour
{
    private NetworkVariable<bool> completed = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        completed.OnValueChanged += OnLevelCompleted;
    }

    public override void OnNetworkDespawn()
    {
        completed.OnValueChanged -= OnLevelCompleted;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (completed.Value) return;
        if (!other.CompareTag("Player")) return;

        completed.Value = true;
    }

    private void OnLevelCompleted(bool previous, bool current)
    {
        if (!current) return;

        // OnValueChanged corre en TODOS los clientes → el panel de resultados
        // aparece sincronizado para ambos jugadores.
        LevelTimer.Instance?.StopTimer();
        Debug.Log("META ALCANZADA");

        if (LevelManager.Instance != null)
            LevelManager.Instance.CompleteLevel();
    }
}
