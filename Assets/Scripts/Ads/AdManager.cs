using System;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AdManager : MonoBehaviour
{
    public static AdManager Instance { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // IDs de prueba de Google. Google pide si o si usar anuncios de prueba
    // mientras se desarrolla: si tocas anuncios reales te pueden marcar la
    // cuenta por actividad invalida. Un build normal (sin Development Build)
    // usa los IDs de abajo.
    private const string AppOpenId              = "ca-app-pub-3940256099942544/9257395921";
    private const string BannerId               = "ca-app-pub-3940256099942544/6300978111";
    private const string InterstitialId         = "ca-app-pub-3940256099942544/1033173712";
    private const string RewardedId             = "ca-app-pub-3940256099942544/5224354917";
    private const string RewardedInterstitialId = "ca-app-pub-3940256099942544/5354046379";
#else
    private const string AppOpenId              = "ca-app-pub-2266949018056491/4808218513";
    private const string BannerId               = "ca-app-pub-2266949018056491/6464116356";
    private const string InterstitialId         = "ca-app-pub-2266949018056491/1196276561";
    private const string RewardedId             = "ca-app-pub-2266949018056491/3322947690";
    private const string RewardedInterstitialId = "ca-app-pub-2266949018056491/3837953016";
#endif

    // en estas escenas va el banner y el app open; en los niveles no
    private static readonly HashSet<string> MenuScenes =
        new HashSet<string> { "MainMenu", "Lobby", "LevelSelect" };

    private const float InterstitialMinInterval = 45f; // segundos entre interstitials

    private bool _initialized;
    private bool _showingFullScreen;
    private bool _wasBackgrounded;
    private bool _appOpenPendingOnLaunch;
    private float _lastInterstitialTime = -999f;

    private BannerView _banner;
    private InterstitialAd _interstitial;
    private RewardedAd _rewarded;
    private RewardedInterstitialAd _rewardedInterstitial;
    private AppOpenAd _appOpen;
    private DateTime _appOpenExpire;

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

        // que los callbacks caigan en el hilo principal de Unity,
        // si no no se puede tocar UI ni cargar escenas desde ellos
        MobileAds.RaiseAdEventsOnUnityMainThread = true;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
#if UNITY_EDITOR
        // en el editor no hay UMP: inicializamos directo y el plugin
        // muestra sus anuncios de mentira (placeholders)
        InitializeAds();
#else
        // consentimiento UMP: si hace falta se muestra el formulario
        // antes de inicializar el SDK
        ConsentInformation.Update(new ConsentRequestParameters(), updateError =>
        {
            if (updateError != null)
            {
                // si falla seguimos igual, fuera de Europa el formulario no es obligatorio
                Debug.LogWarning($"[AdManager] UMP update: {updateError.Message}");
                InitializeAds();
                return;
            }

            ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
            {
                if (formError != null)
                    Debug.LogWarning($"[AdManager] UMP form: {formError.Message}");

                if (ConsentInformation.CanRequestAds())
                    InitializeAds();
                else
                    Debug.Log("[AdManager] Sin consentimiento para anuncios — no se inicializa el SDK.");
            });
        });
#endif
    }

    private void InitializeAds()
    {
        if (_initialized) return;

        MobileAds.Initialize(_ =>
        {
            _initialized = true;

            // el app open tarda en cargar: se muestra cuando termine, no aca
            _appOpenPendingOnLaunch = true;

            LoadAppOpen();
            LoadInterstitial();
            LoadRewarded();
            LoadRewardedInterstitial();

            RefreshBanner(SceneManager.GetActiveScene().name);
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

    private void OnApplicationPause(bool paused)
    {
        if (paused) _wasBackgrounded = true;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // al volver de segundo plano va el app open, pero solo en menus
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
            _appOpenExpire = DateTime.Now + TimeSpan.FromHours(4); // vencen a las 4 horas
            ad.OnAdFullScreenContentOpened += () => _showingFullScreen = true;
            ad.OnAdFullScreenContentClosed += () => { _showingFullScreen = false; LoadAppOpen(); };
            ad.OnAdFullScreenContentFailed += _ => { _showingFullScreen = false; LoadAppOpen(); };

            // recien ahora esta listo: si es el arranque, va el de apertura
            if (_appOpenPendingOnLaunch)
            {
                _appOpenPendingOnLaunch = false;
                ShowAppOpenIfReady();
            }
        });
    }

    private bool AppOpenReady =>
        _appOpen != null && _appOpen.CanShowAd() && DateTime.Now < _appOpenExpire;

    private void ShowAppOpenIfReady()
    {
        if (_showingFullScreen || !AppOpenReady || !CurrentIsMenu()) return;
        _appOpen.Show();
        FixEditorPlaceholders();
    }

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
        FixEditorPlaceholders();
    }

    private void DestroyBanner()
    {
        if (_banner == null) return;
        _banner.Destroy();
        _banner = null;
    }

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
            ad.OnAdFullScreenContentClosed += HandleInterstitialFinished;
            ad.OnAdFullScreenContentFailed += _ => HandleInterstitialFinished();
        });
    }

    private Action _onInterstitialClosed;

    // Un solo handler para el cierre: primero avisamos al que pidio el anuncio
    // (asi sigue la transicion) y recien despues recargamos. Con dos handlers
    // separados el reload dejaba _interstitial en null y el otro explotaba,
    // y la transicion no llegaba a ejecutarse nunca.
    private void HandleInterstitialFinished()
    {
        _showingFullScreen = false;

        var cb = _onInterstitialClosed;
        _onInterstitialClosed = null;
        cb?.Invoke();

        LoadInterstitial();
    }

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
        _interstitial.Show();
        FixEditorPlaceholders();
    }

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

    public void ShowRewarded(Action onReward)
    {
        if (_showingFullScreen || !RewardedReady) return;
        _rewarded.Show(_ => onReward?.Invoke());
        FixEditorPlaceholders();
    }

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

    public void ShowRewardedInterstitial(Action onReward)
    {
        if (_showingFullScreen || !RewardedInterstitialReady) return;
        _rewardedInterstitial.Show(_ => onReward?.Invoke());
        FixEditorPlaceholders();
    }

    // los placeholders del plugin vienen con sorting 0 y layout de celu
    // vertical: quedan tapados por nuestra UI y el boton de cerrar puede
    // caer afuera. Los subimos y les ponemos escalado Expand.

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void FixEditorPlaceholders()
    {
        StartCoroutine(FixEditorPlaceholdersRoutine());
    }

    private System.Collections.IEnumerator FixEditorPlaceholdersRoutine()
    {
        // el prefab tarda unos frames en aparecer, insistimos un rato
        for (int i = 0; i < 5; i++)
        {
            foreach (var canvas in FindObjectsByType<Canvas>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string n = canvas.gameObject.name;
                bool fullScreenAd = n.StartsWith("768x1024") || n.StartsWith("1024x768");
                bool bannerAd = n.StartsWith("BANNER") || n.StartsWith("ADAPTIVE")
                    || n.StartsWith("SMART_BANNER") || n.StartsWith("FULL_BANNER")
                    || n.StartsWith("LARGE_BANNER") || n.StartsWith("LEADERBOARD")
                    || n.StartsWith("MEDIUM_RECTANGLE") || n.StartsWith("CENTER");

                if (!fullScreenAd && !bannerAd) continue;

                // arriba de toda nuestra UI. Algunos prefabs ya vienen en 32767
                // y otros en 0 (el app open horizontal, por ejemplo): solo subimos.
                canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 32000);

                // solo los full-screen necesitan el arreglo de escalado
                if (fullScreenAd)
                {
                    var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                    if (scaler != null)
                    {
                        scaler.uiScaleMode =
                            UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                        scaler.screenMatchMode =
                            UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
                    }
                }
            }
            yield return null;
        }
    }

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
