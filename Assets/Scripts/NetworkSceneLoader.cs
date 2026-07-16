using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkSceneLoader : MonoBehaviour
{
    public static NetworkSceneLoader Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadScene(string sceneName)
    {
        if (!NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("[NetworkSceneLoader] Solo el host puede cargar escenas.");
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
