using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private string lobbySceneName = "Lobby";

    // El botón "Jugar" ahora va al Lobby, no al LevelSelect
    public void StartGame()
    {
        SceneManager.LoadScene(lobbySceneName);
    }

    public void QuitGame()
    {
        Debug.Log("Quit Game");
        Application.Quit();
    }
}
