using UnityEngine;
using UnityEngine.UI;

// Полоса HP босса (19.11.2026, просьба владельца «поменять HUD хп босса»): тёмная плашка под таймером с именем
// цвета босса, цифрами HP, отметкой второй фазы на 50%, светлым «следом» потерянного HP, вспышкой при уроне
// и надписью «ЯРОСТЬ» во второй фазе. Появляется раскрытием и заполнением. Всё на unscaled time.
public class BossHealthBar : MonoBehaviour
{
    private RectTransform root, fill, trail, shine;
    private Image fillImage, trailImage, frameImage, flash;
    private Outline border;
    private Text title, numbers, enraged;
    private CanvasGroup group;
    private float shown = 1f, trailValue = 1f, target = 1f, appear, hitFlash, shake;
    private int lastHealth = -1;
    private bool visible, isEnraged;
    private Color accent = Color.white;
    private string current = "";

    public bool Visible => visible;
    public float Shown => shown;

    public static BossHealthBar Create(Transform parent)
    {
        RectTransform plate = GameUi.Box(parent, "Boss HP", new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Color(.02f, .035f, .05f, .94f));
        plate.pivot = new Vector2(.5f, 1f);
        plate.sizeDelta = new Vector2(640f, 58f);
        plate.anchoredPosition = new Vector2(0f, -110f);
        BossHealthBar bar = plate.gameObject.AddComponent<BossHealthBar>();
        bar.Build(plate);
        plate.gameObject.SetActive(false);
        return bar;
    }

    private void Build(RectTransform plate)
    {
        root = plate;
        frameImage = plate.GetComponent<Image>();
        UiArtwork.Round(frameImage);
        border = plate.gameObject.AddComponent<Outline>();
        border.effectDistance = new Vector2(2f, -2f);
        group = plate.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        title = GameUi.Readable(GameUi.Label(plate, "", new Vector2(.02f, .52f), new Vector2(.7f, 1f), 17), 1f);
        title.alignment = TextAnchor.MiddleLeft;
        title.fontStyle = FontStyle.Bold;
        enraged = GameUi.Readable(GameUi.Label(plate, GameText.Source("hud.boss.enraged"), new Vector2(.7f, .52f), new Vector2(.98f, 1f), 15), 1f);
        enraged.alignment = TextAnchor.MiddleRight;
        enraged.fontStyle = FontStyle.Bold;
        RectTransform track = GameUi.Box(plate, "Track", new Vector2(.02f, .12f), new Vector2(.98f, .5f), new Color(.09f, .11f, .13f));
        track.gameObject.AddComponent<RectMask2D>();
        trail = GameUi.Box(track, "Trail", Vector2.zero, Vector2.one, new Color(1f, .92f, .8f, .85f));
        trailImage = trail.GetComponent<Image>();
        fill = GameUi.Box(track, "Fill", Vector2.zero, Vector2.one, Color.red);
        fillImage = fill.GetComponent<Image>();
        shine = GameUi.Box(fill, "Shine", new Vector2(0f, .55f), Vector2.one, new Color(1f, 1f, 1f, .22f));
        GameUi.Box(fill, "Shade", Vector2.zero, new Vector2(1f, .25f), new Color(0f, 0f, 0f, .25f));
        // Отметка второй фазы (50%).
        RectTransform notch = GameUi.Box(track, "Phase", new Vector2(.5f, 0f), new Vector2(.5f, 1f), new Color(.02f, .03f, .04f, .9f));
        notch.sizeDelta = new Vector2(3f, 0f);
        flash = GameUi.Box(track, "Hit flash", Vector2.zero, Vector2.one, Color.clear).GetComponent<Image>();
        numbers = GameUi.Readable(GameUi.Label(track, "", Vector2.zero, Vector2.one, 13), 1f);
        numbers.rectTransform.offsetMin = numbers.rectTransform.offsetMax = Vector2.zero;
        numbers.fontStyle = FontStyle.Bold;
    }

    // Показать/обновить: имя, цвет, HP, вторая фаза.
    public void Set(string name, Color color, int health, int max, bool phaseTwo)
    {
        if (!visible || name != current)
        {
            visible = true;
            current = name;
            root.gameObject.SetActive(true);
            appear = 0f;
            shown = trailValue = 0f;
            lastHealth = -1;
        }
        accent = color;
        title.text = name;
        target = Mathf.Clamp01((float)health / Mathf.Max(1, max));
        numbers.text = Group(health) + " / " + Group(max);
        if (lastHealth >= 0 && health < lastHealth)
        {
            hitFlash = 1f;
            if ((lastHealth - health) > max * 0.015f) shake = 1f;
        }
        lastHealth = health;
        if (phaseTwo && !isEnraged) { shake = 1f; hitFlash = 1f; }
        isEnraged = phaseTwo;
    }

    public void Hide()
    {
        if (!visible) return;
        visible = false;
        current = "";
        isEnraged = false;
        root.gameObject.SetActive(false);
    }

    private static string Group(int value)
    {
        return value.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' ');
    }

    private void Update()
    {
        if (!visible) return;
        float dt = Time.unscaledDeltaTime;
        appear += dt;
        // Раскрытие плашки по ширине, затем заполнение полосы.
        float open = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(appear / 0.3f));
        float jitter = shake > 0f ? Mathf.Sin(appear * 90f) * 4f * shake : 0f;
        root.localScale = new Vector3(Mathf.Lerp(.6f, 1f, open), 1f, 1f);
        root.anchoredPosition = new Vector2(jitter, -110f);
        group.alpha = open;
        shake = Mathf.MoveTowards(shake, 0f, dt * 4f);
        float fillSpeed = appear < 0.9f ? 1.6f : 6f;
        shown = Mathf.MoveTowards(shown, target, dt * fillSpeed * Mathf.Max(0.15f, Mathf.Abs(target - shown) * 4f));
        if (appear < 0.9f) trailValue = shown;
        else if (trailValue < shown) trailValue = shown;
        else trailValue = Mathf.MoveTowards(trailValue, shown, dt * 0.35f);
        fill.anchorMax = new Vector2(shown, 1f);
        trail.anchorMax = new Vector2(trailValue, 1f);
        fill.gameObject.SetActive(shown > 0.001f);
        trail.gameObject.SetActive(trailValue > 0.001f);
        // Цвет: босса; во второй фазе пульсирует к красному; вспышка белым при уроне.
        float pulse = isEnraged ? 0.5f + 0.5f * Mathf.Sin(appear * 7f) : 0f;
        Color body = Color.Lerp(accent, new Color(1f, .2f, .15f), isEnraged ? .35f + .25f * pulse : 0f);
        fillImage.color = Color.Lerp(body, Color.white, hitFlash * .45f);
        hitFlash = Mathf.MoveTowards(hitFlash, 0f, dt * 6f);
        flash.color = new Color(1f, 1f, 1f, hitFlash * .12f);
        title.color = Color.Lerp(accent, Color.white, .25f);
        border.effectColor = new Color(accent.r, accent.g, accent.b, isEnraged ? .55f + .4f * pulse : .55f);
        enraged.gameObject.SetActive(isEnraged);
        if (isEnraged) enraged.color = Color.Lerp(new Color(1f, .35f, .25f), Color.white, pulse * .4f);
        shine.gameObject.SetActive(shown > 0.01f);
    }
}
