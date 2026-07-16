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
        buttonHost.onClick.AddListener(() => OnStartHost?.Invoke());
        buttonClient.onClick.AddListener(() => OnStartClient?.Invoke());
        buttonDisconnect.onClick.AddListener(() => OnDiconnectClient?.Invoke());
        EnableButtons();
    }

    public void DisableButtons()
    {
        buttonHost.interactable       = false;
        buttonClient.interactable     = false;
        buttonDisconnect.interactable = true;
    }

    public void EnableButtons()
    {
        buttonHost.interactable       = true;
        buttonClient.interactable     = true;
        buttonDisconnect.interactable = false;
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
