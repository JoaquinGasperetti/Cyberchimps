using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Maneja la escena Lobby.
/// 
/// SETUP en la escena Lobby:
/// Creá un Canvas con estos elementos y asignalos en el Inspector:
///   - PanelConnect:   panel inicial con botones Host/Join
///   - PanelLobby:     panel que se muestra una vez conectado
///   - ButtonHost:     botón para hostear
///   - ButtonJoin:     botón para unirse
///   - ButtonDisconnect
///   - ButtonStartGame: solo visible para el host, arranca el juego
///   - InputCode:      TMP_InputField para ingresar el código
///   - LabelCode:      TMP_Text que muestra el código al host
///   - LabelStatus:    TMP_Text que muestra estado ("Esperando jugador..." / "Listo!")
/// </summary>
public class LobbyManager : MonoBehaviour
{
    [Header("Paneles")]
    [SerializeField] private GameObject panelConnect;
    [SerializeField] private GameObject panelLobby;

    [Header("Botones - Panel Connect")]
    [SerializeField] private Button buttonHost;
    [SerializeField] private Button buttonJoin;
    [SerializeField] private TMP_InputField inputCode;

    [Header("Botones - Panel Lobby")]
    [SerializeField] private Button buttonDisconnect;
    [SerializeField] private Button buttonStartGame;

    [Header("Labels - Panel Lobby")]
    [SerializeField] private TMP_Text labelCode;
    [SerializeField] private TMP_Text labelStatus;

    [Header("Escenas")]
    [SerializeField] private string levelSelectScene = "LevelSelect";

    private bool secondPlayerConnected = false;

    private void Start()
    {
        buttonHost.onClick.AddListener(OnHostClicked);
        buttonJoin.onClick.AddListener(OnJoinClicked);
        buttonDisconnect.onClick.AddListener(OnDisconnectClicked);
        buttonStartGame.onClick.AddListener(OnStartGameClicked);

        ShowConnectPanel();

        // Suscribirse a eventos del NetworkSessionManager
        if (NetworkSessionManager.Instance != null)
        {
            NetworkSessionManager.Instance.OnPlayerConnected    += OnPlayerConnected;
            NetworkSessionManager.Instance.OnPlayerDisconnected += OnPlayerDisconnected;
        }
    }

    private void OnDestroy()
    {
        if (NetworkSessionManager.Instance != null)
        {
            NetworkSessionManager.Instance.OnPlayerConnected    -= OnPlayerConnected;
            NetworkSessionManager.Instance.OnPlayerDisconnected -= OnPlayerDisconnected;
        }
    }

    // -------------------------------------------------------
    // BOTONES
    // -------------------------------------------------------

    private async void OnHostClicked()
    {
        buttonHost.interactable = false;
        buttonJoin.interactable = false;

        try
        {
            string code = await NetworkSessionManager.Instance.CreateSessionAsync();
            ShowLobbyPanel(isHost: true, code: code);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyManager] Error al hostear: {e.Message}");
            buttonHost.interactable = true;
            buttonJoin.interactable = true;
        }
    }

    private async void OnJoinClicked()
    {
        string code = inputCode.text.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            Debug.LogWarning("[LobbyManager] Ingresá un código.");
            return;
        }

        buttonHost.interactable = false;
        buttonJoin.interactable = false;

        try
        {
            await NetworkSessionManager.Instance.JoinSessionAsync(code);
            ShowLobbyPanel(isHost: false, code: code);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyManager] Error al unirse: {e.Message}");
            buttonHost.interactable = true;
            buttonJoin.interactable = true;
        }
    }

    private async void OnDisconnectClicked()
    {
        await NetworkSessionManager.Instance.LeaveSessionAsync();
        secondPlayerConnected = false;
        ShowConnectPanel();
    }

    private void OnStartGameClicked()
    {
        if (!secondPlayerConnected)
        {
            Debug.LogWarning("[LobbyManager] Esperá que se conecte el segundo jugador.");
            return;
        }

        // El host carga la escena de selección de nivel para todos
        NetworkSceneLoader.Instance.LoadScene(levelSelectScene);
    }

    // -------------------------------------------------------
    // EVENTOS DE RED
    // -------------------------------------------------------

    private void OnPlayerConnected(ulong clientId)
    {
        // Cuando se conecta un segundo jugador (clientId != 0 es el cliente, 0 es el host)
        if (clientId != NetworkManager.Singleton.LocalClientId)
        {
            secondPlayerConnected = true;
            UpdateLobbyStatus();
        }
    }

    private void OnPlayerDisconnected(ulong clientId)
    {
        secondPlayerConnected = false;
        UpdateLobbyStatus();
    }

    // -------------------------------------------------------
    // UI
    // -------------------------------------------------------

    private void ShowConnectPanel()
    {
        panelConnect.SetActive(true);
        panelLobby.SetActive(false);
        buttonHost.interactable = true;
        buttonJoin.interactable = true;
        inputCode.text = "";
    }

    private void ShowLobbyPanel(bool isHost, string code)
    {
        panelConnect.SetActive(false);
        panelLobby.SetActive(true);

        if (isHost)
        {
            labelCode.text = $"Código: {code}";
            buttonStartGame.gameObject.SetActive(true);
        }
        else
        {
            labelCode.text = $"Conectado";
            buttonStartGame.gameObject.SetActive(false);
        }

        UpdateLobbyStatus();
    }

    private void UpdateLobbyStatus()
    {
        if (labelStatus == null) return;

        bool isHost = NetworkSessionManager.Instance != null && NetworkSessionManager.Instance.IsHost;

        if (isHost)
        {
            labelStatus.text = secondPlayerConnected
                ? "✓ Jugador conectado — podés comenzar"
                : "Esperando al segundo jugador...";

            if (buttonStartGame != null)
                buttonStartGame.interactable = secondPlayerConnected;
        }
        else
        {
            labelStatus.text = "Conectado — esperando al host...";
        }
    }
}
