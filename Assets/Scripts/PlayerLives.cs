using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.UI;

public class PlayerLives : NetworkBehaviour
{
    [Header("Vidas")]
    [SerializeField] private int maxLives = 3;

    [Header("Caída al vacío")]
    [Tooltip("Si el jugador cae por debajo de esta Y (mundo), pierde una vida")]
    [SerializeField] private float fallThresholdY = -8f;

    [Header("UI (sprites del pack)")]
    [SerializeField] private Sprite fullHeartSprite;
    [SerializeField] private Sprite emptyHeartSprite;

    private NetworkVariable<int> lives = new NetworkVariable<int>(
        3,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public int CurrentLives => lives.Value;

    private Vector3 spawnPosition;
    private float lastLifeLostTime = -10f;
    private const float InvulnerabilityAfterRespawn = 1.5f;
    private bool gameOverTriggered;

    private Canvas livesCanvas;
    private Image[] heartImages;

    public override void OnNetworkSpawn()
    {
        lives.OnValueChanged += OnLivesChanged;

        if (IsServer)
        {
            // el spawn point donde lo puso PlayerSpawnManager: ahi vuelve al morir
            spawnPosition = transform.position;
            lives.Value = maxLives;
        }

        if (IsOwner)
        {
            BuildLivesUI();
            RefreshLivesUI(lives.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        lives.OnValueChanged -= OnLivesChanged;

        if (livesCanvas != null)
            Destroy(livesCanvas.gameObject);
    }

    private void Update()
    {
        if (!IsServer || gameOverTriggered) return;

        if (transform.position.y < fallThresholdY)
            LoseLifeFromServer();
    }

    public void LoseLifeFromServer()
    {
        if (!IsServer || gameOverTriggered) return;
        if (lives.Value <= 0) return;

        // ventana de gracia para no perder dos vidas por el mismo golpe
        if (Time.time - lastLifeLostTime < InvulnerabilityAfterRespawn) return;
        lastLifeLostTime = Time.time;

        lives.Value--;

        if (lives.Value > 0)
        {
            RespawnOwnerClientRpc(spawnPosition, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            });
        }
        else
        {
            gameOverTriggered = true;
            LevelTimer.Instance?.StopTimer();
            GameOverClientRpc(OwnerClientId);
        }
    }

    [ClientRpc]
    private void RespawnOwnerClientRpc(Vector3 position, ClientRpcParams rpcParams = default)
    {
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // teleport para que el otro no lo vea volar por el mapa hasta el spawn
        var netTransform = GetComponent<NetworkTransform>();
        if (netTransform != null)
            netTransform.Teleport(position, transform.rotation, transform.localScale);
        else
            transform.position = position;
    }

    [ClientRpc]
    private void GameOverClientRpc(ulong loserClientId)
    {
        string loserName = loserClientId == NetworkManager.ServerClientId
            ? "Jugador 1"
            : "Jugador 2";

        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        // solo el que perdio puede revivir mirando un anuncio
        bool isLoser = NetworkManager.Singleton != null
            && loserClientId == NetworkManager.Singleton.LocalClientId;

        GameOverUI.Show(loserName, isHost, isLoser);
    }

    public void RequestReviveFromAd()
    {
        if (!IsOwner) return;
        ReviveServerRpc();
    }

    [ServerRpc]
    private void ReviveServerRpc()
    {
        if (!gameOverTriggered) return;

        gameOverTriggered = false;
        lives.Value = 1;
        lastLifeLostTime = Time.time; // gracia para no volver a morir al instante

        LevelTimer.Instance?.StartTimer();

        RespawnOwnerClientRpc(spawnPosition, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        });
        HideGameOverClientRpc();
    }

    [ClientRpc]
    private void HideGameOverClientRpc()
    {
        GameOverUI.Hide();
    }

    private void OnLivesChanged(int previous, int current)
    {
        if (IsOwner)
            RefreshLivesUI(current);
    }

    private void BuildLivesUI()
    {
        livesCanvas = SimpleUI.CreateOverlayCanvas("LivesUI", 300);

        heartImages = new Image[maxLives];
        for (int i = 0; i < maxLives; i++)
        {
            var go = new GameObject($"Heart{i + 1}");
            go.transform.SetParent(livesCanvas.transform, false);

            var img = go.AddComponent<Image>();
            img.sprite = fullHeartSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(64f, 64f);
            rt.anchoredPosition = new Vector2(70f + i * 72f, -140f);

            heartImages[i] = img;
        }
    }

    private void RefreshLivesUI(int current)
    {
        if (heartImages == null) return;

        for (int i = 0; i < heartImages.Length; i++)
        {
            if (heartImages[i] == null) continue;

            if (emptyHeartSprite != null)
                heartImages[i].sprite = i < current ? fullHeartSprite : emptyHeartSprite;
            else
                heartImages[i].enabled = i < current;
        }
    }
}
