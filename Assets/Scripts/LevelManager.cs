using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Countdown")]
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private float countdownDuration = 3f;

    [Header("Timer")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private float levelTime = 120f;

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

    private float currentTime;
    private bool timerRunning;

    public bool CanPlay { get; private set; }

    public float CurrentTime => currentTime;

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
        currentTime = levelTime;

        CanPlay = false;

        StartCoroutine(StartCountdown());
    }

    private void Update()
    {
        if (!timerRunning)
            return;

        UpdateTimer();
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
        timerRunning = true;
    }
    public void AddTime(float amount)
    {
        currentTime += amount;

        if (bonusTextCoroutine != null)
        {
            StopCoroutine(bonusTextCoroutine);
        }

        bonusTextCoroutine = StartCoroutine(ShowBonusText(amount));

        UpdateTimerUI();
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

    private void UpdateTimer()
    {
        currentTime -= Time.deltaTime;

        if (currentTime <= 0f)
        {
            currentTime = 0f;
        }

        UpdateTimerUI();
    }

    private void UpdateTimerUI()
    {
        int minutes = Mathf.FloorToInt(currentTime / 60f);
        int seconds = Mathf.FloorToInt(currentTime % 60f);

        timerText.text = FormatTime(currentTime);

        if (currentTime <= 0f)
        {
            timerText.color = Color.red;
        }
    }

    public void StopTimer()
    {
        timerRunning = false;
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
        return currentTime > 0f;
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
            finalTimeText.text = FormatTime(currentTime);
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

        StopTimer();

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