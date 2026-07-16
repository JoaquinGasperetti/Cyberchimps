using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerSpawnManager : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;

    private readonly Dictionary<ulong, GameObject> spawnedPlayers = new();

    public override void OnNetworkSpawn()
    {
        // spawnea solo el host
        if (!IsServer) return;

        StartCoroutine(SpawnAllPlayersDelayed());
    }

    private IEnumerator SpawnAllPlayersDelayed()
    {
        // un toque de margen para que todos terminen de cargar la escena
        yield return new WaitForSeconds(0.5f);
        SpawnAllPlayers();
    }

    private void SpawnAllPlayers()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerSpawnManager] playerPrefab no asignado.");
            return;
        }

        int index = 0;
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (spawnedPlayers.ContainsKey(clientId)) continue;

            Vector3 spawnPos = GetSpawnPosition(index);
            SpawnPlayerForClient(clientId, spawnPos);
            index++;
        }
    }

    private void SpawnPlayerForClient(ulong clientId, Vector3 position)
    {
        GameObject player = Instantiate(playerPrefab, position, Quaternion.identity);
        NetworkObject netObj = player.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("[PlayerSpawnManager] El prefab no tiene NetworkObject.");
            Destroy(player);
            return;
        }

        // SpawnAsPlayerObject le da el ownership al cliente
        netObj.SpawnAsPlayerObject(clientId, destroyWithScene: true);
        spawnedPlayers[clientId] = player;

        Debug.Log($"[PlayerSpawnManager] Jugador spawneado para cliente {clientId} en {position}");
    }

    private Vector3 GetSpawnPosition(int index)
    {
        if (spawnPoints != null && spawnPoints.Length > index && spawnPoints[index] != null)
            return spawnPoints[index].position;

        // por si faltan spawn points
        return new Vector3(index * 2f, 0f, 0f);
    }
}
