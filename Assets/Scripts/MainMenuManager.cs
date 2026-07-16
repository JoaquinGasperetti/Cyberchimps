using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private string lobbySceneName = "Lobby";

    // "Jugar" va al lobby, no al selector de niveles
    public void StartGame()
    {
        SceneManager.LoadScene(lobbySceneName);
    }

    public void OpenOptions()
    {
        OptionsMenuUI.Show();
    }

    public void QuitGame()
    {
        Debug.Log("Quit Game");
        Application.Quit();
    }
}
