using UnityEngine;
using TMPro;
using Unity.Netcode;

public class LevelTimer : NetworkBehaviour
{
    public static LevelTimer Instance { get; private set; }

    [Header("Duración del nivel")]
    [SerializeField] private float levelDuration = 120f;

    [Header("UI (opcional — si preferís que LevelManager maneje el texto, dejar vacío)")]
    [SerializeField] private TMP_Text timerText;

    private NetworkVariable<float> remainingTime = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isRunning = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public float RemainingTime => remainingTime.Value;
    public bool IsRunning => isRunning.Value;

    public event System.Action<float> OnTimeChanged;

    public event System.Action OnTimeUp;

    public event System.Action<float> OnBonusTimeAdded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // destruir solo el componente: el GameObject puede ser el Canvas
            // o un NetworkObject spawneado
            Debug.LogWarning($"[LevelTimer] Duplicado en '{name}' — ya existe uno en '{Instance.name}'. Se elimina el componente duplicado.");
            Destroy(this);
            return;
        }
        Instance = this;
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            remainingTime.Value = levelDuration;
            isRunning.Value = false; // arranca cuando LevelManager llama StartTimer()
        }

        remainingTime.OnValueChanged += HandleTimeChanged;
        UpdateUI(remainingTime.Value);
    }

    public override void OnNetworkDespawn()
    {
        remainingTime.OnValueChanged -= HandleTimeChanged;
    }

    private void HandleTimeChanged(float oldValue, float newValue)
    {
        UpdateUI(newValue);
        OnTimeChanged?.Invoke(newValue);
    }

    public void StartTimer()
    {
        if (!IsServer) return;
        isRunning.Value = true;
    }

    public void StopTimer()
    {
        if (!IsServer) return;
        isRunning.Value = false;
    }

    private void Update()
    {
        if (!IsServer || !isRunning.Value) return;

        remainingTime.Value = Mathf.Max(0f, remainingTime.Value - Time.deltaTime);

        if (remainingTime.Value <= 0f)
        {
            isRunning.Value = false;
            NotifyTimeUpClientRpc();
        }
    }

    [ClientRpc]
    private void NotifyTimeUpClientRpc()
    {
        OnTimeUp?.Invoke();
    }

    public void AddBonusTime(float amount)
    {
        if (!IsServer) return;
        remainingTime.Value += amount;
        NotifyBonusClientRpc(amount);
    }

    [ClientRpc]
    private void NotifyBonusClientRpc(float amount)
    {
        OnBonusTimeAdded?.Invoke(amount);
    }

    private void UpdateUI(float t)
    {
        if (timerText != null)
            timerText.text = GetFormattedTime(t);
    }

    public string GetFormattedTime() => GetFormattedTime(remainingTime.Value);

    private string GetFormattedTime(float t)
    {
        t = Mathf.Max(0f, t);
        int min = Mathf.FloorToInt(t / 60f);
        int sec = Mathf.FloorToInt(t % 60f);
        return string.Format("{0:00}:{1:00}", min, sec);
    }
}
