using UnityEngine;

public class CyberdataCollectible : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (CyberdataManager.Instance != null)
        {
            CyberdataManager.Instance.CollectCyberdata();
        }

        Destroy(gameObject);
    }
}