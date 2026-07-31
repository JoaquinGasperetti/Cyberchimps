using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MultiplayerUI : MonoBehaviour
{
    [Header("Botones")]
    [SerializeField] private Button buttonHost;
    [SerializeField] private Button buttonClient;
    [SerializeField] private Button buttonDisconnect;

    [Header("Texto e Input")]
    [SerializeField] private TMP_Text labelJoinCode;
    [SerializeField] private TMP_InputField inputJoinCode;

    public event Action OnStartHost;
    public event Action OnStartClient;
    public event Action OnDiconnectClient;

    private void Start()
    {
        // si a una escena le falta el Canvas del HUD estas refs quedan en null;
        // antes eso tiraba NullReference y se caia el Start entero del nivel
        if (buttonHost == null || buttonClient == null || buttonDisconnect == null)
        {
            Debug.LogError($"[MultiplayerUI] Faltan referencias de UI en la escena " +
                           $"'{gameObject.scene.name}'. Revisa que este el prefab Canvas.");
            return;
        }

        buttonHost.onClick.AddListener(() => OnStartHost?.Invoke());
        buttonClient.onClick.AddListener(() => OnStartClient?.Invoke());
        buttonDisconnect.onClick.AddListener(() => OnDiconnectClient?.Invoke());
        EnableButtons();
    }

    public void DisableButtons()
    {
        if (buttonHost != null)       buttonHost.interactable       = false;
        if (buttonClient != null)     buttonClient.interactable     = false;
        if (buttonDisconnect != null) buttonDisconnect.interactable = true;
    }

    public void EnableButtons()
    {
        if (buttonHost != null)       buttonHost.interactable       = true;
        if (buttonClient != null)     buttonClient.interactable     = true;
        if (buttonDisconnect != null) buttonDisconnect.interactable = false;
    }

    public void ShowJoinCode(string code)
    {
        if (labelJoinCode != null)
            labelJoinCode.text = $"IP: {code}";
    }

    public string GetJoinCodeInput()
    {
        return inputJoinCode != null ? inputJoinCode.text : string.Empty;
    }
}
