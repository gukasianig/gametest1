using UnityEngine;
using UnityEngine.UI;

public class HealthBarFx : MonoBehaviour
{
    private PlayerHealth health;
    private Image fill, shine;
    private RectTransform frame;
    private Vector2 basePosition;
    private Outline border;
    private Image emblem;
    private int previousHP;
    private float hitFlash, healFlash;

    public void Initialize(PlayerHealth owner, Image healthFill)
    {
        health = owner;
        fill = healthFill;
        previousHP = health.CurrentHP;
        frame = GetComponent<RectTransform>();
        basePosition = frame.anchoredPosition;
        Image background = GetComponent<Image>();
        if (background != null)
        {
            background.color = new Color(0.06f, 0.085f, 0.09f, 0.96f);
            border = GetComponent<Outline>();
            if (border == null) border = gameObject.AddComponent<Outline>();
            border.effectDistance = new Vector2(1f, -1f);
            Shadow shadow = null;
            foreach (Shadow candidate in GetComponents<Shadow>())
                if (candidate.GetType() == typeof(Shadow)) { shadow = candidate; break; }
            if (shadow == null) shadow = gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(0f, -2.5f);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        }
        shine = GameUi.Box(transform, "Health Shine", new Vector2(0f, 0.6f), Vector2.one,
            new Color(1f, 1f, 1f, 0.14f)).GetComponent<Image>();
        shine.rectTransform.offsetMin = new Vector2(5f, 0f);
        shine.rectTransform.offsetMax = new Vector2(-5f, -4f);
        shine.sprite = fill.sprite;
        shine.type = Image.Type.Filled;
        shine.fillMethod = Image.FillMethod.Horizontal;
        shine.fillOrigin = 0;
        emblem = UiArtwork.Picture(transform, UiArtwork.Heart(false), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        emblem.name = "Health Heart";
        emblem.rectTransform.sizeDelta = new Vector2(34f, 34f);
        emblem.rectTransform.anchoredPosition = new Vector2(-25f, 0f);
    }

    private void LateUpdate()
    {
        if (health == null || fill == null) return;
        if (health.CurrentHP < previousHP) hitFlash = 1f;
        else if (health.CurrentHP > previousHP) healFlash = 1f;
        previousHP = health.CurrentHP;
        hitFlash = Mathf.MoveTowards(hitFlash, 0f, Time.unscaledDeltaTime * 3.5f);
        healFlash = Mathf.MoveTowards(healFlash, 0f, Time.unscaledDeltaTime * 2.5f);
        float fraction = (float)health.CurrentHP / Mathf.Max(1, health.maxHP);
        float pulse = (Mathf.Sin(Time.unscaledTime * 5f) + 1f) * 0.5f;
        bool critical = fraction > 0f && fraction <= 0.25f;
        Color calm = new Color(0.3f, 0.5f, 0.4f, 0.4f);
        Color danger = new Color(1f, 0.27f, 0.2f, critical ? 0.3f + pulse * 0.35f : 0.75f);
        if (border != null) border.effectColor = Color.Lerp(critical ? danger : calm, danger, hitFlash);
        fill.color = Color.Lerp(fill.color, Color.white, Mathf.Max(hitFlash * 0.65f, healFlash * 0.3f));
        shine.fillAmount = fill.fillAmount;
        shine.color = new Color(1f, 1f, 1f, 0.12f + hitFlash * 0.15f);
        emblem.sprite = UiArtwork.Heart(critical);
        emblem.color = Color.white;
        emblem.transform.localScale = Vector3.one * (1f + healFlash * 0.1f + (critical ? pulse * 0.06f : 0f));
        frame.anchoredPosition = basePosition;
    }
}
