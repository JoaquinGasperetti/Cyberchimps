using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private MultiplayerUI m_multiplayerUI;

    private ISession currentSession;

    private void Start()
    {
        if (m_multiplayerUI == null)
        {
            Debug.LogError("[GameManager] MultiplayerUI no asignado.");
            return;
        }

        m_multiplayerUI.OnStartHost += () => _ = StartHostAsync();
        m_multiplayerUI.OnStartClient += () => _ = StartClientAsync();
        m_multiplayerUI.OnDiconnectClient += () => _ = DisconnectAsync();

        _ = InitUGSAsync();
    }

    private async Task InitUGSAsync()
    {
        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Debug.Log($"[GameManager] UGS listo. ID: {AuthenticationService.Instance.PlayerId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameManager] Error en UGS init: {e.Message}");
        }
    }

    private async Task StartHostAsync()
    {
        m_multiplayerUI.DisableButtons();
        try
        {
            var options = new SessionOptions { MaxPlayers = 2 }.WithRelayNetwork();
            currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);

            string joinCode = currentSession.Code;
            m_multiplayerUI.ShowJoinCode(joinCode);

            NetworkManager.Singleton.StartHost();
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            Debug.Log($"[GameManager] Host iniciado. Código: {joinCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameManager] Error iniciando host: {e.Message}");
            m_multiplayerUI.EnableButtons();
        }
    }

    private async Task StartClientAsync()
    {
        string code = m_multiplayerUI.GetJoinCodeInput().Trim().ToUpper();

        if (string.IsNullOrWhiteSpace(code))
        {
            Debug.LogWarning("[GameManager] Ingresá el código de sesión.");
            return;
        }

        m_multiplayerUI.DisableButtons();
        try
        {
            // JoinSessionByCodeAsync no necesita WithRelayNetwork —
            // el relay se configura automáticamente desde los datos de la sesión existente
            currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);

            NetworkManager.Singleton.StartClient();
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            Debug.Log($"[GameManager] Cliente conectado. Código: {code}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameManager] Error conectando cliente: {e.Message}");
            m_multiplayerUI.EnableButtons();
        }
    }

    private async Task DisconnectAsync()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.Singleton.Shutdown();

        if (currentSession != null)
        {
            try { await currentSession.LeaveAsync(); }
            catch (Exception e) { Debug.LogWarning($"[GameManager] Error al salir: {e.Message}"); }
            currentSession = null;
        }

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