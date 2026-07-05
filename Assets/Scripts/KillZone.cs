using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Zona letal: cuando un jugador la toca, pierde una vida y respawnea
/// (ver PlayerLives). Usada por el agua del nivel 1 ("Sea").
///
/// SETUP:
///   - Collider con Is Trigger activado en el mismo GameObject.
///   - Solo el servidor procesa el trigger (las vidas son server-authoritative).
/// </summary>
public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Las vidas se descuentan solo en el servidor
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (!other.CompareTag("Player")) return;

        PlayerLives playerLives = other.GetComponentInParent<PlayerLives>();
        playerLives?.LoseLifeFromServer();
    }
}
