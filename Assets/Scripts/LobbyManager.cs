using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    [Header("Paneles")]
    [SerializeField] private GameObject panelConnect;
    [SerializeField] private GameObject panelLoading;
    [SerializeField] private GameObject panelLobby;
    [Tooltip("Lista de partidas publicas. Opcional hasta que armes el panel.")]
    [SerializeField] private GameObject panelBrowser;

    [Header("Panel Connect")]
    [Tooltip("Crear sesion PRIVADA (se entra con codigo)")]
    [SerializeField] private Button buttonHost;
    [SerializeField] private Button buttonJoin;
    [SerializeField] private TMP_InputField inputCode;
    [SerializeField] private Button buttonQuickMatch;
    [SerializeField] private Button buttonCreatePublic;
    [SerializeField] private Button buttonBrowse;
    [Tooltip("Mensajes de error / aviso. Opcional.")]
    [SerializeField] private TMP_Text labelMessage;

    [Header("Panel Loading")]
    [SerializeField] private TMP_Text labelLoading;
    [SerializeField] private Image loadingSpinner;
    [Tooltip("Cancela la busqueda de partida rapida. Opcional.")]
    [SerializeField] private Button buttonCancelSearch;

    [Header("Panel Browser (partidas publicas)")]
    [Tooltip("Content del ScrollView donde se instancian las filas")]
    [SerializeField] private Transform browserListContainer;
    [SerializeField] private SessionListItemUI sessionItemPrefab;
    [SerializeField] private Button buttonRefresh;
    [SerializeField] private Button buttonBrowserBack;
    [SerializeField] private TMP_Text labelBrowserStatus;
    [SerializeField] private float browserAutoRefreshSeconds = 5f;

    [Header("Panel Lobby")]
    [SerializeField] private Button buttonDisconnect;
    [SerializeField] private Button buttonStartGame;
    [SerializeField] private TMP_Text labelCode;
    [SerializeField] private TMP_Text labelStatus;

    [Header("Modelos 3D en escena")]
    [Tooltip("Prefab del mesh del CyberChimp sin Rigidbody ni NetworkObject")]
    [SerializeField] private GameObject playerModelPrefab;
    [Tooltip("Slot del jugador 1 — siempre el HOST")]
    [SerializeField] private Transform player1Slot;
    [Tooltip("Slot del jugador 2 — siempre el CLIENTE")]
    [SerializeField] private Transform player2Slot;

    [Header("Animaciones de lobby")]
    [SerializeField] private string danceAnimTrigger = "Dance";
    [SerializeField] private string idleAnimBool = "IsIdle";

    [Header("Escenas")]
    [SerializeField] private string levelSelectScene = "LevelSelect";

    private GameObject model1Instance; // host
    private GameObject model2Instance; // cliente

    private bool secondPlayerConnected = false;
    private bool currentIsPublic = false;
    private Coroutine spinnerCoroutine;

    // evita doble click mientras hay una operacion online en curso
    private bool isBusy;
    // la busqueda de partida rapida no se puede abortar a mitad: se marca y se limpia al volver
    private bool searchCancelled;
    private bool leaving;

    private bool isRefreshingBrowser;
    private string browserNotice;
    private Coroutine browserRefreshCoroutine;
    private readonly List<GameObject> browserItems = new();

    private NetworkSessionManager Net => NetworkSessionManager.Instance;

    // ------------------------------------------------------------------
    // Ciclo de vida
    // ------------------------------------------------------------------

    private void Start()
    {
        Bind(buttonHost, OnHostClicked);
        Bind(buttonJoin, OnJoinClicked);
        Bind(buttonDisconnect, OnDisconnectClicked);
        Bind(buttonStartGame, OnStartGameClicked);

        Bind(buttonQuickMatch, OnQuickMatchClicked);
        Bind(buttonCreatePublic, OnCreatePublicClicked);
        Bind(buttonBrowse, OnBrowseClicked);
        Bind(buttonCancelSearch, OnCancelSearchClicked);
        Bind(buttonRefresh, OnRefreshClicked);
        Bind(buttonBrowserBack, OnBrowserBackClicked);

        // si volvemos con la sesion todavia viva, va el panel de lobby directo
        if (Net != null && Net.IsConnected)
        {
            RestoreActiveSession();
        }
        else
        {
            ShowConnectPanel();
        }

        if (Net != null)
        {
            Net.OnPlayerConnected += OnPlayerConnected;
            Net.OnPlayerDisconnected += OnPlayerDisconnected;
        }
    }

    private void OnDestroy()
    {
        if (Net != null)
        {
            Net.OnPlayerConnected -= OnPlayerConnected;
            Net.OnPlayerDisconnected -= OnPlayerDisconnected;
        }
    }

    private static void Bind(Button button, UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }

    private void RestoreActiveSession()
    {
        bool isHost = Net.IsHost;
        string code = Net.CurrentSession != null ? Net.CurrentSession.Code : "";
        currentIsPublic = Net.IsPublicSession;

        // ConnectedClientsList solo es confiable en el host
        secondPlayerConnected = !isHost
            || (NetworkManager.Singleton != null
                && NetworkManager.Singleton.ConnectedClientsList.Count > 1);

        ShowLobbyPanel(isHost, code, currentIsPublic);

        SpawnModel(player1Slot, ref model1Instance);
        if (secondPlayerConnected)
            SpawnModel(player2Slot, ref model2Instance);

        // se habia bloqueado al empezar la partida: al volver al lobby se reabre
        if (isHost) _ = Net.SetSessionLockedAsync(false);
    }

    // ------------------------------------------------------------------
    // Sesion privada (codigo)
    // ------------------------------------------------------------------

    private async void OnHostClicked()
    {
        if (isBusy) return;
        isBusy = true;
        ShowLoadingPanel("Creando sesión...", false);

        try
        {
            string code = await Net.CreateSessionAsync();
            if (this == null) return;

            EnterLobbyAsHost(code, isPublic: false);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyManager] Error al hostear: {e.Message}");
            if (this != null) ShowConnectPanel("No se pudo crear la sesión. Revisá tu conexión.");
        }
        finally
        {
            isBusy = false;
        }
    }

    private async void OnJoinClicked()
    {
        if (isBusy) return;

        string code = inputCode.text.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            Debug.LogWarning("[LobbyManager] Ingresá un código.");
            SetMessage("Ingresá un código.");
            return;
        }

        isBusy = true;
        ShowLoadingPanel("Uniéndose a la sesión...", false);

        try
        {
            await Net.JoinSessionAsync(code);
            if (this == null) return;

            EnterLobbyAsClient(code, isPublic: false);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyManager] Error al unirse: {e.Message}");
            if (this != null) ShowConnectPanel("No se pudo unir. Revisá el código.");
        }
        finally
        {
            isBusy = false;
        }
    }

    // ------------------------------------------------------------------
    // Partida rapida
    // ------------------------------------------------------------------

    private async void OnQuickMatchClicked()
    {
        if (isBusy) return;
        isBusy = true;
        searchCancelled = false;
        ShowLoadingPanel("Buscando partida...", true);

        try
        {
            bool isHost = await Net.QuickMatchAsync();
            if (this == null) return;

            // cancelo mientras buscaba: salimos de la sesion que se haya armado
            if (searchCancelled)
            {
                await Net.LeaveSessionAsync();
                if (this != null) ShowConnectPanel();
                return;
            }

            if (isHost) EnterLobbyAsHost(null, isPublic: true);
            else EnterLobbyAsClient(null, isPublic: true);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyManager] Error en partida rápida: {e.Message}");
            if (this != null) ShowConnectPanel("No se pudo buscar partida. Revisá tu conexión.");
        }
        finally
        {
            isBusy = false;
        }
    }

    private void OnCancelSearchClicked()
    {
        searchCancelled = true;
        if (labelLoading != null) labelLoading.text = "Cancelando...";
        if (buttonCancelSearch != null) buttonCancelSearch.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Partida publica
    // ------------------------------------------------------------------

    private async void OnCreatePublicClicked()
    {
        if (isBusy) return;
        isBusy = true;
        ShowLoadingPanel("Creando partida pública...", false);

        try
        {
            await Net.CreatePublicSessionAsync();
            if (this == null) return;

            EnterLobbyAsHost(null, isPublic: true);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyManager] Error al crear partida pública: {e.Message}");
            if (this != null) ShowConnectPanel("No se pudo crear la partida. Revisá tu conexión.");
        }
        finally
        {
            isBusy = false;
        }
    }

    // ------------------------------------------------------------------
    // Lista de partidas publicas
    // ------------------------------------------------------------------

    private void OnBrowseClicked()
    {
        if (isBusy) return;
        ShowBrowserPanel();
    }

    private void OnBrowserBackClicked()
    {
        if (isBusy) return;
        ShowConnectPanel();
    }

    private void OnRefreshClicked()
    {
        _ = RefreshBrowserAsync();
    }

    private IEnumerator BrowserRefreshRoutine()
    {
        while (true)
        {
            _ = RefreshBrowserAsync();
            yield return new WaitForSeconds(Mathf.Max(2f, browserAutoRefreshSeconds));
        }
    }

    private async Task RefreshBrowserAsync()
    {
        if (isRefreshingBrowser || sessionItemPrefab == null || browserListContainer == null) return;
        isRefreshingBrowser = true;

        if (browserItems.Count == 0) SetBrowserStatus("Buscando partidas...");

        try
        {
            List<Unity.Services.Multiplayer.ISessionInfo> sessions = await Net.QueryPublicSessionsAsync();

            // el panel se cerro mientras esperabamos la respuesta
            if (this == null || panelBrowser == null || !panelBrowser.activeInHierarchy) return;

            ClearBrowserItems();

            foreach (var s in sessions)
            {
                var item = Instantiate(sessionItemPrefab, browserListContainer);
                item.Setup(s.Id, s.Name, s.MaxPlayers - s.AvailableSlots, s.MaxPlayers, OnBrowserJoinClicked);
                browserItems.Add(item.gameObject);
            }

            string note = browserNotice;
            browserNotice = null;

            if (sessions.Count == 0)
                SetBrowserStatus(note ?? "No hay partidas públicas ahora. Creá una o probá Partida rápida.");
            else
                SetBrowserStatus(note ?? "");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LobbyManager] Error al listar partidas: {e.Message}");
            if (this != null) SetBrowserStatus("No se pudo buscar partidas. Revisá tu conexión.");
        }
        finally
        {
            isRefreshingBrowser = false;
        }
    }

    private async void OnBrowserJoinClicked(string sessionId)
    {
        if (isBusy) return;
        isBusy = true;

        StopBrowserRefresh();
        ShowLoadingPanel("Uniéndose a la partida...", false);

        try
        {
            await Net.JoinSessionByIdAsync(sessionId);
            if (this == null) return;

            EnterLobbyAsClient(null, isPublic: true);
        }
        catch (Exception e)
        {
            // lo mas comun: la partida se lleno o el host ya la cerro
            Debug.LogWarning($"[LobbyManager] No se pudo unir a la partida: {e.Message}");
            if (this != null) ShowBrowserPanel("No se pudo unir: la partida está llena o se cerró.");
        }
        finally
        {
            isBusy = false;
        }
    }

    private void StopBrowserRefresh()
    {
        if (browserRefreshCoroutine != null)
        {
            StopCoroutine(browserRefreshCoroutine);
            browserRefreshCoroutine = null;
        }
    }

    private void ClearBrowserItems()
    {
        foreach (var go in browserItems)
            if (go != null) Destroy(go);

        browserItems.Clear();
    }

    private void SetBrowserStatus(string text)
    {
        if (labelBrowserStatus != null) labelBrowserStatus.text = text;
    }

    // ------------------------------------------------------------------
    // Entrar / salir del lobby
    // ------------------------------------------------------------------

    private void EnterLobbyAsHost(string code, bool isPublic)
    {
        currentIsPublic = isPublic;

        // por si el segundo jugador entro justo antes de que se muestre el panel
        secondPlayerConnected = NetworkManager.Singleton != null
            && NetworkManager.Singleton.ConnectedClientsList.Count > 1;

        ShowLobbyPanel(isHost: true, code: code, isPublic: isPublic);

        // el host siempre va al slot 1
        SpawnModel(player1Slot, ref model1Instance);
        if (secondPlayerConnected)
            SpawnModel(player2Slot, ref model2Instance);
    }

    private void EnterLobbyAsClient(string code, bool isPublic)
    {
        currentIsPublic = isPublic;
        secondPlayerConnected = true;

        ShowLobbyPanel(isHost: false, code: code, isPublic: isPublic);

        // el cliente va al slot 2; el modelo del host se muestra igual
        SpawnModel(player1Slot, ref model1Instance);
        SpawnModel(player2Slot, ref model2Instance);
    }

    private async void OnDisconnectClicked()
    {
        await LeaveAndShowConnectPanel(null);
    }

    private async Task LeaveAndShowConnectPanel(string message)
    {
        if (leaving) return;
        leaving = true;

        try
        {
            DestroyAllModels();
            await Net.LeaveSessionAsync();
        }
        finally
        {
            leaving = false;
        }

        if (this == null) return;

        secondPlayerConnected = false;
        ShowConnectPanel(message);
    }

    private async void OnStartGameClicked()
    {
        if (!secondPlayerConnected)
        {
            Debug.LogWarning("[LobbyManager] Esperá que se conecte el segundo jugador.");
            return;
        }

        // cierra la sesion para que no se una nadie mas con la partida en marcha
        await Net.SetSessionLockedAsync(true);
        if (this == null) return;

        NetworkSceneLoader.Instance.LoadScene(levelSelectScene);
    }

    private void OnPlayerConnected(ulong clientId)
    {
        // esto solo lo recibe el host cuando alguien se une
        if (!Net.IsHost) return;
        if (clientId == NetworkManager.Singleton.LocalClientId) return;

        secondPlayerConnected = true;

        SpawnModel(player2Slot, ref model2Instance);

        UpdateLobbyStatus();
    }

    private async void OnPlayerDisconnected(ulong clientId)
    {
        if (leaving) return;

        var nm = NetworkManager.Singleton;
        bool isHost = nm != null && nm.IsHost;

        if (isHost)
        {
            // el callback del propio host al cerrar no nos interesa
            if (clientId == nm.LocalClientId) return;

            // se fue el cliente: sacamos su modelo
            secondPlayerConnected = false;
            DestroyModel(ref model2Instance);
            UpdateLobbyStatus();
            return;
        }

        // somos cliente y se cayo el host (o la conexion): volvemos al panel inicial
        await LeaveAndShowConnectPanel("El host cerró la partida.");
    }

    // ------------------------------------------------------------------
    // Modelos 3D
    // ------------------------------------------------------------------

    private void SpawnModel(Transform slot, ref GameObject modelRef)
    {
        if (playerModelPrefab == null || slot == null) return;

        if (modelRef != null) Destroy(modelRef);

        modelRef = Instantiate(playerModelPrefab, slot.position, slot.rotation, slot);

        Animator anim = modelRef.GetComponentInChildren<Animator>();
        if (anim == null) return;

        foreach (var param in anim.parameters)
        {
            if (param.name == idleAnimBool && param.type == AnimatorControllerParameterType.Bool)
            {
                anim.SetBool(idleAnimBool, true);
                break;
            }
        }

        foreach (var param in anim.parameters)
        {
            if (param.name == danceAnimTrigger && param.type == AnimatorControllerParameterType.Trigger)
            {
                anim.SetTrigger(danceAnimTrigger);
                break;
            }
        }
    }

    private void DestroyModel(ref GameObject modelRef)
    {
        if (modelRef != null) { Destroy(modelRef); modelRef = null; }
    }

    private void DestroyAllModels()
    {
        DestroyModel(ref model1Instance);
        DestroyModel(ref model2Instance);
    }

    // ------------------------------------------------------------------
    // Paneles
    // ------------------------------------------------------------------

    private void HideAllPanels()
    {
        panelConnect.SetActive(false);
        panelLoading.SetActive(false);
        panelLobby.SetActive(false);
        if (panelBrowser != null) panelBrowser.SetActive(false);

        StopBrowserRefresh();
    }

    private void ShowConnectPanel(string message = null)
    {
        HideAllPanels();
        panelConnect.SetActive(true);

        SetInteractable(buttonHost, true);
        SetInteractable(buttonJoin, true);
        SetInteractable(buttonQuickMatch, true);
        SetInteractable(buttonCreatePublic, true);
        SetInteractable(buttonBrowse, true);
        inputCode.text = "";

        SetMessage(message ?? "");

        StopSpinner();
        DestroyAllModels();
        ClearBrowserItems();
    }

    private void ShowLoadingPanel(string message, bool showCancel)
    {
        HideAllPanels();
        panelLoading.SetActive(true);

        if (labelLoading != null) labelLoading.text = message;
        if (buttonCancelSearch != null) buttonCancelSearch.gameObject.SetActive(showCancel);
        StartSpinner();
    }

    private void ShowBrowserPanel(string notice = null)
    {
        if (panelBrowser == null)
        {
            Debug.LogWarning("[LobbyManager] Falta asignar panelBrowser.");
            return;
        }

        HideAllPanels();
        StopSpinner();
        panelBrowser.SetActive(true);

        browserNotice = notice;
        browserRefreshCoroutine = StartCoroutine(BrowserRefreshRoutine());
    }

    private void ShowLobbyPanel(bool isHost, string code, bool isPublic)
    {
        HideAllPanels();
        panelLobby.SetActive(true);

        StopSpinner();

        if (labelCode != null)
        {
            if (!isHost) labelCode.text = "Conectado";
            else labelCode.text = isPublic ? "Partida pública" : $"Código: {code}";
        }

        buttonStartGame.gameObject.SetActive(isHost);
        UpdateLobbyStatus();
    }

    private void UpdateLobbyStatus()
    {
        if (labelStatus == null) return;

        bool isHost = Net != null && Net.IsHost;

        if (isHost)
        {
            labelStatus.text = secondPlayerConnected
                ? "✓ Jugador 2 conectado — podés comenzar"
                : (currentIsPublic
                    ? "Esperando que se una otro jugador..."
                    : "Esperando al segundo jugador...");

            if (buttonStartGame != null)
                buttonStartGame.interactable = secondPlayerConnected;
        }
        else
        {
            labelStatus.text = "Conectado — esperando al host...";
        }
    }

    private void SetMessage(string text)
    {
        if (labelMessage != null) labelMessage.text = text;
    }

    private static void SetInteractable(Button button, bool value)
    {
        if (button != null) button.interactable = value;
    }

    // ------------------------------------------------------------------
    // Spinner
    // ------------------------------------------------------------------

    private void StartSpinner()
    {
        if (loadingSpinner == null) return;
        StopSpinner();
        spinnerCoroutine = StartCoroutine(SpinnerRoutine());
    }

    private void StopSpinner()
    {
        if (spinnerCoroutine != null)
        {
            StopCoroutine(spinnerCoroutine);
            spinnerCoroutine = null;
        }
    }

    private IEnumerator SpinnerRoutine()
    {
        while (true)
        {
            loadingSpinner.transform.Rotate(0f, 0f, -180f * Time.deltaTime);
            yield return null;
        }
    }
}