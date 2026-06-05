using UnityEngine;
using UnityEngine.SceneManagement;

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

            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.CompleteLevel();
            }

            Debug.Log("META ALCANZADA");
        }
    }
   

}