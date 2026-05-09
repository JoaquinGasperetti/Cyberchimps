using UnityEngine;

public class LevelSetup : MonoBehaviour
{
    private void Start()
    {
        var allCyberdata = FindObjectsByType<CyberdataCollectible>(FindObjectsSortMode.None);
        int totalCyberdata = allCyberdata.Length;

        if (CyberdataManager.Instance != null)
        {
            CyberdataManager.Instance.StartLevel(totalCyberdata);
        }

        Debug.Log($"Cyberdatos en el nivel: {totalCyberdata}");
    }
}