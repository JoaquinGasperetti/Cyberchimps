using Unity.Netcode;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private string lobbyScene = "Lobby";
    [SerializeField] private string mainMenuScene = "MainMenu";

    private bool handlingDisconnect;

    private void Start()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private async void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[GameManager] Cliente desconectado: {clientId}");

        var nm = NetworkManager.Singleton;
        if (nm == null || handlingDisconnect) return;

        // si somos cliente y se corto la conexion, limpiamos y volvemos al menu;
        // al host no le hacemos nada cuando se va el cliente
        bool lostConnection = !nm.IsHost
            && (clientId == NetworkManager.ServerClientId || clientId == nm.LocalClientId);

        if (!lostConnection) return;

        handlingDisconnect = true;

        if (NetworkSessionManager.Instance != null)
            await NetworkSessionManager.Instance.LeaveSessionAsync();

        UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuScene);
    }
}
