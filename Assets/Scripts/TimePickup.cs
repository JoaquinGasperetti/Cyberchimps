using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Bonus de tiempo (prefab "BonusItem"). Suma tiempo al LevelTimer compartido,
/// visible/sincronizado para AMBOS jugadores.
///
/// REQUERIDO en el prefab BonusItem:
///  - Agregar un componente NetworkObject (igual que tiene Cyberdato.prefab).
///  - Agregar el prefab a la lista de Network Prefabs del NetworkManager.
///
/// Solo el servidor valida la colisión y aplica el bonus (evita duplicar el
/// bonus si por lag ambos clientes detectan el trigger).
/// </summary>
public class TimePickup : NetworkBehaviour
{
    [SerializeField] private float timeToAdd = 5f;

    private bool collected = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (collected) return;
        if (!other.CompareTag("Player")) return;

        collected = true;

        if (LevelTimer.Instance != null)
        {
            LevelTimer.Instance.AddBonusTime(timeToAdd);
        }

        // Despawn sincroniza la destrucción en todos los clientes.
        NetworkObject.Despawn(true);
    }
}
