using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Every Health registers itself: no per-prefab UI setup is required.
// Runs after the camera's LateUpdate so bars are never placed with last frame's camera position.
[DefaultExecutionOrder(1000)]
public class EnemyHealthDisplay : MonoBehaviour
{
    private class HealthBar
    {
        public RectTransform root;
        public RectTransform fill;
        public Image image;
        public Text label;
        public float height;
    }

    private class DamageNumber
    {
        public RectTransform root;
        public Text label;
        public Vector3 position;
        public float age;
        public float drift;
    }

    private static EnemyHealthDisplay instance;
    private readonly Dictionary<Health, HealthBar> bars = new Dictionary<Health, HealthBar>();
    private readonly List<DamageNumber> numbers = new List<DamageNumber>();
    private RectTransform canvasRect;
    private Font font;
    private Camera viewCamera;

    public static EnemyHealthDisplay GetOrCreate()
    {
        if (instance == null)
            instance = new GameObject("Enemy Health HUD").AddComponent<EnemyHealthDisplay>();
        return instance;
    }

    void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        canvasRect = canvas.GetComponent<RectTransform>();
        // No GraphicRaycaster: enemy HUD never intercepts menu/game input.
    }

    public void Register(Health health)
    {
        if (bars.ContainsKey(health)) return;
        RectTransform root = Rect("Enemy HP", canvasRect, new Vector2(56, 19)); // было 82×28 и шрифт 14 — меньше по просьбе владельца
        Image background = root.gameObject.AddComponent<Image>();
        background.color = new Color(0.03f, 0.04f, 0.06f, 0.95f);
        background.raycastTarget = false;
        RectTransform fill = Rect("Fill", root, Vector2.zero);
        fill.anchorMin = new Vector2(0, 0);
        fill.anchorMax = new Vector2(1, 0.3f);
        fill.offsetMin = new Vector2(1.5f, 1.5f);
        fill.offsetMax = new Vector2(-2, -1);
        Image image = fill.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        Text label = TextLabel(root, "HP", 10);
        label.horizontalOverflow = HorizontalWrapMode.Overflow; // «1200 / 1200» не переносится на вторую строку
        label.rectTransform.anchoredPosition = new Vector2(0, 3);
        Collider body = health.GetComponentInChildren<Collider>();
        float height = body != null ? body.bounds.max.y - health.transform.position.y + 0.45f : 1.5f;
        bars.Add(health, new HealthBar { root = root, fill = fill, image = image, label = label, height = height });
        Refresh(health);
        // A new rect sits at the canvas centre (where the hero is) until the next LateUpdate: hide/place it now.
        Place(root, health.transform.position + Vector3.up * height, Vector2.zero);
    }

    public void Refresh(Health health)
    {
        if (!bars.TryGetValue(health, out HealthBar bar)) return;
        float fraction = Mathf.Clamp01((float)health.CurrentHealth / Mathf.Max(1, health.maxHealth));
        bar.fill.anchorMax = new Vector2(fraction, 0.3f);
        bar.fill.offsetMax = new Vector2(1.5f - 3f * fraction, -1);
        bar.image.enabled = fraction > 0;
        bar.image.color = Color.Lerp(new Color(1f, 0.25f, 0.2f), new Color(0.3f, 0.9f, 0.55f), fraction);
        bar.label.text = health.CurrentHealth + " / " + health.maxHealth;
    }

    public void Unregister(Health health)
    {
        if (!bars.TryGetValue(health, out HealthBar bar)) return;
        Destroy(bar.root.gameObject);
        bars.Remove(health);
    }

    public void ShowDamage(Health health, int amount, bool crit = false)
    {
        if (amount <= 0 || !GameSettings.DamageNumbers) return;
        float height = bars.TryGetValue(health, out HealthBar bar) ? bar.height : 1.5f;
        RectTransform root = Rect("Damage " + amount, canvasRect, new Vector2(100, 40));
        Text label = TextLabel(root, "-" + amount + (crit ? "!" : ""), crit ? 32 : 24);
        label.color = crit ? new Color(1f, 0.35f, 0.2f) : new Color(1f, 0.8f, 0.3f);
        label.fontStyle = FontStyle.Bold;
        // Not parented to the enemy: a killing hit remains visible after its death.
        numbers.Add(new DamageNumber { root = root, label = label,
            position = health.transform.position + Vector3.up * height,
            drift = Random.Range(-20f, 20f) });
        Place(root, numbers[numbers.Count - 1].position, new Vector2(0f, 25f));
    }

    void LateUpdate()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        foreach (KeyValuePair<Health, HealthBar> item in bars)
        {
            if (item.Key == null) continue;
            Place(item.Value.root, item.Key.transform.position + Vector3.up * item.Value.height, Vector2.zero);
        }
        for (int i = numbers.Count - 1; i >= 0; i--)
        {
            DamageNumber number = numbers[i];
            number.age += Time.unscaledDeltaTime;
            if (number.age >= 0.9f)
            {
                Destroy(number.root.gameObject);
                numbers.RemoveAt(i);
                continue;
            }
            Place(number.root, number.position, new Vector2(number.drift * number.age, 25 + 52 * number.age));
            Color color = number.label.color;
            color.a = 1 - Mathf.Clamp01((number.age - 0.25f) / 0.65f);
            number.label.color = color;
        }
    }

    private void Place(RectTransform rect, Vector3 worldPosition, Vector2 offset)
    {
        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera == null) { rect.gameObject.SetActive(false); return; }
        Vector3 screen = viewCamera.WorldToScreenPoint(worldPosition);
        bool visible = screen.z > 0 && screen.x >= 0 && screen.x <= Screen.width && screen.y >= 0 && screen.y <= Screen.height;
        rect.gameObject.SetActive(visible);
        if (!visible) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 point);
        rect.anchoredPosition = point + offset;
    }

    private RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        return rect;
    }

    private Text TextLabel(RectTransform parent, string value, int size)
    {
        RectTransform rect = Rect("Label", parent, parent.sizeDelta);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = value;
        text.raycastTarget = false;
        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.8f);
        outline.effectDistance = new Vector2(1, -1);
        return text;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
