using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Helpers para construir UI básica en runtime (paneles generados por código).
/// Usado por LevelCompleteUI y PauseMenuUI como UI funcional por defecto —
/// cuando se diseñe la UI definitiva en el editor, estos paneles se pueden
/// reemplazar asignando las referencias serializadas correspondientes.
/// </summary>
public static class SimpleUI
{
    // Paleta simple acorde al estilo hyper-casual
    public static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.16f, 0.92f);
    public static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.55f);
    public static readonly Color GreenButton = new Color(0.22f, 0.68f, 0.32f, 1f);
    public static readonly Color BlueButton = new Color(0.20f, 0.45f, 0.85f, 1f);
    public static readonly Color RedButton = new Color(0.80f, 0.25f, 0.25f, 1f);
    public static readonly Color GreyButton = new Color(0.35f, 0.35f, 0.40f, 1f);

    /// <summary>
    /// Canvas overlay propio (ScaleWithScreenSize 1920x1080) para no depender
    /// del canvas de controles móviles. sortOrder alto = dibuja encima de todo.
    /// </summary>
    public static Canvas CreateOverlayCanvas(string name, int sortOrder)
    {
        EnsureEventSystem();

        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>Imagen full-screen semitransparente que bloquea el input detrás del panel.</summary>
    public static Image CreateOverlay(Transform parent)
    {
        var img = CreateImage(parent, "Overlay", OverlayColor);
        Stretch(img.rectTransform);
        return img;
    }

    public static Image CreatePanel(Transform parent, Vector2 size)
    {
        var img = CreateImage(parent, "Panel", PanelColor);
        Center(img.rectTransform, Vector2.zero, size);
        return img;
    }

    public static TextMeshProUGUI CreateText(
        Transform parent, string name, string text, float fontSize,
        Vector2 anchoredPos, Vector2 size,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        Center(tmp.rectTransform, anchoredPos, size);
        return tmp;
    }

    public static Button CreateButton(
        Transform parent, string name, string label,
        Vector2 anchoredPos, Vector2 size, Color color, UnityAction onClick)
    {
        var img = CreateImage(parent, name, color);
        Center(img.rectTransform, anchoredPos, size);

        var button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        if (onClick != null) button.onClick.AddListener(onClick);

        var colors = button.colors;
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        button.colors = colors;

        CreateText(img.transform, "Label", label, size.y * 0.42f, Vector2.zero, size);
        return button;
    }

    // ── Internos ──────────────────────────────────────────────────────────

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static void Center(RectTransform rt, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Los niveles ya traen EventSystem (por los controles móviles), pero por
    /// las dudas creamos uno con el input module del Input System nuevo.
    /// </summary>
    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }
}
