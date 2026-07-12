using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menú de opciones in-game, generado en runtime.
/// LevelManager lo crea automáticamente en cada escena de nivel.
///
/// Muestra un botón de pausa (⏸) arriba a la derecha que abre el menú:
///  - Continuar: cierra el menú.
///  - Volver al Lobby (solo host): lleva a AMBOS jugadores al Lobby por red,
///    manteniendo la sesión viva.
///  - Salir al menú principal: abandona la sesión (solo este jugador) y carga
///    MainMenu localmente. Si sale el host, el cliente se desconecta y
///    GameManager lo devuelve al menú.
///
/// NOTA: en multiplayer no se pausa el tiempo (Time.timeScale queda intacto) —
/// el otro jugador sigue jugando.
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private string lobbyScene = "Lobby";
    [SerializeField] private string mainMenuScene = "MainMenu";

    private GameObject menuRoot;
    private bool isLeaving;

    public static PauseMenuUI Ensure()
    {
        var existing = FindFirstObjectByType<PauseMenuUI>();
        if (existing != null) return existing;

        var canvas = SimpleUI.CreateOverlayCanvas("PauseMenuUI", 500);
        return canvas.gameObject.AddComponent<PauseMenuUI>();
    }

    private void Start()
    {
        BuildPauseButton();
        BuildMenu();
        menuRoot.SetActive(false);
    }

    // ── Construcción ──────────────────────────────────────────────────────

    private void BuildPauseButton()
    {
        var button = SimpleUI.CreateButton(
            transform, "PauseButton", "| |",
            Vector2.zero, new Vector2(90f, 90f),
            SimpleUI.GreyButton, ToggleMenu);

        // Anclar arriba a la derecha (CreateButton lo deja centrado)
        var rt = button.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-30f, -30f);
    }

    private void BuildMenu()
    {
        menuRoot = new GameObject("MenuRoot");
        menuRoot.transform.SetParent(transform, false);
        var rootRt = menuRoot.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        SimpleUI.CreateOverlay(menuRoot.transform);

        var panel = SimpleUI.CreatePanel(menuRoot.transform, new Vector2(640f, 680f));
        Transform p = panel.transform;

        SimpleUI.CreateText(p, "Title", "PAUSA", 60f,
            new Vector2(0f, 260f), new Vector2(560f, 80f));

        var size = new Vector2(460f, 90f);

        SimpleUI.CreateButton(p, "ButtonResume", "Continuar",
            new Vector2(0f, 140f), size, SimpleUI.GreenButton, CloseMenu);

        SimpleUI.CreateButton(p, "ButtonOptions", "Opciones",
            new Vector2(0f, 20f), size, SimpleUI.GreyButton, OptionsMenuUI.Show);

        var lobbyButton = SimpleUI.CreateButton(p, "ButtonLobby", "Volver al Lobby (ambos)",
            new Vector2(0f, -100f), size, SimpleUI.BlueButton, OnLobbyClicked);

        SimpleUI.CreateButton(p, "ButtonQuit", "Salir al menú principal",
            new Vector2(0f, -220f), size, SimpleUI.RedButton, OnQuitClicked);

        // Solo el host puede llevar a los dos al Lobby (carga de escena en red)
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        lobbyButton.interactable = isHost;
        if (!isHost)
        {
            var label = lobbyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = "Volver al Lobby (solo host)";
        }
    }

    // ── Acciones ──────────────────────────────────────────────────────────

    private void ToggleMenu()
    {
        if (menuRoot != null) menuRoot.SetActive(!menuRoot.activeSelf);
    }

    private void CloseMenu()
    {
        if (menuRoot != null) menuRoot.SetActive(false);
    }

    private void OnLobbyClicked()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost) return;
        NetworkSceneLoader.Instance?.LoadScene(lobbyScene);
    }

    private async void OnQuitClicked()
    {
        if (isLeaving) return;
        isLeaving = true;

        if (NetworkSessionManager.Instance != null)
            await NetworkSessionManager.Instance.LeaveSessionAsync();

        // Transición natural → interstitial (null-safe, sigue de largo si no hay)
        AdManager.Interstitial(() => SceneManager.LoadScene(mainMenuScene));
    }
}
