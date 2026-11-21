using UnityEngine;

// Появление элемента интерфейса (19.11.2026): задержка, сдвиг и прозрачность, по желанию — «впрыгивание» значка
// и рост полосы (fill: anchorMax.x от 0 до своего значения). Unscaled time — экраны итогов стоят на паузе.
public class UiReveal : MonoBehaviour
{
    private RectTransform rect;
    private CanvasGroup group;
    private Vector2 basePosition, offset;
    private float delay, age, fillTarget;
    private Transform pop;
    private RectTransform fill;

    public static UiReveal Add(RectTransform target, float delay, Vector2 offset, Transform pop = null, RectTransform fill = null)
    {
        if (target == null) return null;
        UiReveal reveal = target.gameObject.AddComponent<UiReveal>();
        reveal.rect = target;
        reveal.delay = Mathf.Max(0f, delay);
        reveal.offset = offset;
        reveal.basePosition = target.anchoredPosition;
        reveal.pop = pop;
        reveal.fill = fill;
        if (fill != null) { reveal.fillTarget = fill.anchorMax.x; fill.anchorMax = new Vector2(fill.anchorMin.x, fill.anchorMax.y); }
        reveal.group = target.GetComponent<CanvasGroup>();
        if (reveal.group == null) reveal.group = target.gameObject.AddComponent<CanvasGroup>();
        reveal.group.alpha = 0f;
        reveal.Apply();
        return reveal;
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        Apply();
        if (age > delay + 1.2f) { Finish(); Destroy(this); }
    }

    private void Apply()
    {
        float t = Mathf.Clamp01((age - delay) / 0.32f);
        float ease = 1f - Mathf.Pow(1f - t, 3f);
        group.alpha = t;
        rect.anchoredPosition = basePosition + offset * (1f - ease);
        if (pop != null)
        {
            float p = Mathf.Clamp01((age - delay - 0.08f) / 0.35f);
            float spring = 1f + 2.2f * Mathf.Pow(p - 1f, 3f) + 1.2f * Mathf.Pow(p - 1f, 2f);
            pop.localScale = Vector3.one * Mathf.Max(0.01f, spring);
        }
        if (fill != null)
        {
            float f = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - delay - 0.15f) / 0.6f));
            fill.anchorMax = new Vector2(Mathf.Lerp(fill.anchorMin.x, fillTarget, f), fill.anchorMax.y);
        }
    }

    private void Finish()
    {
        group.alpha = 1f;
        rect.anchoredPosition = basePosition;
        if (pop != null) pop.localScale = Vector3.one;
        if (fill != null) fill.anchorMax = new Vector2(fillTarget, fill.anchorMax.y);
    }
}
