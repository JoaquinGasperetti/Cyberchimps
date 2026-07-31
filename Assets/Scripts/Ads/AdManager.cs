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
    private const float MaxRetryDelay = 64f; // tope del backoff al reintentar cargas

    // Para probar los IDs REALES en un celu fisico sin riesgo de actividad
    // invalida: corre el juego una vez en el dispositivo, busca en logcat la
    // linea que dice "RequestConfiguration.Builder.setTestDeviceIds" y pega
    // aca el hash que muestra. AdMob pasa a servir anuncios de prueba sobre
    // los ad units reales solo en ese dispositivo.
    private static readonly List<string> TestDeviceIds = new List<string>();

    private bool _initStarted;  // ya se llamo a MobileAds.Initialize
    private bool _initialized;  // el SDK contesto que esta listo
    private bool _initResolved; // el arranque (UMP + SDK) ya termino, bien o mal
    private bool _showingFullScreen;
    private bool _wasBackgrounded;
    private bool _appOpenPendingOnLaunch;
    private float _lastInterstitialTime = -999f;

    private int _appOpenRetries;
    private int _interstitialRetries;
    private int _rewardedRetries;
    private int _rewardedInterstitialRetries;
    private int _bannerRetries;

    private BannerView _banner;
    private InterstitialAd _interstitial;
    private RewardedAd _rewarded;
    private RewardedInterstitialAd _rewardedInterstitial;
    private AppOpenAd _appOpen;
    private DateTime _appOpenExpire;

    // para que la escena Init sepa cuando puede seguir al menu
    public static bool InitResolved => Instance != null && Instance._initResolved;

    public static void EnsureCreated()
    {
        if (Instance != null) return;
        var go = new GameObject("AdManager");
        go.AddComponent<AdManager>();
    }

    // Se crea siempre, en cualquier plataforma y sin depender de la escena
    // Init: si alguna vez se arranca en otra escena (o Init cambia), igual
    // hay anuncios. EnsureCreated es idempotente.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() => EnsureCreated();

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
        // Red de seguridad: pase lo que pase con UMP, el SDK se inicializa.
        // Sin esto, cualquier excepcion o callback que nunca vuelve dejaba la
        // app publicada sin un solo anuncio, y en el editor no se notaba
        // porque esta rama ni corre.
        StartCoroutine(InitWatchdog());

        try
        {
            RequestConsentThenInitialize();
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdManager] UMP tiro excepcion ({e.Message}) — inicializamos igual.");
            InitializeAds();
        }
#endif
    }

