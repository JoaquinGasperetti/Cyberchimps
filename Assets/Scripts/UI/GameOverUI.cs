using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    private static GameOverUI instance;

    private const string LobbyScene = "Lobby";

    public static void Show(string loserName, bool isHost, bool isLoser)
    {
        if (instance != null) return; // ya visible

        var canvas = SimpleUI.CreateOverlayCanvas("GameOverUI", 400);
        instance = canvas.gameObject.AddComponent<GameOverUI>();
        instance.Build(loserName, isHost, isLoser);
    }

    public static void Hide()
    {
        if (instance != null) Destroy(instance.gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Build(string loserName, bool isHost, bool isLoser)
    {
        SimpleUI.CreateOverlay(transform);

        var panel = SimpleUI.CreatePanel(transform, new Vector2(720f, 620f));
        Transform p = panel.transform;

        var title = SimpleUI.CreateText(p, "Title", "GAME OVER", 72f,
            new Vector2(0f, 230f), new Vector2(660f, 90f));
        title.color = new Color(1f, 0.35f, 0.3f, 1f);

        SimpleUI.CreateText(p, "Loser", $"{loserName} se quedó sin vidas", 40f,
            new Vector2(0f, 140f), new Vector2(660f, 60f));

        var size = new Vector2(460f, 95f);

        if (isLoser)
        {
            var reviveBtn = SimpleUI.CreateButton(p, "ButtonRevive", "REVIVIR (Anuncio)",
                new Vector2(0f, 40f), size, SimpleUI.GreenButton, null);
            reviveBtn.onClick.AddListener(() => OnReviveClicked(reviveBtn));
            // si no hay anuncio cargado, se ve deshabilitado
            reviveBtn.interactable = AdManager.CanShowRewarded;
        }

        if (isHost)
        {
            SimpleUI.CreateButton(p, "ButtonRetry", "Reintentar",
                new Vector2(0f, -80f), size, SimpleUI.BlueButton, OnRetryClicked);

            SimpleUI.CreateButton(p, "ButtonLobby", "Volver al Lobby",
                new Vector2(0f, -200f), size, SimpleUI.GreyButton, OnLobbyClicked);
        }
        else if (!isLoser)
        {
            SimpleUI.CreateText(p, "Waiting", "Esperando al host...", 36f,
                new Vector2(0f, -120f), new Vector2(660f, 60f))
                .color = new Color(1f, 1f, 1f, 0.7f);
        }
    }

    private void OnReviveClicked(UnityEngine.UI.Button reviveBtn)
    {
        reviveBtn.interactable = false; // evitar doble uso mientras carga el anuncio
        AdManager.Rewarded(() =>
        {
            var lives = PlayerLivesLocal();
            if (lives != null) lives.RequestReviveFromAd();
            // si algo falla la pantalla no se cierra y el boton queda para reintentar
        });

        // si no habia anuncio, no paso nada: lo rehabilitamos
        if (!AdManager.CanShowRewarded) reviveBtn.interactable = true;
    }

    private static PlayerLives PlayerLivesLocal()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null)
            return null;
        return nm.LocalClient.PlayerObject.GetComponent<PlayerLives>();
    }

    private void OnRetryClicked()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost) return;
        string scene = SceneManager.GetActiveScene().name;
        AdManager.Interstitial(() => NetworkSceneLoader.Instance?.LoadScene(scene));
    }

    private void OnLobbyClicked()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost) return;
        AdManager.Interstitial(() => NetworkSceneLoader.Instance?.LoadScene(LobbyScene));
    }
}
