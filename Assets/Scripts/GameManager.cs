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

public class GameManager : MonoBehaviour
{
    [SerializeField] private MultiplayerUI m_multiplayerUI;

    private const int MaxConnections = 1;

    private async void Start()
    {
        await InitializeUGS();

        if (m_multiplayerUI == null) return;

        m_multiplayerUI.OnStartHost += () => _ = StartHostWithRelay();
        m_multiplayerUI.OnStartClient += () => _ = StartClientWithRelay();
        m_multiplayerUI.OnDiconnectClient += Disconnect;
    }

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

    private async Task StartHostWithRelay()
    {
        m_multiplayerUI.DisableButtons();

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            Debug.Log($"[GameManager] Join Code: {joinCode}");
            m_multiplayerUI.ShowJoinCode(joinCode);

            // API nueva: AllocationUtils en lugar del constructor directo
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

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
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim().ToUpper());

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, "dtls"));

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