using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private string levelSelectorSceneName = "LevelSelector";

    public void StartGame()
    {
        SceneManager.LoadScene(levelSelectorSceneName);
    }

    public void QuitGame()
    {
        Debug.Log("Quit Game");

        Application.Quit();
    }
}