using System;
using UnityEngine;
#if UNITY_ANDROID || UNITY_EDITOR
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

public class PlayGamesManager : MonoBehaviour
{
    public static PlayGamesManager Instance { get; private set; }

    // true cuando el intento de sign-in automatico ya respondio (bien o mal)
    public static bool SignInResolved { get; private set; }
    public static bool SignedIn { get; private set; }

    public static void EnsureCreated()
    {
        if (Instance != null) return;
        var go = new GameObject("PlayGamesManager");
        go.AddComponent<PlayGamesManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Activate deja Play Games como plataforma social por defecto y
        // Authenticate dispara el sign-in automatico. No se puede llamar a
        // ninguna otra API de PGS hasta que este callback conteste Success.
        PlayGamesPlatform.Activate();
        PlayGamesPlatform.Instance.Authenticate(ProcessAuthentication);
#else
        // el plugin v2 no tiene mock de editor: seguimos como no logueado
        SignInResolved = true;
        Debug.Log("[PlayGames] Sign-in salteado (solo funciona en Android).");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void ProcessAuthentication(SignInStatus status)
    {
        SignInResolved = true;
        SignedIn = status == SignInStatus.Success;

        if (SignedIn)
            Debug.Log($"[PlayGames] Sesion iniciada: {PlayGamesPlatform.Instance.GetUserDisplayName()}");
        else
            // puede fallar sin perfil de Play Games, sin red o sin la app
            // de Play Juegos: queda ManualSignIn para reintentar con boton
            Debug.LogWarning($"[PlayGames] Sign-in automatico fallo: {status}");
    }
#endif

    // para un boton "iniciar sesion" en el menu si el automatico fallo
    public static void ManualSignIn(Action<bool> onDone = null)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        PlayGamesPlatform.Instance.ManuallyAuthenticate(status =>
        {
            SignedIn = status == SignInStatus.Success;
            onDone?.Invoke(SignedIn);
        });
#else
        onDone?.Invoke(false);
#endif
    }
}
