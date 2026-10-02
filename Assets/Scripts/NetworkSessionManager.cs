using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

public class NetworkSessionManager : MonoBehaviour
{
    public static NetworkSessionManager Instance { get; private set; }

    public const int MaxPlayersPerSession = 2;

    // segundos que busca una partida publica antes de crear una propia
    private const float QuickMatchTimeoutSeconds = 8f;

    public ISession CurrentSession { get; private set; }

    // true si la sesion actual es publica (partida rapida / partida publica)
    public bool IsPublicSession { get; private set; }

    public bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
    public bool IsConnected => NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;
    public bool UGSReady { get; private set; }

    public event Action OnSessionStarted;
    public event Action OnSessionEnded;
    public event Action<ulong> OnPlayerConnected;
    public event Action<ulong> OnPlayerDisconnected;

    private Task initTask;

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

    // ------------------------------------------------------------------
    // UGS
    // ------------------------------------------------------------------

    public Task InitUGSAsync()
    {
        // si ya hay un intento en curso (o ya salio bien) se reutiliza;
        // si el ultimo fallo, se permite reintentar
        if (initTask == null || (initTask.IsCompleted && !UGSReady))
            initTask = InitUGSInternalAsync();

        return initTask;
    }

    private async Task InitUGSInternalAsync()
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

    private async Task EnsureUGSReadyAsync()
    {
        if (!UGSReady) await InitUGSAsync();
        if (!UGSReady) throw new Exception("No hay conexión con los servicios online.");
    }

