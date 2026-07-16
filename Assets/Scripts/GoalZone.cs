using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

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

    // solo lo usa el server: quienes estan adentro de la zona
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

    private void HandleClientConnected(ulong clientId)
    {
        RefreshRequiredPlayers();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        // OnTriggerExit no salta si el player despawnea, hay que limpiarlo a mano
        clientsInside.Remove(clientId);
        playersInZone.Value = clientsInside.Count;
        RefreshRequiredPlayers();
        // si el que queda ya estaba en la meta, el nivel se completa solo
        TryComplete();
    }

    private void RefreshRequiredPlayers()
    {
        requiredPlayers.Value = requiredPlayersOverride > 0
            ? requiredPlayersOverride
            : Mathf.Max(1, NetworkManager.ConnectedClientsIds.Count);
    }

    private void TryComplete()
    {
        if (!IsServer || completed.Value) return;
        if (clientsInside.Count < requiredPlayers.Value) return;

        completed.Value = true;

        // solo el server puede leer los wallets de todos
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

    private void HandleProgressChanged(int oldValue, int newValue)
    {
        if (completed.Value) return;
        LevelManager.Instance?.ShowGoalProgress(playersInZone.Value, requiredPlayers.Value);
    }
}
