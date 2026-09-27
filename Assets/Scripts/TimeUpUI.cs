using System;
using UnityEngine;

// Panel que se muestra cuando el LevelTimer llega a cero. A diferencia de
// GameOverUI (que es por jugador, con revivir por anuncio), aca el reloj es
// compartido: cuando se acaba, el nivel termina para los dos.
public class TimeUpUI : MonoBehaviour
{
    private static TimeUpUI instance;

    public static void Show(bool isHost, Action onRetry, Action onLobby)
    {
        if (instance != null) return; // ya visible

        var canvas = SimpleUI.CreateOverlayCanvas("TimeUpUI", 400);
        instance = canvas.gameObject.AddComponent<TimeUpUI>();
        instance.Build(isHost, onRetry, onLobby);
    }

    public static void Hide()
    {
        if (instance != null) Destroy(instance.gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Build(bool isHost, Action onRetry, Action onLobby)
    {
        SimpleUI.CreateOverlay(transform);

        var panel = SimpleUI.CreatePanel(transform, new Vector2(720f, 520f));
        Transform p = panel.transform;

        var title = SimpleUI.CreateText(p, "Title", "¡SE ACABÓ EL TIEMPO!", 64f,
            new Vector2(0f, 180f), new Vector2(660f, 90f));
        title.color = new Color(1f, 0.35f, 0.3f, 1f);

        SimpleUI.CreateText(p, "Subtitle", "No llegaron a la meta antes del reloj", 38f,
            new Vector2(0f, 90f), new Vector2(660f, 60f));

        var size = new Vector2(460f, 95f);

        if (isHost)
        {
            SimpleUI.CreateButton(p, "ButtonRetry", "Reintentar",
                new Vector2(0f, -40f), size, SimpleUI.BlueButton, () => onRetry?.Invoke());

            SimpleUI.CreateButton(p, "ButtonLobby", "Volver al Lobby",
                new Vector2(0f, -160f), size, SimpleUI.GreyButton, () => onLobby?.Invoke());
        }
        else
        {
            SimpleUI.CreateText(p, "Waiting", "Esperando al host...", 36f,
                new Vector2(0f, -100f), new Vector2(660f, 60f))
                .color = new Color(1f, 1f, 1f, 0.7f);
        }
    }
}