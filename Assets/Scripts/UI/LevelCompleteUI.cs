using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Panel de "Nivel completado" generado en runtime.
/// Lo muestra LevelManager cuando no hay un levelCompletePanel asignado en el
/// Inspector (así el flujo funciona en todas las escenas sin armar UI a mano).
///
/// - HOST: botones "Volver al Lobby" y "Siguiente nivel" (si hay uno configurado).
/// - CLIENTE: mensaje "Esperando al host..." — solo el host puede cargar escenas
///   en red (mismo criterio que NetworkLevelSelectorManager).
/// </summary>
public class LevelCompleteUI : MonoBehaviour
{
    private static LevelCompleteUI instance;

    /// <param name="formattedTime">Tiempo restante ya formateado (mm:ss).</param>
    /// <param name="earnedStars">Estrellas obtenidas (0-3).</param>
    /// <param name="isHost">Si este jugador decide a dónde ir.</param>
    /// <param name="hasNextLevel">Si hay un "siguiente nivel" configurado.</param>
    /// <param name="onLobby">Callback del botón Lobby (solo host).</param>
    /// <param name="onNextLevel">Callback del botón Siguiente nivel (solo host).</param>
    public static void Show(
        string formattedTime, int earnedStars, bool isHost, bool hasNextLevel,
        Action onLobby, Action onNextLevel)
    {
        if (instance != null) return; // ya visible

        var canvas = SimpleUI.CreateOverlayCanvas("LevelCompleteUI", 400);
        instance = canvas.gameObject.AddComponent<LevelCompleteUI>();
        instance.Build(formattedTime, earnedStars, isHost, hasNextLevel, onLobby, onNextLevel);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Build(
        string formattedTime, int earnedStars, bool isHost, bool hasNextLevel,
        Action onLobby, Action onNextLevel)
    {
        SimpleUI.CreateOverlay(transform);

        var panel = SimpleUI.CreatePanel(transform, new Vector2(760f, 560f));
        Transform p = panel.transform;

        SimpleUI.CreateText(p, "Title", "¡NIVEL COMPLETADO!", 64f,
            new Vector2(0f, 190f), new Vector2(700f, 90f));

        SimpleUI.CreateText(p, "Time", $"Tiempo restante: {formattedTime}", 40f,
            new Vector2(0f, 100f), new Vector2(700f, 60f));

        SimpleUI.CreateText(p, "Stars", $"Estrellas: {earnedStars} / 3", 40f,
            new Vector2(0f, 40f), new Vector2(700f, 60f));

        if (isHost)
        {
            var size = new Vector2(420f, 90f);

            SimpleUI.CreateButton(p, "ButtonLobby", "Volver al Lobby",
                new Vector2(0f, hasNextLevel ? -60f : -120f), size,
                SimpleUI.BlueButton, () => onLobby?.Invoke());

            if (hasNextLevel)
            {
                SimpleUI.CreateButton(p, "ButtonNext", "Siguiente nivel",
                    new Vector2(0f, -170f), size,
                    SimpleUI.GreenButton, () => onNextLevel?.Invoke());
            }
        }
        else
        {
            SimpleUI.CreateText(p, "Waiting", "Esperando al host...", 36f,
                new Vector2(0f, -120f), new Vector2(700f, 60f))
                .color = new Color(1f, 1f, 1f, 0.7f);
        }
    }
}
