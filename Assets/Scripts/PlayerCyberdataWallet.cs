using System;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Monedero de Cyberdatos PERSONAL de cada jugador. Se agrega al Player prefab
/// (una instancia por jugador — cada uno lleva la suya).
///
/// - levelCyberdata: cuántos juntó ESTE jugador en el nivel actual.
///   NetworkVariable con ReadPermission.Owner: solo el propio dueño (y el
///   servidor) reciben las actualizaciones. El otro jugador no ve ni puede
///   leer este valor — es privado de cada uno, como pide el diseño.
///
/// - TotalWallet: el total acumulado (moneda del juego), persistido con
///   PlayerPrefs en el dispositivo LOCAL del dueño. Cada jugador guarda su
///   propia moneda en su propio dispositivo/cliente.
///
/// SETUP en Unity:
/// 1. Agregar este componente al Player.prefab.
/// 2. No requiere referencias en el Inspector.
/// 3. El HUD (CyberdataUI.cs) se engancha solo, buscando el wallet del jugador local.
/// </summary>
public class PlayerCyberdataWallet : NetworkBehaviour
{
    private const string TotalWalletKey = "CyberChimps_CyberdataWallet";

    private NetworkVariable<int> levelCyberdata = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Owner,
        NetworkVariableWritePermission.Server
    );

    /// <summary>Cuántos Cyberdatos junté YO en el nivel actual (solo válido para el dueño local).</summary>
    public int LevelCyberdata => levelCyberdata.Value;

    /// <summary>Total histórico guardado en ESTE dispositivo (solo válido para el dueño local).</summary>
    public int TotalWallet { get; private set; }

    /// <summary>Se dispara (solo en el dueño local) cada vez que cambia lo juntado este nivel.</summary>
    public event Action<int> OnLevelCyberdataChanged;

    /// <summary>Se dispara (solo en el dueño local) cada vez que cambia la moneda total guardada.</summary>
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

    /// <summary>
    /// Llamado SOLO por código que corre en el servidor (ej: CyberdataCollectible)
    /// cuando este jugador específico recolecta un Cyberdato.
    /// </summary>
    public void AddCyberdata(int amount = 1)
    {
        if (!IsServer) return;
        levelCyberdata.Value += amount;
    }

    /// <summary>Resetea el contador del nivel (opcional — normalmente no hace falta
    /// porque el Player se re-spawnea entero en cada nivel nuevo).</summary>
    public void ResetLevelCount()
    {
        if (!IsServer) return;
        levelCyberdata.Value = 0;
    }

    private void HandleLevelCyberdataChanged(int oldValue, int newValue)
    {
        // Este callback solo llega al dueño y al servidor (ReadPermission.Owner).
        // Si el servidor es host del OTRO jugador, no queremos guardar su moneda acá.
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
