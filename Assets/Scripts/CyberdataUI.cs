using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

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
        // el player local puede tardar unos frames en spawnear al cargar el nivel
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
