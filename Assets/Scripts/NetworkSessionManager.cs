using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

/// <summary>
/// Singleton persistente que maneja toda la sesión de red.
/// Vive en DontDestroyOnLoad y es accesible desde cualquier escena.
/// </summary>
public class NetworkSessionManager : MonoBehaviour
{
    public static NetworkSessionManager Instance { get; private set; }

    public ISession CurrentSession { get; private set; }
    public bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
    public bool IsConnected => NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;
    public bool UGSReady { get; private set; }

    public event Action OnSessionStarted;
    public event Action OnSessionEnded;
    public event Action<ulong> OnPlayerConnected;
    public event Action<ulong> OnPlayerDisconnected;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        await InitUGSAsync();
    }

    public async Task InitUGSAsync()
    {
        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            UGSReady = true;
            Debug.Log($"[NetworkSessionManager] UGS listo. ID: {AuthenticationService.Instance.PlayerId}");
        }
        catch (Exception e)
        {
            UGSReady = false;
            Debug.LogError($"[NetworkSessionManager] Error UGS: {e.Message}");
        }
    }

    public async Task<string> CreateSessionAsync()
    {
        if (!UGSReady) throw new Exception("UGS no está listo.");

        var options = new SessionOptions { MaxPlayers = 2 }.WithRelayNetwork();
        CurrentSession = await MultiplayerService.Instance.CreateSessionAsync(options);

        NetworkManager.Singleton.OnClientConnectedCallback  += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectedInternal;

        NetworkManager.Singleton.StartHost();

        OnSessionStarted?.Invoke();
        Debug.Log($"[NetworkSessionManager] Sesión creada. Código: {CurrentSession.Code}");
        return CurrentSession.Code;
    }

    public async Task JoinSessionAsync(string code)
    {
        if (!UGSReady) throw new Exception("UGS no está listo.");

        CurrentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpper());

        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectedInternal;

        NetworkManager.Singleton.StartClient();

        OnSessionStarted?.Invoke();
        Debug.Log($"[NetworkSessionManager] Unido a sesión con código: {code}");
    }

    public async Task LeaveSessionAsync()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback  -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectedInternal;
            NetworkManager.Singleton.Shutdown();
        }

        if (CurrentSession != null)
        {
            try { await CurrentSession.LeaveAsync(); }
            catch (Exception e) { Debug.LogWarning($"[NetworkSessionManager] Error al salir: {e.Message}"); }
            CurrentSession = null;
        }

        OnSessionEnded?.Invoke();
        Debug.Log("[NetworkSessionManager] Sesión terminada.");
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NetworkSessionManager] Cliente conectado: {clientId}");
        OnPlayerConnected?.Invoke(clientId);
    }

    private void OnClientDisconnectedInternal(ulong clientId)
    {
        Debug.Log($"[NetworkSessionManager] Cliente desconectado: {clientId}");
        OnPlayerDisconnected?.Invoke(clientId);
    }
}
