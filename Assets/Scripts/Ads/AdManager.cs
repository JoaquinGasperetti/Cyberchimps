using System;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Administrador central de anuncios (Google AdMob, plugin v11.2.0).
///
/// Se autoinstancia al arrancar el juego (RuntimeInitializeOnLoadMethod) y
/// persiste entre escenas. Maneja los 5 formatos con los IDs del proyecto:
///   - App Open  : al abrir la app y al volver del segundo plano (solo en menús)
///   - Banner    : fijo abajo en las pantallas de menú (MainMenu / Lobby / LevelSelect)
///   - Interstitial : en transiciones que dispara el host (fin de nivel / game over)
///   - Rewarded  : opt-in — revivir en Game Over
///   - Rewarded Interstitial : opt-in — recompensa extra al completar un nivel
///
/// POLÍTICA DE GOOGLE (respetada acá):
///   - Nunca se muestran dos anuncios full-screen a la vez (guard _showingFullScreen).
///   - No se muestran full-screen durante el gameplay activo (solo en menús o en
///     las pantallas de transición fin-de-nivel/game-over).
///   - El banner no tapa los controles: solo aparece en menús, no en el nivel.
///   - Los interstitials tienen un intervalo mínimo para no ser intrusivos.
///   - Las recompensas se otorgan SOLO si el usuario terminó de ver el anuncio.
/// </summary>
public class AdManager : MonoBehaviour
{
    public static AdManager Instance { get; private set; }

    // ── IDs de bloques de anuncios (AdMob) ────────────────────────────────
    private const string AppOpenId             = "ca-app-pub-2266949018056491/4808218513";
    private const string BannerId              = "ca-app-pub-2266949018056491/6464116356";
    private const string InterstitialId        = "ca-app-pub-2266949018056491/1196276561";
    private const string RewardedId            = "ca-app-pub-2266949018056491/3322947690";
    private const string RewardedInterstitialId = "ca-app-pub-2266949018056491/3837953016";

    // Escenas que SÍ son menú (ahí van banner y app-open; el resto es gameplay)
    private static readonly HashSet<string> MenuScenes =
        new HashSet<string> { "MainMenu", "Lobby", "LevelSelect" };

    private const float InterstitialMinInterval = 45f; // segundos entre interstitials

    // ── Estado ────────────────────────────────────────────────────────────
    private bool _initialized;
    private bool _showingFullScreen;      // hay un anuncio full-screen en pantalla
    private bool _wasBackgrounded;        // la app estuvo en segundo plano
    private float _lastInterstitialTime = -999f;

    private BannerView _banner;
    private InterstitialAd _interstitial;
    private RewardedAd _rewarded;
    private RewardedInterstitialAd _rewardedInterstitial;
    private AppOpenAd _appOpen;
    private DateTime _appOpenExpire;

