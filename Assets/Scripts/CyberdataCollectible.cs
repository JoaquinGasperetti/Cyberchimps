using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Moneda (Cyberdato) sincronizada en red.
/// REQUERIDO en el prefab: NetworkObject.
/// Agregarlo a la lista de Network Prefabs del NetworkManager.
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

        // Notificar a todos los clientes para actualizar la UI
        CollectClientRpc();

        // Despawn destruye el objeto en todos los clientes automáticamente
        NetworkObject.Despawn(true);
    }

    [ClientRpc]
    private void CollectClientRpc()
    {
        CyberdataManager.Instance?.CollectCyberdata();
    }
}
