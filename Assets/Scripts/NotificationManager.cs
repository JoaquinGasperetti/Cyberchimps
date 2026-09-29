using System;
using System.Collections;
using Unity.Notifications;
using UnityEngine;

// Notificaciones locales para traer de vuelta a jugadores que dejaron de
// jugar. No hay backend ni push real: todo se programa desde el propio
// dispositivo con el paquete oficial com.unity.mobile.notifications.
//
// Cadencia y mensajes (investigado: Countly, Helpshift, Udonis, Braze,
// Hubapps — retencion en juegos moviles):
// - Avisos a los 3, 7 y 14 dias sin jugar. Los juegos casuales suelen
//   arrancar el reenganche entre el dia 3 y el 7; a partir de los 14 dias
//   ya se considera "reactivacion" y mandar mas avisos locales sin variar
//   nada solo aumenta la chance de que desinstalen el juego, asi que ahi
//   se corta.
// - Salen a las 20:00 hora local del celular: la franja de la tarde/noche
//   es donde mas se mira el telefono y varias fuentes marcan 20-23hs como
//   pico de apertura de notificaciones.
// - El texto rota entre variantes tematicas (Cyberchimps, el companero de
//   juego, el nivel a medio terminar) para no mandar siempre el mismo
//   mensaje si el jugador entra y sale varias veces.
public class NotificationManager : MonoBehaviour
{
    public static NotificationManager Instance { get; private set; }

    private const string AndroidChannelId = "cyberchimps_reminders";

    // a los cuantos dias sin jugar sale cada aviso
    private static readonly int[] ReminderDaysSinceLastSession = { 3, 7, 14 };
    private const int ReminderHour = 20; // 20:00 hora local del dispositivo

    // ids fijos: permiten cancelar/reemplazar cada aviso individualmente
    private const int BaseNotificationId = 5000;

    private static readonly (string title, string body)[] Messages =
    {
        ("Los Cyberchimps te extrañan",
         "Tu compañero sigue esperando en la selva digital. Volvé a hackear el sistema."),
        ("El sistema sigue sin hackear",
         "Hace unos días que no jugás a Cyberchimps. Retomá donde lo dejaste."),
        ("Un nivel a medio terminar te espera",
         "Tu mono cibernético quedó parado justo donde lo dejaste."),
        ("¿Seguimos la aventura?",
         "Cyberchimps sigue ahí. Volvé cuando quieras retomarla."),
    };

    private bool _channelReady;

    public static void EnsureCreated()
    {
        if (Instance != null) return;
        var go = new GameObject("NotificationManager");
        go.AddComponent<NotificationManager>();
    }

    // se crea solo, en cualquier escena, igual que AdManager
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() => EnsureCreated();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeChannel();
    }

    private void InitializeChannel()
    {
        if (_channelReady) return;
        _channelReady = true;

        var args = NotificationCenterArgs.Default;
        args.AndroidChannelId = AndroidChannelId;
        args.AndroidChannelName = "Recordatorios";
        args.AndroidChannelDescription = "Avisos para volver a jugar a Cyberchimps";
        NotificationCenter.Initialize(args);
    }

    // llamar desde el toggle de Opciones cuando el jugador lo prende o apaga
    public static void SetEnabled(bool on)
    {
        SettingsManager.Notifications = on;

        if (on)
        {
            EnsureCreated();
            Instance.StartCoroutine(Instance.RequestPermissionRoutine());
        }
        else
        {
            CancelReminders();
        }
    }

    private IEnumerator RequestPermissionRoutine()
    {
        InitializeChannel();

        var request = NotificationCenter.RequestPermission();
        if (request.Status == NotificationsPermissionStatus.RequestPending)
            yield return request;

        // si el usuario le dice que no al permiso del sistema operativo,
        // no dejamos el toggle prendido mintiendo
        if (request.Status != NotificationsPermissionStatus.Granted)
            SettingsManager.Notifications = false;
    }

    // el reloj de "dias sin jugar" arranca cada vez que la app se manda
    // a segundo plano; si el jugador vuelve, se cancela todo de nuevo
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            ScheduleReminders();
        else
            CancelReminders();
    }

    private static void ScheduleReminders()
    {
        if (!SettingsManager.Notifications) return;
        if (Instance == null) return;

        Instance.InitializeChannel();

        // por si quedo algo de una pausa anterior sin cancelar
        CancelReminders();

        for (int i = 0; i < ReminderDaysSinceLastSession.Length; i++)
        {
            var (title, body) = Messages[UnityEngine.Random.Range(0, Messages.Length)];

            var notification = new Notification
            {
                Identifier = BaseNotificationId + i,
                Title = title,
                Text = body,
            };

            var when = DateTime.Now.Date
                .AddDays(ReminderDaysSinceLastSession[i])
                .AddHours(ReminderHour);

            NotificationCenter.ScheduleNotification(notification, new NotificationDateTimeSchedule(when));
        }
    }

    private static void CancelReminders()
    {
        for (int i = 0; i < ReminderDaysSinceLastSession.Length; i++)
            NotificationCenter.CancelScheduledNotification(BaseNotificationId + i);
    }
}