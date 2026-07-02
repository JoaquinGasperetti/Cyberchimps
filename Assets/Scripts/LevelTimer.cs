using UnityEngine;
using TMPro;
using Unity.Netcode;

/// <summary>
/// Timer de nivel: cuenta regresiva sincronizada para ambos jugadores.
/// Es la ÚNICA fuente de verdad del tiempo restante — el servidor decrementa
/// remainingTime y el NetworkVariable se replica automáticamente a todos los clientes.
///
/// SETUP en Unity:
/// 1. Agregar este script a un GameObject en la escena del nivel (ej: "LevelTimer").
/// 2. Agregarle un componente NetworkObject a ese mismo GameObject
///    (In-Scene Placed NetworkObject → se auto-spawnea al cargar la escena en red).
/// 3. Asignar "Level Duration" (segundos totales del nivel) y opcionalmente el TMP_Text.
/// 4. LevelManager ya se engancha solo a este componente (ver LevelManager.cs).
/// </summary>
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

    /// <summary>Se dispara en TODOS los clientes cada vez que cambia el tiempo restante.</summary>
    public event System.Action<float> OnTimeChanged;

    /// <summary>Se dispara en TODOS los clientes cuando el tiempo llega a 0.</summary>
    public event System.Action OnTimeUp;

    /// <summary>Se dispara en TODOS los clientes cuando se suma tiempo bonus (para mostrar el popup "+5s").</summary>
    public event System.Action<float> OnBonusTimeAdded;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
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

    /// <summary>
    /// Arranca la cuenta regresiva. Llamado por LevelManager cuando termina el
    /// countdown inicial (3-2-1). Solo tiene efecto si lo llama el servidor;
    /// en los clientes es un no-op seguro (por eso se puede llamar sin chequear IsServer afuera).
    /// </summary>
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

    /// <summary>
    /// Suma tiempo extra al timer compartido (bonus del BonusItem).
    /// IMPORTANTE: llamar SOLO desde código que ya corre en el servidor
    /// (ej: TimePickup.OnTriggerEnter, que está guardado con "if (!IsServer) return;").
    /// </summary>
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
