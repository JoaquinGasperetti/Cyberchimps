using TMPro;
using UnityEngine;

public class CyberdataUI : MonoBehaviour
{
    [SerializeField] private TMP_Text cyberdataText;
    [SerializeField] private TMP_Text globalText;

    private void OnEnable()
    {
        if (CyberdataManager.Instance != null)
        {
            CyberdataManager.Instance.OnCyberdataChanged += UpdateUI;
            UpdateUI();
        }
    }

    private void Start()
    {
        UpdateUI();
    }

    private void OnDisable()
    {
        if (CyberdataManager.Instance != null)
            CyberdataManager.Instance.OnCyberdataChanged -= UpdateUI;
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