    // =========================================================
    // BOOTSTRAP / INIT
    // =========================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("AdManager");
        go.AddComponent<AdManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // CLAVE: que los callbacks de anuncios se disparen en el hilo principal
        // de Unity — así podemos cargar escenas y tocar la UI desde ellos sin crashear.
        MobileAds.RaiseAdEventsOnUnityMainThread = true;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        MobileAds.Initialize(_ =>
        {
            _initialized = true;

            LoadAppOpen();
            LoadInterstitial();
            LoadRewarded();
            LoadRewardedInterstitial();

            // Banner acorde a la escena actual + app-open de apertura
            RefreshBanner(SceneManager.GetActiveScene().name);
            ShowAppOpenIfReady();
        });
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshBanner(scene.name);
    }

    private static bool CurrentIsMenu()
        => MenuScenes.Contains(SceneManager.GetActiveScene().name);

    // =========================================================
    // APP OPEN — apertura y vuelta del segundo plano
    // =========================================================

    private void OnApplicationPause(bool paused)
    {
        if (paused) _wasBackgrounded = true;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Al volver del segundo plano mostramos App Open (solo en menús, para no
        // interrumpir una partida co-op en curso del otro jugador).
        if (hasFocus && _wasBackgrounded)
        {
            _wasBackgrounded = false;
            if (CurrentIsMenu()) ShowAppOpenIfReady();
        }
    }

    private void LoadAppOpen()
    {
        if (!_initialized) return;
        _appOpen?.Destroy();
        _appOpen = null;

        AppOpenAd.Load(AppOpenId, new AdRequest(), (AppOpenAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null) return;
            _appOpen = ad;
            _appOpenExpire = DateTime.Now + TimeSpan.FromHours(4); // caducan a las 4h
            ad.OnAdFullScreenContentOpened += () => _showingFullScreen = true;
            ad.OnAdFullScreenContentClosed += () => { _showingFullScreen = false; LoadAppOpen(); };
            ad.OnAdFullScreenContentFailed += _ => { _showingFullScreen = false; LoadAppOpen(); };
        });
    }

    private bool AppOpenReady =>
        _appOpen != null && _appOpen.CanShowAd() && DateTime.Now < _appOpenExpire;

    private void ShowAppOpenIfReady()
    {
        if (_showingFullScreen || !AppOpenReady || !CurrentIsMenu()) return;
        _appOpen.Show();
    }

    // =========================================================
    // BANNER — fijo abajo en los menús
    // =========================================================

    private void RefreshBanner(string sceneName)
    {
        if (MenuScenes.Contains(sceneName)) ShowBanner();
        else DestroyBanner();
    }

    private void ShowBanner()
    {
        if (!_initialized) return;
        if (_banner != null) { _banner.Show(); return; }

        _banner = new BannerView(BannerId, AdSize.Banner, AdPosition.Bottom);
        _banner.LoadAd(new AdRequest());
    }

    private void DestroyBanner()
    {
        if (_banner == null) return;
        _banner.Destroy();
        _banner = null;
    }

    // =========================================================
    // INTERSTITIAL — transiciones (lo dispara el host)
    // =========================================================

    private void LoadInterstitial()
    {
        if (!_initialized) return;
        _interstitial?.Destroy();
        _interstitial = null;

        InterstitialAd.Load(InterstitialId, new AdRequest(), (InterstitialAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null) return;
            _interstitial = ad;
            ad.OnAdFullScreenContentOpened += () => _showingFullScreen = true;
            ad.OnAdFullScreenContentClosed += () => { _showingFullScreen = false; LoadInterstitial(); };
            ad.OnAdFullScreenContentFailed += _ => { _showingFullScreen = false; LoadInterstitial(); };
        });
    }

    private Action _onInterstitialClosed;

    /// <summary>
    /// Muestra un interstitial y ejecuta <paramref name="onClosed"/> al cerrarse.
    /// Si no hay anuncio listo / está capado / ya hay un full-screen, ejecuta
    /// <paramref name="onClosed"/> de una (así la transición nunca se traba).
    /// </summary>
    public void ShowInterstitial(Action onClosed = null)
    {
        bool capped = Time.unscaledTime - _lastInterstitialTime < InterstitialMinInterval;
        if (_showingFullScreen || capped || _interstitial == null || !_interstitial.CanShowAd())
        {
            onClosed?.Invoke();
            return;
        }

        _lastInterstitialTime = Time.unscaledTime;
        _onInterstitialClosed = onClosed;

        void Fire()
        {
            _interstitial.OnAdFullScreenContentClosed -= Fire;
            var cb = _onInterstitialClosed; _onInterstitialClosed = null;
            cb?.Invoke();
        }
        _interstitial.OnAdFullScreenContentClosed += Fire;
        _interstitial.OnAdFullScreenContentFailed += _ =>
        {
            var cb = _onInterstitialClosed; _onInterstitialClosed = null;
            cb?.Invoke();
        };
        _interstitial.Show();
    }

    // =========================================================
    // REWARDED — opt-in (revivir)
    // =========================================================

    private void LoadRewarded()
    {
        if (!_initialized) return;
        _rewarded?.Destroy();
        _rewarded = null;

        RewardedAd.Load(RewardedId, new AdRequest(), (RewardedAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null) return;
            _rewarded = ad;
            ad.OnAdFullScreenContentOpened += () => _showingFullScreen = true;
            ad.OnAdFullScreenContentClosed += () => { _showingFullScreen = false; LoadRewarded(); };
            ad.OnAdFullScreenContentFailed += _ => { _showingFullScreen = false; LoadRewarded(); };
        });
    }

    public bool RewardedReady => _rewarded != null && _rewarded.CanShowAd();

    /// <summary>Muestra el rewarded; <paramref name="onReward"/> se llama SOLO si se completó.</summary>
    public void ShowRewarded(Action onReward)
    {
        if (_showingFullScreen || !RewardedReady) return;
        _rewarded.Show(_ => onReward?.Invoke());
    }

    // =========================================================
    // REWARDED INTERSTITIAL — recompensa extra en transición
    // =========================================================

    private void LoadRewardedInterstitial()
    {
        if (!_initialized) return;
        _rewardedInterstitial?.Destroy();
        _rewardedInterstitial = null;

        RewardedInterstitialAd.Load(RewardedInterstitialId, new AdRequest(),
            (RewardedInterstitialAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null) return;
                _rewardedInterstitial = ad;
                ad.OnAdFullScreenContentOpened += () => _showingFullScreen = true;
                ad.OnAdFullScreenContentClosed += () => { _showingFullScreen = false; LoadRewardedInterstitial(); };
                ad.OnAdFullScreenContentFailed += _ => { _showingFullScreen = false; LoadRewardedInterstitial(); };
            });
    }

    public bool RewardedInterstitialReady =>
        _rewardedInterstitial != null && _rewardedInterstitial.CanShowAd();

    /// <summary>Muestra el rewarded interstitial; <paramref name="onReward"/> solo si se completó.</summary>
    public void ShowRewardedInterstitial(Action onReward)
    {
        if (_showingFullScreen || !RewardedInterstitialReady) return;
        _rewardedInterstitial.Show(_ => onReward?.Invoke());
    }

    // =========================================================
    // HELPERS ESTÁTICOS (null-safe para los llamadores)
    // =========================================================

    public static void Interstitial(Action onClosed)
    {
        if (Instance != null) Instance.ShowInterstitial(onClosed);
        else onClosed?.Invoke();
    }

    public static bool CanShowRewarded => Instance != null && Instance.RewardedReady;
    public static void Rewarded(Action onReward) => Instance?.ShowRewarded(onReward);

    public static bool CanShowRewardedInterstitial =>
        Instance != null && Instance.RewardedInterstitialReady;
    public static void RewardedInterstitial(Action onReward) =>
        Instance?.ShowRewardedInterstitial(onReward);
}
