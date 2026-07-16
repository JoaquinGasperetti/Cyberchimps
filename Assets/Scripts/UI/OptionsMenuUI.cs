using TMPro;
using UnityEngine;

public class OptionsMenuUI : MonoBehaviour
{
    private static OptionsMenuUI instance;

    private TextMeshProUGUI qualityLabel;
    private TextMeshProUGUI vibrationLabel;
    private TextMeshProUGUI volumeValueLabel;

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

        SimpleUI.CreateText(p, "VolumeLabel", "Volumen", 40f,
            new Vector2(-160f, 150f), new Vector2(320f, 55f),
            TextAlignmentOptions.Left);

        volumeValueLabel = SimpleUI.CreateText(p, "VolumeValue",
            FormatPercent(SettingsManager.Volume), 36f,
            new Vector2(280f, 150f), new Vector2(120f, 55f));

        SimpleUI.CreateSlider(p, "VolumeSlider",
            new Vector2(0f, 85f), new Vector2(620f, 40f),
            SettingsManager.Volume, OnVolumeChanged);

        SimpleUI.CreateText(p, "QualityTitle", "Calidad gráfica", 40f,
            new Vector2(-160f, 0f), new Vector2(320f, 55f),
            TextAlignmentOptions.Left);

        var qualityButton = SimpleUI.CreateButton(p, "QualityButton", "",
            new Vector2(180f, 0f), new Vector2(280f, 70f),
            SimpleUI.BlueButton, CycleQuality);
        qualityLabel = qualityButton.GetComponentInChildren<TextMeshProUGUI>();
        qualityLabel.fontSize = 30f;
        UpdateQualityLabel();

        SimpleUI.CreateText(p, "VibrationTitle", "Vibración", 40f,
            new Vector2(-160f, -95f), new Vector2(320f, 55f),
            TextAlignmentOptions.Left);

        var vibrationButton = SimpleUI.CreateButton(p, "VibrationButton", "",
            new Vector2(180f, -95f), new Vector2(280f, 70f),
            SimpleUI.GreyButton, ToggleVibration);
        vibrationLabel = vibrationButton.GetComponentInChildren<TextMeshProUGUI>();
        vibrationLabel.fontSize = 30f;
        UpdateVibrationLabel();

        SimpleUI.CreateButton(p, "ButtonClose", "Cerrar",
            new Vector2(0f, -235f), new Vector2(460f, 90f),
            SimpleUI.GreenButton, Close);
    }

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
