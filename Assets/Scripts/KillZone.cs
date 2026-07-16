using Unity.Netcode;
using UnityEngine;

public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // las vidas se descuentan solo en el server
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (!other.CompareTag("Player")) return;

        PlayerLives playerLives = other.GetComponentInParent<PlayerLives>();
        playerLives?.LoseLifeFromServer();
    }
}
