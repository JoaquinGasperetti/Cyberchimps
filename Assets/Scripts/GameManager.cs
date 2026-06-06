using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [SerializeField] private MultiplayerUI m_multiplayerUI;

    private void Start()
    {
        if (m_multiplayerUI == null) return;

        m_multiplayerUI.OnStartHost      += StartHost;
        m_multiplayerUI.OnStartClient    += StartClient;
        m_multiplayerUI.OnDiconnectClient += DisconnectClient;
    }

    private void StartHost()
    {
        m_multiplayerUI.DisableButtons();

        NetworkManager.Singleton.OnClientConnectedCallback  += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        NetworkManager.Singleton.StartHost();
        Debug.Log("[GameManager] Host iniciado.");
    }

    private void StartClient()
    {
        m_multiplayerUI.DisableButtons();

        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        NetworkManager.Singleton.StartClient();
        Debug.Log("[GameManager] Conectando como cliente...");
    }

    private void DisconnectClient()
    {
        NetworkManager.Singleton.OnClientConnectedCallback  -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

        NetworkManager.Singleton.Shutdown();

        m_multiplayerUI.EnableButtons();
        Debug.Log("[GameManager] Desconectado.");
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[GameManager] Cliente conectado: {clientId}");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[GameManager] Cliente desconectado: {clientId}");
        m_multiplayerUI?.EnableButtons();
    }
}
