using TMPro;
using UnityEngine;

public class CyberdataUI : MonoBehaviour
{
    [SerializeField] private TMP_Text cyberdataText;
    [SerializeField] private TMP_Text globalText;

    private void Start()
    {
        // Al hacerlo en el Start, nos aseguramos de que el Awake 
        // del CyberdataManager ya se ejecutó y la Instance existe.
        if (CyberdataManager.Instance != null)
        {
            CyberdataManager.Instance.OnCyberdataChanged += UpdateUI;
            UpdateUI(); // Actualiza los textos por primera vez al cargar
        }
    }

    private void OnDestroy()
    {
        // Es buena práctica desuscribirse al destruirse, 
        // en lugar de OnDisable, para evitar errores al cambiar de escena.
        if (CyberdataManager.Instance != null)
        {
            CyberdataManager.Instance.OnCyberdataChanged -= UpdateUI;
        }
    }

    private void UpdateUI()
    {
        if (CyberdataManager.Instance == null) return;
        if (cyberdataText == null || globalText == null) return;

        cyberdataText.text =
            $"{CyberdataManager.Instance.LevelCyberdataCollected}/{CyberdataManager.Instance.LevelCyberdataTotal} cyberdatos";

        globalText.text =
            $"Global: {CyberdataManager.Instance.GlobalCyberdata}";
    }
}