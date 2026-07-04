using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Zona de meta cooperativa: el nivel se completa cuando TODOS los jugadores
/// conectados están adentro al mismo tiempo.
///
/// - Con un solo jugador adentro, ambos ven "En la meta: 1/2" (vía LevelManager).
/// - Cuando entran todos, el servidor toma una foto de las stats de cada
///   jugador (Cyberdatos del nivel) y la manda por ClientRpc — los wallets son
///   privados durante la partida (ReadPermission.Owner), pero el resumen final
///   se comparte con ambos.
///
/// SETUP en Unity:
/// 1. GameObject con Collider en modo trigger + este script.
/// 2. Agregar componente NetworkObject (in-scene placed, igual que LevelTimer).
/// </summary>
public class GoalZone : NetworkBehaviour
{
    [Tooltip("0 = automático: se necesitan TODOS los jugadores conectados. " +
             "Otro valor fija la cantidad a mano (útil para probar solo).")]
    [SerializeField] private int requiredPlayersOverride = 0;

    private NetworkVariable<int> playersInZone = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<int> requiredPlayers = new NetworkVariable<int>(
        2, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> completed = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Solo el servidor lo usa — qué clientes están dentro de la zona
    private readonly HashSet<ulong> clientsInside = new HashSet<ulong>();

    public override void OnNetworkSpawn()
    {
        playersInZone.OnValueChanged += HandleProgressChanged;
        requiredPlayers.OnValueChanged += HandleProgressChanged;

        if (IsServer)
        {
            RefreshRequiredPlayers();
            NetworkManager.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        playersInZone.OnValueChanged -= HandleProgressChanged;
        requiredPlayers.OnValueChanged -= HandleProgressChanged;

        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    // ── Triggers (solo el servidor cuenta) ────────────────────────────────

    private void OnTriggerEnter(Collider other) => HandlePlayerTrigger(other, entered: true);
    private void OnTriggerExit(Collider other) => HandlePlayerTrigger(other, entered: false);

    private void HandlePlayerTrigger(Collider other, bool entered)
    {
        if (!IsServer || completed.Value) return;
        if (!other.CompareTag("Player")) return;

        var netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj == null) return;

        if (entered) clientsInside.Add(netObj.OwnerClientId);
        else clientsInside.Remove(netObj.OwnerClientId);

        playersInZone.Value = clientsInside.Count;
        TryComplete();
    }

    // ── Cambios de conexión (server) ──────────────────────────────────────

    private void HandleClientConnected(ulong clientId)
    {
        RefreshRequiredPlayers();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        // OnTriggerExit no se dispara si el player despawnea — limpiar a mano.
        clientsInside.Remove(clientId);
        playersInZone.Value = clientsInside.Count;
        RefreshRequiredPlayers();
        // Si el jugador restante ya estaba en la meta, el nivel se completa solo.
        TryComplete();
    }

    private void RefreshRequiredPlayers()
    {
        requiredPlayers.Value = requiredPlayersOverride > 0
            ? requiredPlayersOverride
            : Mathf.Max(1, NetworkManager.ConnectedClientsIds.Count);
    }

    // ── Completado ────────────────────────────────────────────────────────

    private void TryComplete()
    {
        if (!IsServer || completed.Value) return;
        if (clientsInside.Count < requiredPlayers.Value) return;

        completed.Value = true;

        // Foto de stats por jugador — solo el servidor puede leer todos los wallets
        var wallets = FindObjectsByType<PlayerCyberdataWallet>(FindObjectsSortMode.None);
        var clientIds = new ulong[wallets.Length];
        var cyberdata = new int[wallets.Length];

        for (int i = 0; i < wallets.Length; i++)
        {
            clientIds[i] = wallets[i].OwnerClientId;
            cyberdata[i] = wallets[i].LevelCyberdata;
        }

        CompleteLevelClientRpc(clientIds, cyberdata);
    }

    [ClientRpc]
    private void CompleteLevelClientRpc(ulong[] clientIds, int[] cyberdata)
    {
        Debug.Log("META ALCANZADA — nivel completado para todos");

        LevelTimer.Instance?.StopTimer(); // solo tiene efecto en el server

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.HideGoalProgress();
            LevelManager.Instance.CompleteLevel(clientIds, cyberdata);
        }
    }

    // ── UI de progreso (corre en TODOS los clientes) ──────────────────────

    private void HandleProgressChanged(int oldValue, int newValue)
    {
        if (completed.Value) return;
        LevelManager.Instance?.ShowGoalProgress(playersInZone.Value, requiredPlayers.Value);
    }
}
