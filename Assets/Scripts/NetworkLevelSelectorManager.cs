using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reemplaza al LevelSelectorManager en la escena LevelSelect.
/// Solo el host puede presionar los botones de nivel.
/// El cliente ve los botones desactivados con un mensaje de espera.
/// 
/// SETUP en Unity:
/// 1. En la escena LevelSelect, reemplazá el componente LevelSelectorManager
///    por este script en el mismo GameObject.
/// 2. Asigná el LabelStatus en el Inspector (un TMP_Text que diga
///    "Sos el host - elegí un nivel" o "Esperando al host...").
/// 3. Los botones de nivel ya existentes NO necesitan cambios —
///    este script los detecta automáticamente y bloquea los del cliente.
/// </summary>
public class NetworkLevelSelectorManager : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Label que indica quién puede elegir. Opcional.")]
    [SerializeField] private TMP_Text labelStatus;

    [Tooltip("Botones de nivel en la escena. Si no se asignan, se buscan automáticamente.")]
    [SerializeField] private Button[] levelButtons;

    private void Start()
    {
        // Si no se asignaron manualmente, buscar todos los botones en escena
        if (levelButtons == null || levelButtons.Length == 0)
            levelButtons = FindObjectsByType<Button>(FindObjectsSortMode.None);

        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        foreach (var btn in levelButtons)
            btn.interactable = isHost;

        if (labelStatus != null)
        {
            labelStatus.text = isHost
                ? "Elegí el nivel"
                : "Esperando al host...";
        }
    }

    /// <summary>
    /// Llamado por los botones de nivel en la escena.
    /// Solo ejecuta la carga si sos el host.
    /// </summary>
    public void LoadLevel(string sceneName)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("[NetworkLevelSelectorManager] Solo el host puede cargar niveles.");
            return;
        }

        NetworkSceneLoader.Instance.LoadScene(sceneName);
    }
}
