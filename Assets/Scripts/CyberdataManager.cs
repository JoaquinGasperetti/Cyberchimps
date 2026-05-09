using System;
using UnityEngine;

public class CyberdataManager : MonoBehaviour
{
    public static CyberdataManager Instance { get; private set; }

    [Header("Global")]
    [SerializeField] private int globalCyberdata = 0;

    [Header("Nivel actual")]
    [SerializeField] private int levelCyberdataCollected = 0;
    [SerializeField] private int levelCyberdataTotal = 0;

    public event Action OnCyberdataChanged;

    public int GlobalCyberdata => globalCyberdata;
    public int LevelCyberdataCollected => levelCyberdataCollected;
    public int LevelCyberdataTotal => levelCyberdataTotal;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartLevel(int totalCyberdata)
    {
        levelCyberdataTotal = totalCyberdata;
        levelCyberdataCollected = 0;
        OnCyberdataChanged?.Invoke();
    }

    public void CollectCyberdata()
    {
        if (levelCyberdataCollected >= levelCyberdataTotal)
            return;

        levelCyberdataCollected++;
        OnCyberdataChanged?.Invoke();
    }

    public void CompleteLevel()
    {
        globalCyberdata += levelCyberdataCollected;
        levelCyberdataCollected = 0;
        OnCyberdataChanged?.Invoke();
    }
}