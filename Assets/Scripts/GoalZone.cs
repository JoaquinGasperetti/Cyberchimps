using UnityEngine;

public class GoalZone : MonoBehaviour
{
    private bool completed;

    private void OnTriggerEnter(Collider other)
    {
        if (completed)
            return;

        if (other.CompareTag("Player"))
        {
            completed = true;

            if (LevelTimer.Instance != null)
            {
                LevelTimer.Instance.StopTimer();
            }

            Debug.Log("META ALCANZADA");
        }
    }
}