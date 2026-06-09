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

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[GameManager] Cliente desconectado: {clientId}");

        // Si se desconectó el otro jugador, podés volver al lobby
        // Descomenta la siguiente línea si querés eso:
        // NetworkSceneLoader.Instance?.LoadScene(lobbyScene);
    }
}
