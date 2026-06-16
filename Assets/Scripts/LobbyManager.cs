using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    [Header("Paneles")]
    [SerializeField] private GameObject panelConnect;
    [SerializeField] private GameObject panelLoading;
    [SerializeField] private GameObject panelLobby;

    [Header("Panel Connect")]
    [SerializeField] private Button buttonHost;
    [SerializeField] private Button buttonJoin;
    [SerializeField] private TMP_InputField inputCode;

    [Header("Panel Loading")]
    [SerializeField] private TMP_Text labelLoading;
    [SerializeField] private Image loadingSpinner;

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
    private Coroutine spinnerCoroutine;

    // -------------------------------------------------------
    // INIT
    // -------------------------------------------------------

    private void Start()
    {
        buttonHost.onClick.AddListener(OnHostClicked);
        buttonJoin.onClick.AddListener(OnJoinClicked);
        buttonDisconnect.onClick.AddListener(OnDisconnectClicked);
        buttonStartGame.onClick.AddListener(OnStartGameClicked);

        ShowConnectPanel();

        if (NetworkSessionManager.Instance != null)
        {
            NetworkSessionManager.Instance.OnPlayerConnected += OnPlayerConnected;
            NetworkSessionManager.Instance.OnPlayerDisconnected += OnPlayerDisconnected;
        }
    }

    private void OnDestroy()
    {
        if (NetworkSessionManager.Instance != null)
        {
            NetworkSessionManager.Instance.OnPlayerConnected -= OnPlayerConnected;
            NetworkSessionManager.Instance.OnPlayerDisconnected -= OnPlayerDisconnected;
        }
    }

    // -------------------------------------------------------
    // BOTONES
    // -------------------------------------------------------

    private async void OnHostClicked()
    {
        ShowLoadingPanel("Creando sesión...");

        try
        {
            string code = await NetworkSessionManager.Instance.CreateSessionAsync();
            ShowLobbyPanel(isHost: true, code: code);

            // El host siempre ocupa el Slot 1
            SpawnModel(player1Slot, ref model1Instance);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyManager] Error al hostear: {e.Message}");
            ShowConnectPanel();
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

        ShowLoadingPanel("Uniéndose a la sesión...");

        try
        {
            await NetworkSessionManager.Instance.JoinSessionAsync(code);
            ShowLobbyPanel(isHost: false, code: code);

            // El cliente siempre ocupa el Slot 2
            // El Slot 1 (host) también se muestra para que el cliente vea ambos
            SpawnModel(player1Slot, ref model1Instance); // modelo del host (decorativo)
            SpawnModel(player2Slot, ref model2Instance); // modelo del cliente (vos)
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyManager] Error al unirse: {e.Message}");
            ShowConnectPanel();
        }
    }

    private async void OnDisconnectClicked()
    {
        DestroyAllModels();
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

        NetworkSceneLoader.Instance.LoadScene(levelSelectScene);
    }

    // -------------------------------------------------------
    // EVENTOS DE RED
    // -------------------------------------------------------

    private void OnPlayerConnected(ulong clientId)
    {
        // Este evento solo lo recibe el HOST cuando alguien se une
        if (!NetworkSessionManager.Instance.IsHost) return;
        if (clientId == NetworkManager.Singleton.LocalClientId) return;

        secondPlayerConnected = true;

        // Aparece el modelo del cliente en el Slot 2
        SpawnModel(player2Slot, ref model2Instance);

        UpdateLobbyStatus();
    }

    private void OnPlayerDisconnected(ulong clientId)
    {
        secondPlayerConnected = false;

        // Si el host ve que el cliente se fue, quitamos su modelo
        if (NetworkSessionManager.Instance.IsHost)
            DestroyModel(ref model2Instance);

        UpdateLobbyStatus();
    }

    // -------------------------------------------------------
    // MODELOS 3D
    // -------------------------------------------------------

    private void SpawnModel(Transform slot, ref GameObject modelRef)
    {
        if (playerModelPrefab == null || slot == null) return;

        if (modelRef != null) Destroy(modelRef);

        modelRef = Instantiate(playerModelPrefab, slot.position, slot.rotation, slot);

        Animator anim = modelRef.GetComponentInChildren<Animator>();
        if (anim == null) return;

        // Activar idle
        foreach (var param in anim.parameters)
        {
            if (param.name == idleAnimBool && param.type == AnimatorControllerParameterType.Bool)
            {
                anim.SetBool(idleAnimBool, true);
                break;
            }
        }

        // Activar baile si existe el trigger
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

    // -------------------------------------------------------
    // UI
    // -------------------------------------------------------

    private void ShowConnectPanel()
    {
        panelConnect.SetActive(true);
        panelLoading.SetActive(false);
        panelLobby.SetActive(false);

        buttonHost.interactable = true;
        buttonJoin.interactable = true;
        inputCode.text = "";

        StopSpinner();
        DestroyAllModels();
    }

    private void ShowLoadingPanel(string message)
    {
        panelConnect.SetActive(false);
        panelLoading.SetActive(true);
        panelLobby.SetActive(false);

        if (labelLoading != null) labelLoading.text = message;
        StartSpinner();
    }

    private void ShowLobbyPanel(bool isHost, string code)
    {
        panelConnect.SetActive(false);
        panelLoading.SetActive(false);
        panelLobby.SetActive(true);

        StopSpinner();

        if (labelCode != null)
            labelCode.text = isHost ? $"Código: {code}" : "Conectado";

        buttonStartGame.gameObject.SetActive(isHost);
        UpdateLobbyStatus();
    }

    private void UpdateLobbyStatus()
    {
        if (labelStatus == null) return;

        bool isHost = NetworkSessionManager.Instance != null
                   && NetworkSessionManager.Instance.IsHost;

        if (isHost)
        {
            labelStatus.text = secondPlayerConnected
                ? "✓ Jugador 2 conectado — podés comenzar"
                : "Esperando al segundo jugador...";

            if (buttonStartGame != null)
                buttonStartGame.interactable = secondPlayerConnected;
        }
        else
        {
            labelStatus.text = "Conectado — esperando al host...";
        }
    }

    // -------------------------------------------------------
    // SPINNER
    // -------------------------------------------------------

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