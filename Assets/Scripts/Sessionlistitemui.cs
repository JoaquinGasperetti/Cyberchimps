using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Fila de la lista de partidas publicas del Lobby.
// Va en el prefab "SessionListItem".
public class SessionListItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text labelName;
    [SerializeField] private TMP_Text labelSlots;
    [SerializeField] private Button buttonJoin;

    public void Setup(string sessionId, string sessionName, int players, int maxPlayers, Action<string> onJoin)
    {
        if (labelName != null)
            labelName.text = string.IsNullOrWhiteSpace(sessionName) ? "Partida" : sessionName;

        if (labelSlots != null)
            labelSlots.text = $"{players}/{maxPlayers}";

        if (buttonJoin == null) return;

        buttonJoin.onClick.RemoveAllListeners();
        buttonJoin.onClick.AddListener(() => onJoin?.Invoke(sessionId));
    }
}