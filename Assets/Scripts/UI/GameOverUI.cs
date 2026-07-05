using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Pantalla de Game Over generada en runtime (mismo estilo que LevelCompleteUI).
/// La muestra PlayerLives (vía ClientRpc) en AMBOS jugadores cuando alguno
/// se queda sin vidas.
///
/// - Indica qué jugador perdió.
/// - HOST: botones "Reintentar" (recarga el nivel actual por red, para los dos)
///   y "Volver al Lobby".
/// - CLIENTE: mensaje "Esperando al host..." — solo el host carga escenas en red.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    private static GameOverUI instance;

    private const string LobbyScene = "Lobby";

    public static void Show(string loserName, bool isHost)
    {
        if (instance != null) return; // ya visible

        var canvas = SimpleUI.CreateOverlayCanvas("GameOverUI", 400);
        instance = canvas.gameObject.AddComponent<GameOverUI>();
        instance.Build(loserName, isHost);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Build(string loserName, bool isHost)
    {
        SimpleUI.CreateOverlay(transform);

        var panel = SimpleUI.CreatePanel(transform, new Vector2(720f, 560f));
        Transform p = panel.transform;

        var title = SimpleUI.CreateText(p, "Title", "GAME OVER", 72f,
            new Vector2(0f, 190f), new Vector2(660f, 90f));
        title.color = new Color(1f, 0.35f, 0.3f, 1f);

        SimpleUI.CreateText(p, "Loser", $"{loserName} se quedó sin vidas", 40f,
            new Vector2(0f, 90f), new Vector2(660f, 60f));

        if (isHost)
        {
            var size = new Vector2(460f, 95f);

            SimpleUI.CreateButton(p, "ButtonRetry", "Reintentar",
                new Vector2(0f, -40f), size, SimpleUI.GreenButton, OnRetryClicked);

            SimpleUI.CreateButton(p, "ButtonLobby", "Volver al Lobby",
                new Vector2(0f, -160f), size, SimpleUI.BlueButton, OnLobbyClicked);
        }
        else
        {
            SimpleUI.CreateText(p, "Waiting", "Esperando al host...", 36f,
                new Vector2(0f, -100f), new Vector2(660f, 60f))
                .color = new Color(1f, 1f, 1f, 0.7f);
        }
    }

    // ── Acciones (solo host) ──────────────────────────────────────────────

    private void OnRetryClicked()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost) return;

        // Recargar el nivel actual por red: respawnea a ambos jugadores
        // con las vidas completas (los players se destruyen con la escena).
        NetworkSceneLoader.Instance?.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnLobbyClicked()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost) return;
        NetworkSceneLoader.Instance?.LoadScene(LobbyScene);
    }
}
