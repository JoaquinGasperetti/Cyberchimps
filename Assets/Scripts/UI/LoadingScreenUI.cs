using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pantalla de carga del arranque. Se arma por codigo (mismo patron que el
// resto de la UI) y sobrevive al cambio de escena para tapar tambien la carga
// del menu: sin esto el jugador veia la pantalla en negro varios segundos
// mientras inicializaban los SDKs y parecia que el juego se colgaba.
public class LoadingScreenUI : MonoBehaviour
{
    private static readonly Color Background = new Color(0.05f, 0.06f, 0.12f, 1f);
    private static readonly Color BarBack = new Color(1f, 1f, 1f, 0.15f);
    private static readonly Color Banana = new Color(1f, 0.83f, 0.29f, 1f);

    private TextMeshProUGUI statusText;
    private RectTransform barFill;
    private readonly Image[] dots = new Image[3];

    private string status = "Cargando";
    private float shownProgress;
    private float targetProgress;

    // por las dudas: si algo sale mal, la pantalla se saca sola en vez de
    // quedar tapando el juego para siempre
    private const float MaxLifetime = 30f;
    private float bornAt;

    public static LoadingScreenUI Show(Sprite background = null)
    {
        var canvas = SimpleUI.CreateOverlayCanvas("LoadingScreen", 900);
        DontDestroyOnLoad(canvas.gameObject);

        var screen = canvas.gameObject.AddComponent<LoadingScreenUI>();
        screen.Build(background);
        return screen;
    }

    public void SetStatus(string text)
    {
        status = text;
    }

    // el progreso solo avanza: si una etapa reporta menos que la anterior no
    // queremos que la barra retroceda
    public void SetProgress(float value)
    {
        targetProgress = Mathf.Clamp01(Mathf.Max(targetProgress, value));
    }

    public void Hide()
    {
        if (this != null) Destroy(gameObject);
    }

    // La carga de escena la maneja la pantalla y no quien la creo: al ser una
    // carga Single, la escena Init (y su InitFlow) se destruyen apenas termina,
    // y con ellos moria la corutina antes de poder ocultar esto. Este objeto
    // tiene DontDestroyOnLoad, asi que su corutina si sobrevive.
    public void LoadSceneAndHide(string sceneName)
    {
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        SetStatus("Cargando el menu...");

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError($"[LoadingScreen] No se pudo cargar '{sceneName}'. " +
                           "Revisa que este en Build Settings.");
            Hide();
            yield break;
        }

        while (!op.isDone)
        {
            // progress llega hasta 0.9 y ahi activa la escena
            SetProgress(0.5f + Mathf.Clamp01(op.progress / 0.9f) * 0.5f);
            yield return null;
        }

        SetProgress(1f);

        // un frame para que el menu alcance a dibujar y no se vea un parpadeo
        yield return null;
        Hide();
    }

    private void Build(Sprite background)
    {
        bornAt = Time.realtimeSinceStartup;

        var fill = NewImage(transform, "Fondo", Background);
        Stretch(fill.rectTransform);

        if (background != null)
        {
            var art = NewImage(transform, "Banner", Color.white);
            art.sprite = background;
            var rt = art.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            // EnvelopeParent = cubre toda la pantalla sin deformarse
            var fitter = art.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = background.rect.width / Mathf.Max(background.rect.height, 1f);

            // scrim para que el texto se lea sobre cualquier parte del arte
            var scrim = NewImage(transform, "Scrim", new Color(0f, 0f, 0f, 0.55f));
            Stretch(scrim.rectTransform);
        }

        var title = SimpleUI.CreateText(transform, "Titulo", "CYBERCHIMPS", 110f,
            new Vector2(0f, 150f), new Vector2(1500f, 170f));
        title.fontStyle = FontStyles.Bold;
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(Color.white, Color.white, Banana, Banana);

        statusText = SimpleUI.CreateText(transform, "Estado", status, 34f,
            new Vector2(0f, -110f), new Vector2(1200f, 50f));
        statusText.color = new Color(1f, 1f, 1f, 0.85f);

        BuildDots();
        BuildBar();
    }

    private void BuildDots()
    {
        for (int i = 0; i < dots.Length; i++)
        {
            var dot = NewImage(transform, $"Punto{i + 1}", Banana);
            var rt = dot.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(18f, 18f);
            rt.anchoredPosition = new Vector2(-34f + i * 34f, -30f);
            dots[i] = dot;
        }
    }

    private void BuildBar()
    {
        var back = NewImage(transform, "BarraFondo", BarBack);
        var backRt = back.rectTransform;
        backRt.anchorMin = backRt.anchorMax = new Vector2(0.5f, 0.5f);
        backRt.pivot = new Vector2(0.5f, 0.5f);
        backRt.sizeDelta = new Vector2(760f, 14f);
        backRt.anchoredPosition = new Vector2(0f, -190f);

        var fill = NewImage(back.transform, "BarraRelleno", Banana);
        barFill = fill.rectTransform;
        barFill.anchorMin = new Vector2(0f, 0f);
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.pivot = new Vector2(0f, 0.5f);
        barFill.offsetMin = Vector2.zero;
        barFill.offsetMax = Vector2.zero;
        barFill.sizeDelta = new Vector2(0f, 0f);
    }

    private void Update()
    {
        if (Time.realtimeSinceStartup - bornAt > MaxLifetime)
        {
            Debug.LogWarning("[LoadingScreen] Se supero el tiempo maximo — se cierra sola.");
            Hide();
            return;
        }

        // la barra persigue al valor real: si un SDK resuelve de golpe no salta
        shownProgress = Mathf.MoveTowards(shownProgress, targetProgress, Time.unscaledDeltaTime * 0.8f);

        if (barFill != null)
        {
            var parent = barFill.parent as RectTransform;
            float width = parent != null ? parent.rect.width : 760f;
            barFill.sizeDelta = new Vector2(width * shownProgress, 0f);
        }

        AnimateDots();

        if (statusText != null)
            statusText.text = status;
    }

    private void AnimateDots()
    {
        for (int i = 0; i < dots.Length; i++)
        {
            if (dots[i] == null) continue;

            float phase = Time.unscaledTime * 3.5f - i * 0.5f;
            float pulse = (Mathf.Sin(phase) + 1f) * 0.5f;

            dots[i].rectTransform.localScale = Vector3.one * (0.6f + pulse * 0.5f);

            Color c = Banana;
            c.a = 0.35f + pulse * 0.65f;
            dots[i].color = c;
        }
    }

    private static Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
