using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Colocar este script en cada escena de nivel (OnlineTest, etc).
/// El host lo ejecuta y spawnea un jugador por cada cliente conectado.
///
/// SETUP en Unity:
/// 1. Creá GameObjects vacíos en la escena como puntos de spawn,
///    nombralos "SpawnPoint1", "SpawnPoint2", etc.
/// 2. Agregá este script a un GameObject en la escena del nivel.
/// 3. Asigná el Player prefab y los spawn points en el Inspector.
/// 4. En el NetworkManager: dejá PlayerPrefab vacío y
///    AutoSpawnPlayerPrefabClientSide = false.
/// </summary>
public class PlayerSpawnManager : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;

    // Guarda los jugadores spawneados para no duplicar
    private readonly Dictionary<ulong, GameObject> spawnedPlayers = new();

    public override void OnNetworkSpawn()
    {
        // Solo el host spawnea jugadores
        if (!IsServer) return;

        StartCoroutine(SpawnAllPlayersDelayed());
    }

    private IEnumerator SpawnAllPlayersDelayed()
    {
        // Pequeño delay para asegurarse que todos los clientes cargaron la escena
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

        // SpawnAsPlayerObject asigna ownership al cliente correspondiente
        netObj.SpawnAsPlayerObject(clientId, destroyWithScene: true);
        spawnedPlayers[clientId] = player;

        Debug.Log($"[PlayerSpawnManager] Jugador spawneado para cliente {clientId} en {position}");
    }

    private Vector3 GetSpawnPosition(int index)
    {
        if (spawnPoints != null && spawnPoints.Length > index && spawnPoints[index] != null)
            return spawnPoints[index].position;

        // Fallback: posición por defecto separada lateralmente
        return new Vector3(index * 2f, 0f, 0f);
    }
}
