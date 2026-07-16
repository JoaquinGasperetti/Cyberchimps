using UnityEngine;
using Unity.Netcode;

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

        // el despawn lo destruye en todos los clientes
        NetworkObject.Despawn(true);
    }
}
