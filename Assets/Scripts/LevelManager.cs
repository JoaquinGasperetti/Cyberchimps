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

    // cartel "En la meta: 1/2", generado en runtime
    private GameObject goalProgressRoot;
    private TMP_Text goalProgressText;

    [Header("Flujo de nivel")]
    [SerializeField] private string lobbyScene = "Lobby";
    [Tooltip("Escena del siguiente nivel. Vacío = el panel de completado solo ofrece volver al Lobby.")]
    [SerializeField] private string nextLevelScene = "";

    private Coroutine bonusTextCoroutine;

    public bool CanPlay { get; private set; }

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
        PauseMenuUI.Ensure();
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
        // el LevelTimer spawnea por red: esperamos a que exista antes de engancharnos
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
        // countdownText puede venir sin asignar; sin este guard se moria la corutina
        if (countdownText != null)
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
        }
        else
        {
            yield return new WaitForSeconds(countdownDuration);
        }

        CanPlay = true;

        // en los clientes no hace nada
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

        // aca iria la derrota por tiempo, si algun dia se suma
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
        if (stars == null) return;

        for (int i = 0; i < stars.Length; i++)
        {
            // en algunas escenas las estrellas no estan asignadas
            if (stars[i] == null) continue;

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

        // estrella 1: completar el nivel
        earnedStars++;

        // estrella 2: terminar rapido
        if (HasTimeStar())
            earnedStars++;

        // estrella 3: juntar todos los cyberdatos
        if (HasCyberdataStar())
            earnedStars++;

        return earnedStars;
    }

    private void ShowResults(int earnedStars, string[] playerStatLines)
    {
        if (levelCompletePanel != null)
        {
            // panel armado a mano en la escena
            levelCompletePanel.SetActive(true);

            if (finalTimeText != null)
                finalTimeText.text = FormatTime(CurrentTime);

            UpdateStarsUI(earnedStars);
            return;
        }

        // sin panel asignado usamos la UI generada en runtime
        LevelCompleteUI.Show(
            FormatTime(CurrentTime),
            earnedStars,
            playerStatLines,
            IsSceneAuthority(),
            !string.IsNullOrEmpty(nextLevelScene),
            GoToLobby,
            GoToNextLevel);
    }

    private bool IsSceneAuthority()
    {
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return true; // sin red = prueba local
        return nm.IsHost;
    }

    private void LoadSceneForEveryone(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;

        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            if (!nm.IsHost)
            {
                Debug.LogWarning("[LevelManager] Solo el host puede cargar escenas en red.");
                return;
            }
            NetworkSceneLoader.Instance?.LoadScene(sceneName);
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    public void GoToLobby() => LoadSceneWithInterstitial(lobbyScene);

    public void GoToNextLevel() => LoadSceneWithInterstitial(nextLevelScene);

    private void LoadSceneWithInterstitial(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;

        if (!IsSceneAuthority())
        {
            // el cliente no carga escenas
            LoadSceneForEveryone(sceneName);
            return;
        }

        AdManager.Interstitial(() => LoadSceneForEveryone(sceneName));
    }

    private string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);

        return $"{minutes:00}:{seconds:00}";
    }

    public void CompleteLevel()
    {
        CompleteLevel(null, null);
    }

    public void CompleteLevel(ulong[] statClientIds, int[] statCyberdata)
    {
        if (levelCompleted)
            return;

        levelCompleted = true;

        HideGoalProgress();

        if (LevelTimer.Instance != null)
            LevelTimer.Instance.StopTimer();

        CanPlay = false;

        int stars = CalculateStars();

        ShowResults(stars, BuildPlayerStatLines(statClientIds, statCyberdata));
    }

    public void ShowGoalProgress(int current, int required)
    {
        if (levelCompleted) return;

        if (current <= 0)
        {
            HideGoalProgress();
            return;
        }

        if (goalProgressRoot == null)
        {
            var canvas = SimpleUI.CreateOverlayCanvas("GoalProgressUI", 350);
            goalProgressRoot = canvas.gameObject;

            goalProgressText = SimpleUI.CreateText(canvas.transform, "Progress",
                "", 46f, Vector2.zero, new Vector2(1100f, 70f));

            // arriba al centro, debajo del timer
            var rt = goalProgressText.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -140f);
        }

        goalProgressRoot.SetActive(true);
        goalProgressText.text = current < required
            ? $"En la meta: {current}/{required} — esperando al otro jugador..."
            : $"En la meta: {current}/{required}";
    }

    public void HideGoalProgress()
    {
        if (goalProgressRoot != null)
            goalProgressRoot.SetActive(false);
    }

    private string[] BuildPlayerStatLines(ulong[] clientIds, int[] cyberdata)
    {
        if (clientIds == null || cyberdata == null || clientIds.Length == 0)
            return null;

        System.Array.Sort(clientIds, cyberdata);

        ulong localId = Unity.Netcode.NetworkManager.Singleton != null
            ? Unity.Netcode.NetworkManager.Singleton.LocalClientId
            : ulong.MaxValue;

        var lines = new string[clientIds.Length];
        for (int i = 0; i < clientIds.Length; i++)
        {
            string who = clientIds[i] == localId ? " (vos)" : "";
            lines[i] = $"Jugador {i + 1}{who}: {cyberdata[i]} Cyberdatos";
        }
        return lines;
    }

    public void RetryLevel()
    {
        // en red se recarga via NetworkSceneManager para los dos
        LoadSceneWithInterstitial(SceneManager.GetActiveScene().name);
    }

    public void ReturnToLevelSelector()
    {
        LoadSceneWithInterstitial(levelSelectorScene);
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
