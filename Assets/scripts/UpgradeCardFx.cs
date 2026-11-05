using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UpgradeCardFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
{
    private Outline border;
    private Shadow shadow;
    private CanvasGroup group;
    private RectTransform rect;
    private Color tint = GameUi.Accent;
    private float age, emphasis, delay;
    private bool hovered, selected, pressed;
    public Transform icon;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        UiArtwork.Round(GetComponent<Image>());
        border = gameObject.AddComponent<Outline>();
        border.effectDistance = new Vector2(1f, -1f);
        shadow = gameObject.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(0f, -4f);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        group = gameObject.AddComponent<CanvasGroup>();
    }

    public void Present(Color color, int index)
    {
        tint = color;
        delay = index * 0.05f;
        age = -delay;
        hovered = selected = pressed = false;
        emphasis = 0f;
        if (group != null) group.alpha = 0f;
    }

    private void OnEnable() { age = -delay; }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        float intro = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.22f));
        emphasis = Mathf.Lerp(emphasis, hovered || selected ? 1f : 0f, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        group.alpha = intro;
        float size = Mathf.Lerp(0.98f, 1f, intro) + emphasis * 0.008f - (pressed ? 0.012f : 0f);
        rect.localScale = Vector3.one * size;
        border.effectColor = new Color(tint.r, tint.g, tint.b, 0.18f + emphasis * 0.4f);
        if (icon != null) icon.localScale = Vector3.one * (1f + emphasis * 0.04f);
    }

    public void OnPointerEnter(PointerEventData data) { hovered = true; }
    public void OnPointerExit(PointerEventData data) { hovered = pressed = false; }
    public void OnSelect(BaseEventData data) { selected = true; }
    public void OnDeselect(BaseEventData data) { selected = pressed = false; }
    public void OnPointerDown(PointerEventData data) { pressed = true; }
    public void OnPointerUp(PointerEventData data) { pressed = false; }
}
