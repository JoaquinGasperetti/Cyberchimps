using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelCompleteUI : MonoBehaviour
{
    private static LevelCompleteUI instance;

    public static void Show(
        string formattedTime, int earnedStars, string[] playerStatLines,
        bool isHost, bool hasNextLevel, Action onLobby, Action onNextLevel)
    {
        if (instance != null) return; // ya visible

        var canvas = SimpleUI.CreateOverlayCanvas("LevelCompleteUI", 400);
        instance = canvas.gameObject.AddComponent<LevelCompleteUI>();
        instance.Build(formattedTime, earnedStars, playerStatLines,
            isHost, hasNextLevel, onLobby, onNextLevel);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Build(
        string formattedTime, int earnedStars, string[] playerStatLines,
        bool isHost, bool hasNextLevel, Action onLobby, Action onNextLevel)
    {
        SimpleUI.CreateOverlay(transform);

        var panel = SimpleUI.CreatePanel(transform, new Vector2(780f, 700f));
        Transform p = panel.transform;

        SimpleUI.CreateText(p, "Title", "¡NIVEL COMPLETADO!", 60f,
            new Vector2(0f, 265f), new Vector2(720f, 80f));

        SimpleUI.CreateText(p, "Time", $"Tiempo restante: {formattedTime}", 38f,
            new Vector2(0f, 185f), new Vector2(720f, 55f));

        SimpleUI.CreateText(p, "Stars", $"Estrellas: {earnedStars} / 3", 38f,
            new Vector2(0f, 130f), new Vector2(720f, 55f));

        float y = 60f;
        if (playerStatLines != null)
        {
            foreach (string line in playerStatLines)
            {
                var text = SimpleUI.CreateText(p, "PlayerStat", line, 34f,
                    new Vector2(0f, y), new Vector2(720f, 50f));
                text.color = new Color(0.75f, 0.95f, 1f, 1f);
                y -= 52f;
            }
        }

        BuildAdBonusButton(p);

        if (isHost)
        {
            var size = new Vector2(440f, 90f);

            SimpleUI.CreateButton(p, "ButtonLobby", "Volver al Lobby",
                new Vector2(0f, hasNextLevel ? -130f : -190f), size,
                SimpleUI.BlueButton, () => onLobby?.Invoke());

            if (hasNextLevel)
            {
                SimpleUI.CreateButton(p, "ButtonNext", "Siguiente nivel",
                    new Vector2(0f, -240f), size,
                    SimpleUI.GreenButton, () => onNextLevel?.Invoke());
            }
        }
        else
        {
            SimpleUI.CreateText(p, "Waiting", "Esperando al host...", 36f,
                new Vector2(0f, -190f), new Vector2(720f, 60f))
                .color = new Color(1f, 1f, 1f, 0.7f);
        }
    }

    private void BuildAdBonusButton(Transform parent)
    {
        var wallet = PlayerCyberdataWallet.LocalWallet;
        int collected = wallet != null ? wallet.LevelCyberdata : 0;
        if (collected <= 0) return; // sin cyberdatos no hay nada que duplicar

        var bonusBtn = SimpleUI.CreateButton(parent, "ButtonAdBonus",
            $"x2 Cyberdatos (+{collected}) — Anuncio",
            new Vector2(0f, -45f), new Vector2(520f, 70f),
            new Color(0.85f, 0.62f, 0.12f, 1f), null);

        bonusBtn.onClick.AddListener(() => OnAdBonusClicked(bonusBtn, collected));

        // si el anuncio todavia no cargo, se ve deshabilitado
        bonusBtn.interactable = AdManager.CanShowRewardedInterstitial;
    }

    private void OnAdBonusClicked(Button bonusBtn, int amount)
    {
        bonusBtn.interactable = false; // evitar doble uso

        AdManager.RewardedInterstitial(() =>
        {
            // se acredita unicamente si termino de ver el anuncio
            PlayerCyberdataWallet.LocalWallet?.GrantAdBonus(amount);

            var label = bonusBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = "¡Cyberdatos duplicados!";
        });

        // si justo no habia anuncio, no paso nada: lo rehabilitamos
        if (!AdManager.CanShowRewardedInterstitial) bonusBtn.interactable = true;
    }
}
