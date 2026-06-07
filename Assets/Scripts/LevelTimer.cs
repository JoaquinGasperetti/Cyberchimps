using UnityEngine;
using TMPro;
using Unity.Netcode;

public class LevelTimer : NetworkBehaviour
{
    public static LevelTimer Instance { get; private set; }

    [SerializeField] private TMP_Text timerText;

    private NetworkVariable<float> currentTime = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isRunning = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public float CurrentTime => currentTime.Value;
    public bool IsRunning => isRunning.Value;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentTime.Value = 0f;
            isRunning.Value = true;
        }

        currentTime.OnValueChanged += (_, __) => UpdateUI();
        UpdateUI();
    }

    private void Update()
    {
        if (!IsServer || !isRunning.Value) return;
        currentTime.Value += Time.deltaTime;
    }

    public void StopTimer()
    {
        if (!IsServer) return;
        isRunning.Value = false;
        Debug.Log("Nivel completado en: " + GetFormattedTime());
    }

    private void UpdateUI()
    {
        if (timerText != null)
            timerText.text = GetFormattedTime();
    }

    public string GetFormattedTime()
    {
        float t = currentTime.Value;
        int min = Mathf.FloorToInt(t / 60f);
        int sec = Mathf.FloorToInt(t % 60f);
        int ms  = Mathf.FloorToInt((t * 100f) % 100f);
        return string.Format("{0:00}:{1:00}:{2:00}", min, sec, ms);
    }
}
