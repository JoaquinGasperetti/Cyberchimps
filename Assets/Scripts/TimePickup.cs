using UnityEngine;

public class TimePickup : MonoBehaviour
{
    [SerializeField] private float timeToAdd = 5f;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.AddTime(timeToAdd);
        }

        Destroy(gameObject);
    }
}