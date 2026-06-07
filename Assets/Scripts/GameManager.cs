using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// GameManager SIN Relay. Conexión directa por IP local (misma red WiFi).
/// El host ve su IP en pantalla, el cliente la escribe para conectarse.
/// Útil para demos, testing y juego en LAN.
/// </summary>
public class GameManager : MonoBehaviour
{
    [SerializeField] private MultiplayerUI m_multiplayerUI;

    [Header("Configuración de red")]
    [Tooltip("Puerto de conexión. Debe ser el mismo en host y cliente.")]
    [SerializeField] private ushort port = 7777;

    private void Start()
    {
        if (m_multiplayerUI == null) return;

        m_multiplayerUI.OnStartHost       += StartHost;
        m_multiplayerUI.OnStartClient     += StartClient;
        m_multiplayerUI.OnDiconnectClient += Disconnect;
    }

    // -------------------------------------------------------
    // HOST
    // -------------------------------------------------------

    private void StartHost()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData("0.0.0.0", port); // escucha en todas las interfaces

        NetworkManager.Singleton.OnClientConnectedCallback  += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        NetworkManager.Singleton.StartHost();

        // Mostrar la IP local para que el cliente se conecte
        string localIP = GetLocalIP();
        m_multiplayerUI.ShowJoinCode(localIP);
        m_multiplayerUI.DisableButtons();

        Debug.Log($"[GameManager] Host iniciado. IP local: {localIP} | Puerto: {port}");
    }

    // -------------------------------------------------------
    // CLIENTE
    // -------------------------------------------------------

    private void StartClient()
    {
        string hostIP = m_multiplayerUI.GetJoinCodeInput().Trim();

        if (string.IsNullOrWhiteSpace(hostIP))
        {
            Debug.LogWarning("[GameManager] Ingresá la IP del host antes de conectarte.");
            return;
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(hostIP, port);

        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        NetworkManager.Singleton.StartClient();
        m_multiplayerUI.DisableButtons();

        Debug.Log($"[GameManager] Conectando a {hostIP}:{port}...");
    }

    // -------------------------------------------------------
    // DESCONEXIÓN
    // -------------------------------------------------------

    private void Disconnect()
    {
        NetworkManager.Singleton.OnClientConnectedCallback  -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

        NetworkManager.Singleton.Shutdown();

        m_multiplayerUI.EnableButtons();
        m_multiplayerUI.ShowJoinCode("---");

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

    // -------------------------------------------------------
    // UTILIDAD
    // -------------------------------------------------------

    private static string GetLocalIP()
    {
        try
        {
            using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 65530);
            return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }
}
