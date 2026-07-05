using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sistema de vidas del jugador (3 por defecto), sincronizado en red.
///
/// - Las vidas viven en una NetworkVariable (autoridad del servidor).
/// - Se pierde una vida al caer al agua (KillZone) o al vacío (umbral de Y,
///   chequeado por el servidor — la posición del jugador le llega por su
///   ClientNetworkTransform).
/// - Al perder una vida el jugador vuelve a su punto de spawn: el TELEPORT lo
///   hace el DUEÑO (ClientRpc dirigido) porque el Player es owner-authoritative.
/// - Al quedarse sin vidas: se frena el timer y AMBOS jugadores ven la
///   pantalla de Game Over (GameOverUI) con Reintentar / Volver al Lobby.
/// - La UI de corazones es runtime (solo el jugador local ve la suya) usando
///   los sprites del pack asignados en el prefab del Player.
///
/// SETUP: componente en el prefab Player con los sprites de corazón asignados.
/// No requiere nada en las escenas (el spawn se captura solo).
/// </summary>
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

    // ── Server ────────────────────────────────────────────────────────────
    private Vector3 spawnPosition;
    private float lastLifeLostTime = -10f;
    private const float InvulnerabilityAfterRespawn = 1.5f;
    private bool gameOverTriggered;

    // ── UI local (solo owner) ─────────────────────────────────────────────
    private Canvas livesCanvas;
    private Image[] heartImages;

    // =========================================================
    // INIT
    // =========================================================

    public override void OnNetworkSpawn()
    {
        lives.OnValueChanged += OnLivesChanged;

        if (IsServer)
        {
            // El PlayerSpawnManager instancia al jugador en su spawn point —
            // esa posición es a la que vuelve al perder una vida.
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

    // =========================================================
    // DETECCIÓN DE CAÍDA AL VACÍO (server)
    // =========================================================

    private void Update()
    {
        if (!IsServer || gameOverTriggered) return;

        if (transform.position.y < fallThresholdY)
            LoseLifeFromServer();
    }

    // =========================================================
    // PÉRDIDA DE VIDA — solo en el servidor
    // (llamado por el chequeo de vacío o por KillZone)
    // =========================================================

    public void LoseLifeFromServer()
    {
        if (!IsServer || gameOverTriggered) return;
        if (lives.Value <= 0) return;

        // Ventana de invulnerabilidad: evita perder 2 vidas por el mismo
        // golpe (ej: trigger del agua + umbral de vacío casi simultáneos,
        // o re-trigger mientras la posición sincronizada vuelve al spawn).
        if (Time.time - lastLifeLostTime < InvulnerabilityAfterRespawn) return;
        lastLifeLostTime = Time.time;

        lives.Value--;

        if (lives.Value > 0)
        {
            // El dueño se teletransporta a su spawn (autoridad del owner)
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

    // =========================================================
    // RESPAWN — corre SOLO en el cliente dueño
    // =========================================================

    [ClientRpc]
    private void RespawnOwnerClientRpc(Vector3 position, ClientRpcParams rpcParams = default)
    {
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Teleport explícito para que el otro jugador no vea al player
        // "volando" interpolado por todo el mapa hasta el spawn.
        var netTransform = GetComponent<NetworkTransform>();
        if (netTransform != null)
            netTransform.Teleport(position, transform.rotation, transform.localScale);
        else
            transform.position = position;
    }

    // =========================================================
    // GAME OVER — corre en TODOS los clientes
    // =========================================================

    [ClientRpc]
    private void GameOverClientRpc(ulong loserClientId)
    {
        string loserName = loserClientId == NetworkManager.ServerClientId
            ? "Jugador 1"
            : "Jugador 2";

        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        GameOverUI.Show(loserName, isHost);
    }

    // =========================================================
    // UI DE CORAZONES (solo el jugador local)
    // =========================================================

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
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); // arriba-izquierda
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
