using System;
using UnityEngine;
using Unity.Netcode;

public class PlayerCyberdataWallet : NetworkBehaviour
{
    private const string TotalWalletKey = "CyberChimps_CyberdataWallet";

    private NetworkVariable<int> levelCyberdata = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Owner,
        NetworkVariableWritePermission.Server
    );

    public int LevelCyberdata => levelCyberdata.Value;

    public int TotalWallet { get; private set; }

    public event Action<int> OnLevelCyberdataChanged;

    public event Action<int> OnTotalWalletChanged;

    public override void OnNetworkSpawn()
    {
        levelCyberdata.OnValueChanged += HandleLevelCyberdataChanged;

        if (IsOwner)
        {
            TotalWallet = PlayerPrefs.GetInt(TotalWalletKey, 0);
            OnTotalWalletChanged?.Invoke(TotalWallet);
        }
    }

    public override void OnNetworkDespawn()
    {
        levelCyberdata.OnValueChanged -= HandleLevelCyberdataChanged;
    }

    public void AddCyberdata(int amount = 1)
    {
        if (!IsServer) return;
        levelCyberdata.Value += amount;
    }

    public void ResetLevelCount()
    {
        if (!IsServer) return;
        levelCyberdata.Value = 0;
    }

    public void GrantAdBonus(int amount)
    {
        if (!IsOwner || amount <= 0) return;
        TotalWallet += amount;
        PlayerPrefs.SetInt(TotalWalletKey, TotalWallet);
        PlayerPrefs.Save();
        OnTotalWalletChanged?.Invoke(TotalWallet);
    }

    public static PlayerCyberdataWallet LocalWallet
    {
        get
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null)
                return null;
            return nm.LocalClient.PlayerObject.GetComponent<PlayerCyberdataWallet>();
        }
    }

    private void HandleLevelCyberdataChanged(int oldValue, int newValue)
    {
        // esto llega al dueño y al server; si somos host del otro jugador
        // no hay que guardar su moneda aca
        if (!IsOwner) return;

        int delta = newValue - oldValue;
        if (delta > 0)
        {
            TotalWallet += delta;
            PlayerPrefs.SetInt(TotalWalletKey, TotalWallet);
            PlayerPrefs.Save();
            OnTotalWalletChanged?.Invoke(TotalWallet);
        }

        OnLevelCyberdataChanged?.Invoke(newValue);
    }
}
