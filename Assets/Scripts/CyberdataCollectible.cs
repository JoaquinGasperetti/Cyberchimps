using UnityEngine;
using Unity.Netcode;

public class CyberdataCollectible : NetworkBehaviour
{
    private bool collected = false;

    private void OnTriggerEnter(Collider other)
    {
        // valida solo el server, si no se puede recolectar dos veces
        if (!IsServer) return;
        if (collected) return;
        if (!other.CompareTag("Player")) return;

        collected = true;

        CyberdataManager.Instance?.CollectCyberdata();

        PlayerCyberdataWallet wallet = other.GetComponentInParent<PlayerCyberdataWallet>();
        if (wallet != null)
        {
            wallet.AddCyberdata(1);
        }
        else
        {
            Debug.LogWarning("[CyberdataCollectible] El Player no tiene PlayerCyberdataWallet.");
        }

        // el despawn lo destruye en todos los clientes
        NetworkObject.Despawn(true);
    }
}
