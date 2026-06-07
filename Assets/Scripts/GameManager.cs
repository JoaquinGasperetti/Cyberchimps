using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.UIElements;

public class GameManager : MonoBehaviour
{
    [SerializeField] private MultiplayerUI m_multiplayerUI;

    // Máximo 2 jugadores (host + 1 cliente)
    private const int MaxConnections = 1;

    private async void Start()
    {
        // Inicializar Unity Gaming Services
        await InitializeUGS();

        if (m_multiplayerUI == null) return;

        m_multiplayerUI.OnStartHost       += () => _ = StartHostWithRelay();
        m_multiplayerUI.OnStartClient     += () => _ = StartClientWithRelay();
        m_multiplayerUI.OnDiconnectClient += Disconnect;
    }

    // -------------------------------------------------------
    // INICIALIZACIÓN
    // -------------------------------------------------------

    private async Task InitializeUGS()
    {
        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Debug.Log($"[GameManager] UGS listo. PlayerID: {AuthenticationService.Instance.PlayerId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameManager] Error inicializando UGS: {e.Message}");
        }
    }

    // -------------------------------------------------------
    // HOST
    // -------------------------------------------------------

    private async Task StartHostWithRelay()
    {
        m_multiplayerUI.DisableButtons();

        try
        {
            // Crear asignación Relay para 1 cliente
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections);

            // Obtener el Join Code que el cliente necesita
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            Debug.Log($"[GameManager] Join Code: {joinCode}");

            // Mostrar el código en la UI
            m_multiplayerUI.ShowJoinCode(joinCode);

            // Configurar el transport con los datos de Relay
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            NetworkManager.Singleton.StartHost();

            Debug.Log("[GameManager] Host iniciado con Relay.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameManager] Error iniciando host: {e.Message}");
            m_multiplayerUI.EnableButtons();
        }
    }

    // -------------------------------------------------------
    // CLIENTE
    // -------------------------------------------------------

    private async Task StartClientWithRelay()
    {
        string joinCode = m_multiplayerUI.GetJoinCodeInput();

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            Debug.LogWarning("[GameManager] Ingresá el Join Code antes de conectarte.");
            return;
        }

        m_multiplayerUI.DisableButtons();

        try
        {
            // Unirse a la asignación Relay usando el código
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim().ToUpper());

            // Configurar el transport con los datos de Relay
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            NetworkManager.Singleton.StartClient();

            Debug.Log("[GameManager] Cliente conectado con Relay.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameManager] Error conectando cliente: {e.Message}");
            m_multiplayerUI.EnableButtons();
        }
    }

    // -------------------------------------------------------
    // DESCONEXIÓN
    // -------------------------------------------------------

    private void Disconnect()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.Singleton.Shutdown();
        m_multiplayerUI.EnableButtons();
        m_multiplayerUI.ShowJoinCode("---");
        Debug.Log("[GameManager] Desconectado.");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[GameManager] Cliente desconectado: {clientId}");
        m_multiplayerUI?.EnableButtons();
    }
}
