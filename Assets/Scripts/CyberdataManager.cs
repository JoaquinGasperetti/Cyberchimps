using System;
using UnityEngine;
using Unity.Netcode;

public class CyberdataManager : NetworkBehaviour
{
    public static CyberdataManager Instance { get; private set; }

    [Header("Global")]
    [SerializeField] private int globalCyberdata = 0;

    private NetworkVariable<int> levelCollected = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> levelTotal = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public event Action OnCyberdataChanged;

    public int GlobalCyberdata  => globalCyberdata;
    public int LevelCyberdataCollected => levelCollected.Value;
    public int LevelCyberdataTotal     => levelTotal.Value;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        levelCollected.OnValueChanged += (_, __) => OnCyberdataChanged?.Invoke();
        levelTotal.OnValueChanged     += (_, __) => OnCyberdataChanged?.Invoke();
    }

    public void StartLevel(int totalCyberdata)
    {
        if (!IsServer) return;
        levelTotal.Value     = totalCyberdata;
        levelCollected.Value = 0;
    }

    // solo el server toca este valor
    public void CollectCyberdata()
    {
        if (!IsServer) return;
        if (levelCollected.Value >= levelTotal.Value) return;
        levelCollected.Value++;
    }

    public void CompleteLevel()
    {
        if (!IsServer) return;
        globalCyberdata      += levelCollected.Value;
        levelCollected.Value  = 0;
        OnCyberdataChanged?.Invoke();
    }
}
