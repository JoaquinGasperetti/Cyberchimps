using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Moneda (Cyberdato) sincronizada en red.
/// REQUERIDO en el prefab: NetworkObject (ya lo tiene Cyberdato.prefab).
/// Agregarlo a la lista de Network Prefabs del NetworkManager.
///
/// CAMBIO: además de sumar al contador COMPARTIDO del nivel (CyberdataManager,
/// usado para la estrella de "todos los cyberdatos"), ahora también acredita
/// al monedero PERSONAL (PlayerCyberdataWallet) del jugador específico que
/// lo recolectó — cada jugador junta y guarda los suyos por separado.
/// </summary>
public class CyberdataCollectible : NetworkBehaviour
{
    private bool collected = false;

    private void OnTriggerEnter(Collider other)
    {
        // Solo el servidor valida colisiones para evitar doble-recolección
        if (!IsServer) return;
        if (collected) return;
        if (!other.CompareTag("Player")) return;

        collected = true;

        // Progreso compartido del nivel (estrella de "todos los cyberdatos")
        CyberdataManager.Instance?.CollectCyberdata();

        // Monedero personal del jugador que lo tocó
        PlayerCyberdataWallet wallet = other.GetComponentInParent<PlayerCyberdataWallet>();
        if (wallet != null)
        {
            wallet.AddCyberdata(1);
        }
        else
        {
            Debug.LogWarning("[CyberdataCollectible] El Player no tiene PlayerCyberdataWallet.");
        }

        // Despawn destruye el objeto en todos los clientes automáticamente
        NetworkObject.Despawn(true);
    }
}
