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
        var nm = NetworkManager.Singleton;

        if (nm == null || !nm.IsHost)
        {
            Debug.LogWarning("[NetworkSceneLoader] Solo el host puede cargar escenas.");
            return;
        }

        // una escena que no esta en Build Settings hace que NGO aborte el evento
        // de carga y la sesion queda a medias: mejor cortar antes y avisar
        if (!SceneExistsInBuild(sceneName))
        {
            Debug.LogError($"[NetworkSceneLoader] La escena '{sceneName}' no esta en Build Settings. " +
                           "Agregala en File > Build Settings o corregi el nombre del boton.");
            return;
        }

        var status = nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
            Debug.LogError($"[NetworkSceneLoader] No se pudo cargar '{sceneName}': {status}");
    }

    public static bool SceneExistsInBuild(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                return true;
        }
        return false;
    }
}
