using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkLevelSelectorManager : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Label que indica quién puede elegir. Opcional.")]
    [SerializeField] private TMP_Text labelStatus;

    [Tooltip("Botones de nivel en la escena. Si no se asignan, se buscan automáticamente.")]
    [SerializeField] private Button[] levelButtons;

    private void Start()
    {
        // si no estan asignados, se buscan en la escena
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
