using UnityEngine;
using UnityEngine.UI;

public static class GameUi
{
    public static readonly Color Background = new Color(0.025f, 0.045f, 0.065f, 0.98f);
    public static readonly Color Accent = new Color(0.25f, 0.85f, 0.72f);

    public static Canvas Canvas(string name, int order)
    {
        Canvas canvas = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    public static RectTransform Box(Transform parent, string name, Vector2 min, Vector2 max, Color color,
        bool blocks = false)
    {
        Image image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.rectTransform.anchorMin = min;
        image.rectTransform.anchorMax = max;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        image.color = color;
        image.raycastTarget = blocks;
        return image.rectTransform;
    }

    public static Text Label(Transform parent, string value, Vector2 min, Vector2 max, int size,
        Color? color = null)
    {
        Text label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(parent, false);
        label.rectTransform.anchorMin = min;
        label.rectTransform.anchorMax = max;
        label.rectTransform.offsetMin = new Vector2(10, 4);
        label.rectTransform.offsetMax = new Vector2(-10, -4);
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = value;
        label.fontSize = size;
        label.color = color ?? Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 12;
        label.resizeTextMaxSize = size;
        label.raycastTarget = false;
        return label;
    }

    public static Button Button(Transform parent, string value, Vector2 min, Vector2 max,
        UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = Box(parent, value, min, max, new Color(0.08f, 0.27f, 0.31f), true);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(action);
        Label(rect, value, Vector2.zero, Vector2.one, 25);
        return button;
    }
}