#if !UNITY_EDITOR
    private void RequestConsentThenInitialize()
    {
        // consentimiento UMP: si hace falta se muestra el formulario
        // antes de inicializar el SDK
        ConsentInformation.Update(new ConsentRequestParameters(), updateError =>
        {
            try
            {
                if (updateError != null)
                {
                    // si falla seguimos igual, fuera de Europa el formulario no es obligatorio
                    Debug.LogWarning($"[AdManager] UMP update fallo ({updateError.Message}) — inicializamos igual.");
                    InitializeAds();
                    return;
                }

                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    try
                    {
                        if (formError != null)
                            Debug.LogWarning($"[AdManager] UMP form: {formError.Message}");

                        if (ConsentInformation.CanRequestAds())
                        {
                            InitializeAds();
                        }
                        else
                        {
                            // aca no hay anuncios posibles: pasa si el usuario esta en
                            // una region con consentimiento obligatorio y el mensaje
                            // GDPR no esta publicado en la consola de AdMob
                            _initResolved = true;
                            Debug.LogError("[AdManager] UMP: no se pueden pedir anuncios " +
                                $"(ConsentStatus={ConsentInformation.ConsentStatus}). " +
                                "Revisa que el mensaje de consentimiento este publicado en AdMob.");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[AdManager] UMP form callback fallo ({e.Message}) — inicializamos igual.");
                        InitializeAds();
                    }
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[AdManager] UMP update callback fallo ({e.Message}) — inicializamos igual.");
                InitializeAds();
            }
        });
    }

    // si a los 8s UMP no contesto (sin red, formulario mal configurado, callback
    // que nunca vuelve), arrancamos igual: sin consentimiento explicito el SDK
    // sirve anuncios no personalizados, pero sirve.
    private System.Collections.IEnumerator InitWatchdog()
    {
        yield return new WaitForSecondsRealtime(8f);

        if (!_initStarted)
        {
            Debug.LogWarning("[AdManager] UMP no respondio en 8s — inicializando el SDK igual.");
            InitializeAds();
        }
    }
#endif

    private void InitializeAds()
    {
        // _initialized recien se pone en true dentro del callback: sin este
        // segundo guard, UMP y el watchdog podian llamar a Initialize dos veces
        if (_initStarted) return;
        _initStarted = true;

        if (TestDeviceIds.Count > 0)
            MobileAds.SetRequestConfiguration(new RequestConfiguration { TestDeviceIds = TestDeviceIds });

        MobileAds.Initialize(initStatus =>
        {
            _initialized = true;
            _initResolved = true;

            foreach (var kv in initStatus.getAdapterStatusMap())
                Debug.Log($"[AdManager] Adapter {kv.Key}: {kv.Value.InitializationState} ({kv.Value.Description})");

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

        // el de apertura pudo terminar de cargar durante la escena Init:
        // se muestra al llegar al primer menu
        if (_appOpenPendingOnLaunch && MenuScenes.Contains(scene.name) && AppOpenReady)
        {
            _appOpenPendingOnLaunch = false;
            ShowAppOpenIfReady();
        }
    }

    private static bool CurrentIsMenu()
        => MenuScenes.Contains(SceneManager.GetActiveScene().name);

    private void OnApplicationPause(bool paused)
    {
        // un anuncio fullscreen tambien pausa la app: eso no cuenta como ir a
        // segundo plano, si no al cerrarlo saltaba un app open encima
        if (paused && !_showingFullScreen) _wasBackgrounded = true;
    }

    // en dispositivo real es normal que una carga falle (red, no-fill);
    // sin reintento el formato quedaba muerto para toda la sesion
    private void RetryLoad(int attempt, Action reload)
    {
        float delay = Mathf.Min(Mathf.Pow(2f, attempt), MaxRetryDelay);
        StartCoroutine(RetryLoadRoutine(delay, reload));
    }

    private System.Collections.IEnumerator RetryLoadRoutine(float delay, Action reload)
    {
        yield return new WaitForSecondsRealtime(delay);
        reload();
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
            if (error != null || ad == null)
            {
                Debug.LogError($"[AdManager] AppOpen no cargo: {error}");
                RetryLoad(++_appOpenRetries, LoadAppOpen);
                return;
            }
            _appOpenRetries = 0;
            _appOpen = ad;
            _appOpenExpire = DateTime.Now + TimeSpan.FromHours(4); // vencen a las 4 horas
            ad.OnAdFullScreenContentOpened += () => _showingFullScreen = true;
            ad.OnAdFullScreenContentClosed += () => { _showingFullScreen = false; LoadAppOpen(); };
            ad.OnAdFullScreenContentFailed += err =>
            {
                Debug.LogError($"[AdManager] AppOpen no se pudo mostrar: {err}");
                _showingFullScreen = false;
                LoadAppOpen();
            };

            // recien ahora esta listo: si es el arranque y ya estamos en un
            // menu, va el de apertura. Si seguimos en la escena Init queda
            // pendiente y lo dispara OnSceneLoaded al llegar al primer menu.
            if (_appOpenPendingOnLaunch && CurrentIsMenu())
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
        _banner.OnBannerAdLoaded += () => _bannerRetries = 0;
        _banner.OnBannerAdLoadFailed += error =>
        {
            Debug.LogError($"[AdManager] Banner no cargo: {error}");
            DestroyBanner();
            RetryLoad(++_bannerRetries, () => { if (CurrentIsMenu()) ShowBanner(); });
        };
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
            if (error != null || ad == null)
            {
                Debug.LogError($"[AdManager] Interstitial no cargo: {error}");
                RetryLoad(++_interstitialRetries, LoadInterstitial);
                return;
            }
            _interstitialRetries = 0;
            _interstitial = ad;
            ad.OnAdFullScreenContentOpened += () => _showingFullScreen = true;
            ad.OnAdFullScreenContentClosed += HandleInterstitialFinished;
            ad.OnAdFullScreenContentFailed += err =>
            {
                Debug.LogError($"[AdManager] Interstitial no se pudo mostrar: {err}");
                HandleInterstitialFinished();
            };
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
            if (error != null || ad == null)
            {
                Debug.LogError($"[AdManager] Rewarded no cargo: {error}");
                RetryLoad(++_rewardedRetries, LoadRewarded);
                return;
            }
            _rewardedRetries = 0;
            _rewarded = ad;
            ad.OnAdFullScreenContentOpened += () => _showingFullScreen = true;
            ad.OnAdFullScreenContentClosed += () => { _showingFullScreen = false; LoadRewarded(); };
            ad.OnAdFullScreenContentFailed += err =>
            {
                Debug.LogError($"[AdManager] Rewarded no se pudo mostrar: {err}");
                _showingFullScreen = false;
                LoadRewarded();
            };
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
                if (error != null || ad == null)
                {
                    Debug.LogError($"[AdManager] RewardedInterstitial no cargo: {error}");
                    RetryLoad(++_rewardedInterstitialRetries, LoadRewardedInterstitial);
                    return;
                }
                _rewardedInterstitialRetries = 0;
                _rewardedInterstitial = ad;
                ad.OnAdFullScreenContentOpened += () => _showingFullScreen = true;
                ad.OnAdFullScreenContentClosed += () => { _showingFullScreen = false; LoadRewardedInterstitial(); };
                ad.OnAdFullScreenContentFailed += err =>
                {
                    Debug.LogError($"[AdManager] RewardedInterstitial no se pudo mostrar: {err}");
                    _showingFullScreen = false;
                    LoadRewardedInterstitial();
                };
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
