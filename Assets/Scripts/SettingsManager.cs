using UnityEngine;

/// <summary>
/// Guarda y aplica las opciones del juego (PlayerPrefs).
/// Es estático: no necesita ningún GameObject en escena — se aplica solo
/// al arrancar el juego y cada vez que OptionsMenuUI cambia un valor.
///
/// Opciones actuales (pensadas para mobile):
///  - Volumen general (AudioListener.volume)
///  - Calidad gráfica (QualitySettings, 0 = Baja … n = Alta)
///  - Vibración (para que la lean los scripts de gameplay que usen Handheld.Vibrate)
/// </summary>
public static class SettingsManager
{
    private const string KeyVolume = "opt_volume";
    private const string KeyQuality = "opt_quality";
    private const string KeyVibration = "opt_vibration";

    public static float Volume
    {
        get => PlayerPrefs.GetFloat(KeyVolume, 1f);
        set
        {
            PlayerPrefs.SetFloat(KeyVolume, Mathf.Clamp01(value));
            AudioListener.volume = Mathf.Clamp01(value);
        }
    }

    /// <summary>Índice dentro de QualitySettings.names.</summary>
    public static int Quality
    {
        get => PlayerPrefs.GetInt(KeyQuality, QualitySettings.GetQualityLevel());
        set
        {
            int clamped = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1);
            PlayerPrefs.SetInt(KeyQuality, clamped);
            QualitySettings.SetQualityLevel(clamped, true);
        }
    }

    public static bool Vibration
    {
        get => PlayerPrefs.GetInt(KeyVibration, 1) == 1;
        set => PlayerPrefs.SetInt(KeyVibration, value ? 1 : 0);
    }

    public static void Save() => PlayerPrefs.Save();

    /// <summary>Se ejecuta una sola vez al iniciar el juego, antes de la primera escena.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyOnStartup()
    {
        AudioListener.volume = Volume;
        QualitySettings.SetQualityLevel(
            Mathf.Clamp(Quality, 0, QualitySettings.names.Length - 1), true);
    }
}
