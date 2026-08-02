using System.Collections;
using UnityEngine;

// Escena Init: arranca los SDKs (Play Games y AdMob) y recien despues pasa
// al menu, asi el sign-in y el consentimiento caen en una pantalla de carga
// y no encima del menu principal.
public class InitFlow : MonoBehaviour
{
    [SerializeField] private string nextScene = "MainMenu";

    [Tooltip("Arte de fondo de la pantalla de carga. Opcional: sin esto se ve el fondo liso.")]
    [SerializeField] private Sprite loadingBackground;

    // tope de espera: los SDKs terminan de inicializar solos en segundo plano,
    // no tiene sentido clavar al jugador mirando la pantalla de carga.
    // El anuncio de apertura se muestra igual cuando termine de cargar (lo
    // maneja AdManager al llegar al primer menu).
    private const float MaxWait = 6f;

    private IEnumerator Start()
    {
        var loading = LoadingScreenUI.Show(loadingBackground);
        loading.SetStatus("Iniciando...");

        // el AdManager va primero y aparte: si Play Games explota (sin Play
        // Services, sin red) no queremos quedarnos sin anuncios
        AdManager.EnsureCreated();

        try
        {
            PlayGamesManager.EnsureCreated();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[InitFlow] Play Games no arranco: {e.Message}");
        }

        yield return WaitForSdks(loading);

        // de aca en mas manda la pantalla de carga: la carga es Single y
        // destruye esta escena (y este objeto), asi que cualquier cosa que
        // pusieramos despues no llegaria a ejecutarse
        loading.LoadSceneAndHide(nextScene);
    }

    private IEnumerator WaitForSdks(LoadingScreenUI loading)
    {
        float deadline = Time.realtimeSinceStartup + MaxWait;

        // sale apenas los dos SDKs contestan; el tope es solo una red de seguridad
        while (Time.realtimeSinceStartup < deadline &&
               !(PlayGamesManager.SignInResolved && AdManager.InitResolved))
        {
            loading.SetStatus(CurrentStep());

            // los SDKs son la primera mitad de la barra; la otra mitad es la
            // carga del menu. Si tardan, igual se ve avanzar por el tiempo.
            float byTime = 1f - (deadline - Time.realtimeSinceStartup) / MaxWait;
            float byStep = (PlayGamesManager.SignInResolved ? 0.5f : 0f) +
                           (AdManager.InitResolved ? 0.5f : 0f);
            loading.SetProgress(Mathf.Max(byTime, byStep) * 0.5f);

            yield return null;
        }

        loading.SetProgress(0.5f);
    }

    private static string CurrentStep()
    {
        if (!PlayGamesManager.SignInResolved) return "Conectando con Google Play Juegos...";
        if (!AdManager.InitResolved) return "Preparando anuncios...";
        return "Casi listo...";
    }
}
