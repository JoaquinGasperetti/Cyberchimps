using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Versión actualizada del MultiplayerUI con soporte para Join Code.
/// Agregá en el .uxml:
///   - Label con name="LabelJoinCode"
///   - TextField con name="InputJoinCode"
/// </summary>
public class MultiplayerUI : MonoBehaviour
{
    [SerializeField] private UIDocument m_uiDocument;

    private Button m_hostButton, m_clientButton, m_clientDisconnect;
    private Label m_labelJoinCode;
    private TextField m_inputJoinCode;

    public event Action OnStartHost, OnStartClient, OnDiconnectClient;

    private void Awake()
    {
        var root = m_uiDocument.rootVisualElement;

        m_hostButton        = root.Q<Button>("ButtonHost");
        m_clientButton      = root.Q<Button>("ButtonClient");
        m_clientDisconnect  = root.Q<Button>("ButtonDisconnect");
        m_labelJoinCode     = root.Q<Label>("LabelJoinCode");
        m_inputJoinCode     = root.Q<TextField>("InputJoinCode");
    }

    private void Start()
    {
        m_hostButton.clicked       += () => OnStartHost?.Invoke();
        m_clientButton.clicked     += () => OnStartClient?.Invoke();
        m_clientDisconnect.clicked += () => OnDiconnectClient?.Invoke();
        EnableButtons();
    }

    public void DisableButtons()
    {
        m_hostButton.SetEnabled(false);
        m_clientButton.SetEnabled(false);
        m_clientDisconnect.SetEnabled(true);
    }

    public void EnableButtons()
    {
        m_hostButton.SetEnabled(true);
        m_clientButton.SetEnabled(true);
        m_clientDisconnect.SetEnabled(false);
    }

    /// <summary>Muestra el Join Code generado por el host.</summary>
    public void ShowJoinCode(string code)
    {
        if (m_labelJoinCode != null)
            m_labelJoinCode.text = $"Código: {code}";
    }

    /// <summary>Lee el Join Code ingresado por el cliente.</summary>
    public string GetJoinCodeInput()
    {
        return m_inputJoinCode != null ? m_inputJoinCode.value : string.Empty;
    }
}
