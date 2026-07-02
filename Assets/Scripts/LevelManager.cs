using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Maneja el flujo del nivel: countdown inicial, panel de resultados y estrellas.
///
/// CAMBIO IMPORTANTE:
///   El timer YA NO se lleva localmente acá (antes "currentTime" se decrementaba
///   en Update() de forma independiente en cada cliente → desync entre jugadores).
///   Ahora LevelManager solo LEE y MUESTRA el tiempo que vive en LevelTimer
///   (NetworkVariable, autoridad del servidor, sincronizado para ambos jugadores).
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Countdown")]
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private float countdownDuration = 3f;

    [Header("Timer")]
    [SerializeField] private TMP_Text timerText;

    [Header("Time Bonus")]
    [SerializeField] private TMP_Text bonusTimeText;
    [SerializeField] private float bonusTextDuration = 2f;

    [Header("Level Complete")]
    [SerializeField] private GameObject levelCompletePanel;

    [SerializeField] private TMP_Text finalTimeText;
    [Header("Stars")]
    [SerializeField] private Image[] stars;

    [SerializeField] private Sprite filledStar;
    [SerializeField] private Sprite emptyStar;

    private bool levelCompleted;
    [SerializeField] private string levelSelectorScene = "LevelSelector";
    private Coroutine bonusTextCoroutine;

    public bool CanPlay { get; private set; }

    /// <summary>Tiempo restante — ahora viene siempre de LevelTimer (sincronizado).</summary>
    public float CurrentTime => LevelTimer.Instance != null ? LevelTimer.Instance.RemainingTime : 0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        CanPlay = false;
        StartCoroutine(WaitForTimerAndStart());
    }

    private void OnDestroy()
    {
        if (LevelTimer.Instance != null)
        {
            LevelTimer.Instance.OnTimeChanged -= HandleTimeChanged;
            LevelTimer.Instance.OnTimeUp -= HandleTimeUp;
            LevelTimer.Instance.OnBonusTimeAdded -= HandleBonusTimeAdded;
        }
    }

    private System.Collections.IEnumerator WaitForTimerAndStart()
    {
        // LevelTimer se spawnea en red al cargar la escena — esperamos a que exista
        // antes de suscribirnos, para no perder el primer valor.
        while (LevelTimer.Instance == null)
            yield return null;

        LevelTimer.Instance.OnTimeChanged += HandleTimeChanged;
        LevelTimer.Instance.OnTimeUp += HandleTimeUp;
        LevelTimer.Instance.OnBonusTimeAdded += HandleBonusTimeAdded;

        UpdateTimerUI(LevelTimer.Instance.RemainingTime);

        yield return StartCoroutine(StartCountdown());
    }

    private System.Collections.IEnumerator StartCountdown()
    {
        countdownText.gameObject.SetActive(true);

        countdownText.text = "3";
        yield return new WaitForSeconds(1f);

        countdownText.text = "2";
        yield return new WaitForSeconds(1f);

        countdownText.text = "1";
        yield return new WaitForSeconds(1f);

        countdownText.text = "READY!";
        yield return new WaitForSeconds(1f);

        countdownText.gameObject.SetActive(false);

        CanPlay = true;

        // Solo tiene efecto real en el servidor (no-op seguro en los clientes).
        LevelTimer.Instance.StartTimer();
    }

    private void HandleTimeChanged(float newTime)
    {
        UpdateTimerUI(newTime);
    }

    private void HandleTimeUp()
    {
        if (timerText != null)
            timerText.color = Color.red;

        // Acá se puede enganchar una derrota/fin de nivel por tiempo si el diseño lo pide.
    }

    private void HandleBonusTimeAdded(float amount)
    {
        if (bonusTextCoroutine != null)
            StopCoroutine(bonusTextCoroutine);

        bonusTextCoroutine = StartCoroutine(ShowBonusText(amount));
    }

    private System.Collections.IEnumerator ShowBonusText(float amount)
    {
        if (bonusTimeText == null)
            yield break;

        bonusTimeText.gameObject.SetActive(true);
        bonusTimeText.text = $"+{amount:0}s";

        yield return new WaitForSeconds(bonusTextDuration);

        bonusTimeText.gameObject.SetActive(false);
    }

    private void UpdateTimerUI(float time)
    {
        if (timerText == null) return;

        timerText.text = FormatTime(time);
        timerText.color = time <= 0f ? Color.red : timerText.color;
    }

    private void UpdateStarsUI(int earnedStars)
    {
        for (int i = 0; i < stars.Length; i++)
        {
            stars[i].sprite = i < earnedStars
                ? filledStar
                : emptyStar;
        }
    }

    public bool HasTimeStar()
    {
        return CurrentTime > 0f;
    }

    private int CalculateStars()
    {
        int earnedStars = 0;

        // ⭐ Completar nivel
        earnedStars++;

        // ⭐ Tiempo
        if (HasTimeStar())
            earnedStars++;

        // ⭐ Todos los Cyberdatos
        if (HasCyberdataStar())
            earnedStars++;

        return earnedStars;
    }

    private void ShowResults(int stars)
    {
        if (levelCompletePanel != null)
        {
            levelCompletePanel.SetActive(true);
        }

        if (finalTimeText != null)
        {
            finalTimeText.text = FormatTime(CurrentTime);
        }
        UpdateStarsUI(stars);
    }

    private string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);

        return $"{minutes:00}:{seconds:00}";
    }

    public void CompleteLevel()
    {
        if (levelCompleted)
            return;

        levelCompleted = true;

        if (LevelTimer.Instance != null)
            LevelTimer.Instance.StopTimer();

        CanPlay = false;

        int stars = CalculateStars();

        ShowResults(stars);
    }

    public void RetryLevel()
    {
        Scene currentScene = SceneManager.GetActiveScene();

        SceneManager.LoadScene(currentScene.buildIndex);
    }

    public void ReturnToLevelSelector()
    {
        SceneManager.LoadScene(levelSelectorScene);
    }

    private bool HasCyberdataStar()
    {
        if (CyberdataManager.Instance == null)
            return false;

        return CyberdataManager.Instance.LevelCyberdataCollected ==
               CyberdataManager.Instance.LevelCyberdataTotal
               &&
               CyberdataManager.Instance.LevelCyberdataTotal > 0;
    }
}
