using Unity.Netcode;
using UnityEngine;

/// <summary>
/// GameManager simplificado para la escena de juego (nivel).
/// La sesión ya está iniciada desde el Lobby.
/// Solo maneja eventos de desconexión durante el juego.
/// </summary>
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

        // Somos CLIENTE y se cayó nuestra conexión (el host cerró la sesión o
        // salió al menú) → limpiar la sesión y volver al menú principal.
        // En el host este callback llega cuando se va el cliente: ahí no
        // hacemos nada, el host puede seguir jugando o salir desde el menú.
        bool lostConnection = !nm.IsHost
            && (clientId == NetworkManager.ServerClientId || clientId == nm.LocalClientId);

        if (!lostConnection) return;

        handlingDisconnect = true;

        if (NetworkSessionManager.Instance != null)
            await NetworkSessionManager.Instance.LeaveSessionAsync();

        UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuScene);
    }
}
