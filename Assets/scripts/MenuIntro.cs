using System.Collections.Generic;
using UnityEngine;

// Появление главного экрана меню: элементы по очереди выезжают слева и проявляются.
// Первый показ — полностью (~1,2 с), при возврате с других экранов — быстрее.
public class MenuIntro : MonoBehaviour
{
    private class Item
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Vector2 basePosition;
        public float delay, distance;
        public bool grow;
    }

    private readonly List<Item> items = new List<Item>();
    private float start, speed = 1f;
    private bool shown, finished;
    private const float Duration = 0.5f;

    // distance — на сколько пикселей левее начинает элемент; grow — вместо сдвига растёт по ширине слева.
    public void Add(Component target, float delay, float distance = 50f, bool grow = false)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect == null) return;
        CanvasGroup group = rect.GetComponent<CanvasGroup>();
        if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
        if (grow) rect.pivot = new Vector2(0f, rect.pivot.y);
        Item item = new Item { rect = rect, group = group, basePosition = rect.anchoredPosition, delay = delay, distance = distance, grow = grow };
        items.Add(item);
        finished = false;
        Apply(item, 0f);
    }

    private void OnEnable()
    {
        start = Time.unscaledTime;
        speed = shown ? 2.4f : 1f;
        shown = true;
        finished = false;
        foreach (Item item in items) Apply(item, 0f);
    }

    private void Update()
    {
        if (finished) return;
        float elapsed = (Time.unscaledTime - start) * speed;
        bool done = true;
        foreach (Item item in items)
        {
            float p = Mathf.Clamp01((elapsed - item.delay) / Duration);
            if (p < 1f) done = false;
            Apply(item, p);
        }
        finished = done;
    }

    private static void Apply(Item item, float p)
    {
        float eased = 1f - Mathf.Pow(1f - p, 3f);
        item.group.alpha = Mathf.Clamp01(p * 1.6f);
        if (item.grow) item.rect.localScale = new Vector3(eased, 1f, 1f);
        else item.rect.anchoredPosition = item.basePosition + new Vector2(-item.distance * (1f - eased), 0f);
    }
}
