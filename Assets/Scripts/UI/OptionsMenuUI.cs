using TMPro;
using UnityEngine;

/// <summary>
/// Panel de opciones generado en runtime (mismo estilo que PauseMenuUI /
/// LevelCompleteUI). Se abre desde el botón "Opciones" del menú principal
/// (MainMenuManager.OpenOptions) y desde el menú de pausa in-game.
///
/// Opciones (ver SettingsManager):
///  - Volumen general  → slider
///  - Calidad gráfica  → botón que cicla entre los niveles de QualitySettings
///  - Vibración        → botón Sí/No
///
/// Los valores se guardan en PlayerPrefs al cerrar el panel.
/// </summary>
public class OptionsMenuUI : MonoBehaviour
{
    private static OptionsMenuUI instance;

    private TextMeshProUGUI qualityLabel;
    private TextMeshProUGUI vibrationLabel;
    private TextMeshProUGUI volumeValueLabel;

    /// <summary>Abre el panel (una sola instancia; sortOrder por encima del menú de pausa).</summary>
    public static void Show()
    {
        if (instance != null) return;

        var canvas = SimpleUI.CreateOverlayCanvas("OptionsMenuUI", 600);
        instance = canvas.gameObject.AddComponent<OptionsMenuUI>();
        instance.Build();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Build()
    {
        SimpleUI.CreateOverlay(transform);

        var panel = SimpleUI.CreatePanel(transform, new Vector2(760f, 660f));
        Transform p = panel.transform;

        SimpleUI.CreateText(p, "Title", "OPCIONES", 62f,
            new Vector2(0f, 255f), new Vector2(680f, 80f));

        // ── Volumen ───────────────────────────────────────────────────────
        SimpleUI.CreateText(p, "VolumeLabel", "Volumen", 40f,
            new Vector2(-160f, 150f), new Vector2(320f, 55f),
            TextAlignmentOptions.Left);

        volumeValueLabel = SimpleUI.CreateText(p, "VolumeValue",
            FormatPercent(SettingsManager.Volume), 36f,
            new Vector2(280f, 150f), new Vector2(120f, 55f));

        SimpleUI.CreateSlider(p, "VolumeSlider",
            new Vector2(0f, 85f), new Vector2(620f, 40f),
            SettingsManager.Volume, OnVolumeChanged);

        // ── Calidad gráfica ──────────────────────────────────────────────
        SimpleUI.CreateText(p, "QualityTitle", "Calidad gráfica", 40f,
            new Vector2(-160f, 0f), new Vector2(320f, 55f),
            TextAlignmentOptions.Left);

        var qualityButton = SimpleUI.CreateButton(p, "QualityButton", "",
            new Vector2(180f, 0f), new Vector2(280f, 70f),
            SimpleUI.BlueButton, CycleQuality);
        qualityLabel = qualityButton.GetComponentInChildren<TextMeshProUGUI>();
        qualityLabel.fontSize = 30f;
        UpdateQualityLabel();

        // ── Vibración ────────────────────────────────────────────────────
        SimpleUI.CreateText(p, "VibrationTitle", "Vibración", 40f,
            new Vector2(-160f, -95f), new Vector2(320f, 55f),
            TextAlignmentOptions.Left);

        var vibrationButton = SimpleUI.CreateButton(p, "VibrationButton", "",
            new Vector2(180f, -95f), new Vector2(280f, 70f),
            SimpleUI.GreyButton, ToggleVibration);
        vibrationLabel = vibrationButton.GetComponentInChildren<TextMeshProUGUI>();
        vibrationLabel.fontSize = 30f;
        UpdateVibrationLabel();

        // ── Cerrar ───────────────────────────────────────────────────────
        SimpleUI.CreateButton(p, "ButtonClose", "Cerrar",
            new Vector2(0f, -235f), new Vector2(460f, 90f),
            SimpleUI.GreenButton, Close);
    }

    // ── Acciones ──────────────────────────────────────────────────────────

    private void OnVolumeChanged(float value)
    {
        SettingsManager.Volume = value;
        if (volumeValueLabel != null)
            volumeValueLabel.text = FormatPercent(value);
    }

    private void CycleQuality()
    {
        SettingsManager.Quality =
            (SettingsManager.Quality + 1) % QualitySettings.names.Length;
        UpdateQualityLabel();
    }

    private void ToggleVibration()
    {
        SettingsManager.Vibration = !SettingsManager.Vibration;
        UpdateVibrationLabel();
    }

    private void Close()
    {
        SettingsManager.Save();
        Destroy(gameObject);
    }

    // ── UI helpers ────────────────────────────────────────────────────────

    private void UpdateQualityLabel()
    {
        if (qualityLabel != null)
            qualityLabel.text = QualitySettings.names[SettingsManager.Quality];
    }

    private void UpdateVibrationLabel()
    {
        if (vibrationLabel != null)
            vibrationLabel.text = SettingsManager.Vibration ? "Sí" : "No";
    }

    private static string FormatPercent(float value) =>
        $"{Mathf.RoundToInt(value * 100f)}%";
}
