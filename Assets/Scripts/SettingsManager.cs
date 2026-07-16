using UnityEngine;

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyOnStartup()
    {
        AudioListener.volume = Volume;
        QualitySettings.SetQualityLevel(
            Mathf.Clamp(Quality, 0, QualitySettings.names.Length - 1), true);
    }
}
