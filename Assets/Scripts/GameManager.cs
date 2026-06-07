using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// GameManager con conexión directa por IP (LAN/WiFi).
/// Para juego por internet entre redes distintas, se necesita Relay o port forwarding.
///
/// Flujo:
/// - Host presiona "Start Host" → ve su IP local en pantalla
/// - Cliente escribe esa IP en el input y presiona "Start Client"
/// - Ambos se conectan en la misma red WiFi
/// </summary>
public class GameManager : MonoBehaviour
{
    [SerializeField] private MultiplayerUI m_multiplayerUI;

    [Header("Red")]
    [SerializeField] private ushort port = 7777;

    private void Start()
    {
        if (m_multiplayerUI == null)
        {
            Debug.LogError("[GameManager] MultiplayerUI no asignado en el Inspector.");
            return;
        }

        m_multiplayerUI.OnStartHost       += StartHost;
        m_multiplayerUI.OnStartClient     += StartClient;
        m_multiplayerUI.OnDiconnectClient += Disconnect;
    }

    private void StartHost()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData("0.0.0.0", port);

        NetworkManager.Singleton.OnClientConnectedCallback  += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        bool started = NetworkManager.Singleton.StartHost();

        if (started)
        {
            string ip = GetLocalIP();
            m_multiplayerUI.ShowJoinCode(ip);
            m_multiplayerUI.DisableButtons();
            Debug.Log($"[GameManager] Host iniciado. IP: {ip} | Puerto: {port}");
        }
        else
        {
            Debug.LogError("[GameManager] No se pudo iniciar el Host.");
        }
    }

    private void StartClient()
    {
        string hostIP = m_multiplayerUI.GetJoinCodeInput().Trim();

        if (string.IsNullOrWhiteSpace(hostIP))
        {
            Debug.LogWarning("[GameManager] Ingresá la IP del host.");
            return;
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(hostIP, port);

        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        bool started = NetworkManager.Singleton.StartClient();

        if (started)
        {
            m_multiplayerUI.DisableButtons();
            Debug.Log($"[GameManager] Conectando a {hostIP}:{port}...");
        }
        else
        {
            Debug.LogError("[GameManager] No se pudo iniciar el Cliente.");
        }
    }

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

    private static string GetLocalIP()
    {
        try
        {
            using Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.Connect("8.8.8.8", 65530);
            return (s.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "127.0.0.1";
        }
        catch { return "127.0.0.1"; }
    }
}