    // si quedo una sesion vieja colgada, la cerramos antes de armar otra
    private async Task ReleaseOldSessionAsync()
    {
        if (CurrentSession != null || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening))
            await LeaveSessionAsync();
    }

    // ------------------------------------------------------------------
    // Crear / unirse
    // ------------------------------------------------------------------

    // Sesion PRIVADA: solo se entra con el codigo. No aparece en la lista publica.
    public async Task<string> CreateSessionAsync()
    {
        await EnsureUGSReadyAsync();
        await ReleaseOldSessionAsync();

        var options = new SessionOptions
        {
            MaxPlayers = MaxPlayersPerSession,
            IsPrivate = true
        }.WithRelayNetwork();

        CurrentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
        IsPublicSession = false;

        await BeginNetcodeAsync(asHost: true);

        Debug.Log($"[NetworkSessionManager] Sesión privada creada. Código: {CurrentSession.Code}");
        return CurrentSession.Code;
    }

    // Sesion PUBLICA: aparece en la lista y la encuentra la partida rapida.
    public async Task<string> CreatePublicSessionAsync(string sessionName = null)
    {
        await EnsureUGSReadyAsync();
        await ReleaseOldSessionAsync();

        var options = new SessionOptions
        {
            Name = sessionName ?? await BuildSessionNameAsync(),
            MaxPlayers = MaxPlayersPerSession,
            IsPrivate = false
        }.WithRelayNetwork();

        CurrentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
        IsPublicSession = true;

        await BeginNetcodeAsync(asHost: true);

        Debug.Log($"[NetworkSessionManager] Sesión pública creada: {CurrentSession.Name}");
        return CurrentSession.Code;
    }

    public async Task JoinSessionAsync(string code)
    {
        await EnsureUGSReadyAsync();
        await ReleaseOldSessionAsync();

        CurrentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpper());
        IsPublicSession = false;

        await BeginNetcodeAsync(asHost: false);

        Debug.Log($"[NetworkSessionManager] Unido a sesión con código: {code}");
    }

    public async Task JoinSessionByIdAsync(string sessionId)
    {
        await EnsureUGSReadyAsync();
        await ReleaseOldSessionAsync();

        CurrentSession = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId);
        IsPublicSession = true;

        await BeginNetcodeAsync(asHost: false);

        Debug.Log($"[NetworkSessionManager] Unido a sesión pública: {sessionId}");
    }

    // Partida rapida: se une a una sesion publica con lugar; si no hay ninguna,
    // crea una publica y queda esperando. Devuelve true si terminamos de host.
    public async Task<bool> QuickMatchAsync(string sessionName = null)
    {
        await EnsureUGSReadyAsync();
        await ReleaseOldSessionAsync();

        var quickJoinOptions = new QuickJoinOptions
        {
            Filters = new List<FilterOption>(),
            Timeout = TimeSpan.FromSeconds(QuickMatchTimeoutSeconds),
            CreateSession = true
        };

        var sessionOptions = new SessionOptions
        {
            Name = sessionName ?? await BuildSessionNameAsync(),
            MaxPlayers = MaxPlayersPerSession,
            IsPrivate = false
        }.WithRelayNetwork();

        CurrentSession = await MultiplayerService.Instance.MatchmakeSessionAsync(quickJoinOptions, sessionOptions);
        IsPublicSession = true;

        bool asHost = CurrentSession.IsHost;
        await BeginNetcodeAsync(asHost);

        Debug.Log($"[NetworkSessionManager] Partida rápida lista. Host: {asHost}");
        return asHost;
    }

    // Lista de partidas publicas con lugar, las mas nuevas primero
    public async Task<List<ISessionInfo>> QueryPublicSessionsAsync(int count = 20)
    {
        await EnsureUGSReadyAsync();

        var queryOptions = new QuerySessionsOptions
        {
            Count = count,
            FilterOptions = new List<FilterOption>
            {
                new FilterOption(FilterField.AvailableSlots, "0", FilterOperation.Greater)
            }
        };

        var results = await MultiplayerService.Instance.QuerySessionsAsync(queryOptions);

        return results.Sessions
            .Where(s => s.AvailableSlots > 0 && !s.HasPassword)
            .OrderByDescending(s => s.Created)
            .ToList();
    }

    // Bloquea / desbloquea la sesion para que nadie nuevo se una (solo host).
    // Se bloquea al empezar la partida para que no entren desconocidos a mitad de nivel.
    public async Task SetSessionLockedAsync(bool locked)
    {
        if (CurrentSession == null || !CurrentSession.IsHost) return;

        try
        {
            var hostSession = CurrentSession.AsHost();
            hostSession.IsLocked = locked;
            await hostSession.SavePropertiesAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NetworkSessionManager] No se pudo cambiar el bloqueo: {e.Message}");
        }
    }

    // ------------------------------------------------------------------
    // Salir
    // ------------------------------------------------------------------

    public async Task LeaveSessionAsync()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectedInternal;
            NetworkManager.Singleton.Shutdown();
        }

        var session = CurrentSession;
        CurrentSession = null;
        IsPublicSession = false;

        if (session != null)
        {
            try
            {
                // el host borra la sesion para que no quede fantasma en la lista publica
                if (session.IsHost) await session.AsHost().DeleteAsync();
                else await session.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NetworkSessionManager] Error al salir: {e.Message}");
                try { await session.LeaveAsync(); } catch { /* ya esta cerrada */ }
            }
        }

        OnSessionEnded?.Invoke();
        Debug.Log("[NetworkSessionManager] Sesión terminada.");
    }

    // ------------------------------------------------------------------
    // Netcode
    // ------------------------------------------------------------------

    private async Task BeginNetcodeAsync(bool asHost)
    {
        try
        {
            StartNetcode(asHost);
        }
        catch (Exception)
        {
            await LeaveSessionAsync();
            throw;
        }
    }

    private void StartNetcode(bool asHost)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) throw new Exception("No hay NetworkManager en la escena.");

        // sacar antes de poner evita duplicar callbacks si se arma mas de una sesion
        nm.OnClientConnectedCallback -= OnClientConnected;
        nm.OnClientDisconnectCallback -= OnClientDisconnectedInternal;
        nm.OnClientConnectedCallback += OnClientConnected;
        nm.OnClientDisconnectCallback += OnClientDisconnectedInternal;

        // con WithRelayNetwork() el SDK puede arrancar el NetworkManager solo;
        // si ya esta corriendo no lo arrancamos de nuevo
        if (!nm.IsListening)
        {
            if (asHost) nm.StartHost();
            else nm.StartClient();
        }

        OnSessionStarted?.Invoke();
    }

    private static async Task<string> BuildSessionNameAsync()
    {
        string playerName = null;

        try { playerName = await AuthenticationService.Instance.GetPlayerNameAsync(); }
        catch { /* sin nombre: usamos uno generico */ }

        if (string.IsNullOrEmpty(playerName)) return "Partida pública";

        // el nombre autogenerado viene como "Nombre#1234"
        int hash = playerName.IndexOf('#');
        if (hash > 0) playerName = playerName.Substring(0, hash);

        return $"Partida de {playerName}";
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