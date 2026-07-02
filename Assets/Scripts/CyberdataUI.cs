using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// HUD de Cyberdatos del jugador LOCAL (cada dispositivo muestra solo lo suyo).
/// Se busca el PlayerCyberdataWallet del jugador propio (dueño local) y nos
/// suscribimos a sus eventos para reflejar en tiempo real:
///  - cuántos Cyberdatos juntó ESTE jugador en el nivel actual
///  - el total acumulado guardado en el dispositivo (moneda del juego)
///
/// SETUP en Unity:
/// 1. Asignar "Cyberdata Text" (progreso del nivel) y "Global Text" (moneda total)
///    en el Canvas de cada jugador/HUD.
/// 2. No hace falta más setup — se auto-engancha al Player local cuando spawnea.
/// </summary>
public class CyberdataUI : MonoBehaviour
{
    [SerializeField] public TMP_Text cyberdataText; // ej: "3 cyberdatos" (este nivel)
    [SerializeField] public TMP_Text globalText;     // ej: "Total: 42" (moneda guardada)

    private PlayerCyberdataWallet wallet;

    private void Start()
    {
        StartCoroutine(BindToLocalWallet());
    }

    private void OnDestroy()
    {
        if (wallet != null)
        {
            wallet.OnLevelCyberdataChanged -= UpdateLevelText;
            wallet.OnTotalWalletChanged -= UpdateTotalText;
        }
    }

    private IEnumerator BindToLocalWallet()
    {
        // Esperamos a que el jugador local esté spawneado — puede tardar
        // un par de frames tras cargar el nivel (ver PlayerSpawnManager).
        while (NetworkManager.Singleton == null ||
               NetworkManager.Singleton.LocalClient == null ||
               NetworkManager.Singleton.LocalClient.PlayerObject == null)
        {
            yield return null;
        }

        wallet = NetworkManager.Singleton.LocalClient.PlayerObject
            .GetComponent<PlayerCyberdataWallet>();

        if (wallet == null)
        {
            Debug.LogWarning("[CyberdataUI] El Player local no tiene PlayerCyberdataWallet.");
            yield break;
        }

        wallet.OnLevelCyberdataChanged += UpdateLevelText;
        wallet.OnTotalWalletChanged += UpdateTotalText;

        UpdateLevelText(wallet.LevelCyberdata);
        UpdateTotalText(wallet.TotalWallet);
    }

    private void UpdateLevelText(int levelCount)
    {
        if (cyberdataText == null) return;
        cyberdataText.text = $"{levelCount} cyberdatos";
    }

    private void UpdateTotalText(int total)
    {
        if (globalText == null) return;
        globalText.text = $"Total: {total}";
    }
}
