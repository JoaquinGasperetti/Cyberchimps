using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// MultiplayerUI usando Canvas UGUI estándar en lugar de UIDocument/UI Toolkit.
/// Más confiable en Android y no tiene problemas de World Space.
///
/// SETUP en Unity:
/// 1. Creá un Canvas (GameObject → UI → Canvas)
///    - Render Mode: Screen Space - Overlay
///    - UI Scale Mode: Scale With Screen Size, Reference 1080x1920
/// 2. Dentro del Canvas agregá:
///    - Button llamado "ButtonHost"     → texto "Start Host"
///    - Button llamado "ButtonClient"   → texto "Start Client"
///    - Button llamado "ButtonDisconnect" → texto "Disconnect"
///    - Text (TMP) llamado "LabelJoinCode" → texto "IP: ---"
///    - InputField (TMP) llamado "InputJoinCode" → placeholder "Ingresá la IP..."
/// 3. Asigná este script a un GameObject vacío hijo del Canvas o al mismo Canvas.
/// 4. Arrastrá cada elemento a los campos del Inspector.
/// </summary>
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
