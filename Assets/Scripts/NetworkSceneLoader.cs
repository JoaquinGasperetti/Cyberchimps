using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton persistente que carga escenas en red para todos los clientes.
/// El host es el único que puede llamar a LoadScene().
/// </summary>
public class NetworkSceneLoader : MonoBehaviour
{
    public static NetworkSceneLoader Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Carga una escena para todos los jugadores conectados.
    /// Solo el host puede llamar esto.
    /// </summary>
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
