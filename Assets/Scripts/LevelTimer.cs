using UnityEngine;
using TMPro;

public class LevelTimer : MonoBehaviour
{
    public static LevelTimer Instance { get; private set; }

    [SerializeField] private TMP_Text timerText;

    private float currentTime;
    private bool isRunning;

    public float CurrentTime => currentTime;
    public bool IsRunning => isRunning;

    private void Awake()
    {
        // Singleton simple
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        StartTimer();
    }

    private void Update()
    {
        if (!isRunning)
            return;

        currentTime += Time.deltaTime;

        UpdateUI();
    }

    public void StartTimer()
    {
        currentTime = 0f;
        isRunning = true;
    }

    public void StopTimer()
    {
        isRunning = false;

        Debug.Log("Nivel completado en: " + GetFormattedTime());
    }

    private void UpdateUI()
    {
        if (timerText != null)
        {
            timerText.text = GetFormattedTime();
        }
    }

    public string GetFormattedTime()
    {
        int minutes = Mathf.FloorToInt(currentTime / 60f);
        int seconds = Mathf.FloorToInt(currentTime % 60f);
        int milliseconds = Mathf.FloorToInt((currentTime * 100f) % 100f);

        return string.Format("{0:00}:{1:00}:{2:00}",
            minutes,
            seconds,
            milliseconds);
    }
}