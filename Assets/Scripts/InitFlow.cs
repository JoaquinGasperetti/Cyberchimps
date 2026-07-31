using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Escena Init: arranca los SDKs (Play Games y AdMob) y recien despues pasa
// al menu, asi el sign-in y el consentimiento caen en una pantalla de carga
// y no encima del menu principal.
public class InitFlow : MonoBehaviour
{
    [SerializeField] private string nextScene = "MainMenu";

    // si algo se cuelga (sin red, sin Play Services) no dejamos al jugador
    // clavado aca: los SDKs siguen inicializando solos en segundo plano
    private const float MaxWait = 12f;

    private IEnumerator Start()
    {
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

        float deadline = Time.realtimeSinceStartup + MaxWait;
        while (Time.realtimeSinceStartup < deadline &&
               !(PlayGamesManager.SignInResolved && AdManager.InitResolved))
            yield return null;

        SceneManager.LoadScene(nextScene);
    }
}
