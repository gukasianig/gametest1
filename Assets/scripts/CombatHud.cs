using UnityEngine;
using UnityEngine.UI;

// Панель героя слева внизу экрана боя: ХП (полоса, след урона, цифры, вспышка и пульс при малом запасе),
// броня (процент снимаемого урона, вспышка при поглощении удара) и готовность рывка.
// Старая полоса ХП из сцены скрывается, но продолжает работать (её читают проверки).
[RequireComponent(typeof(PlayerHealth))]
public class CombatHud : MonoBehaviour
{
    public static CombatHud Instance { get; private set; }

    private static readonly Color HpHigh = new Color(0.24f, 0.86f, 0.36f);
    private static readonly Color HpLow = new Color(0.95f, 0.22f, 0.2f);
    private static readonly Color ArmorColor = new Color(0.45f, 0.72f, 1f);
    private static readonly Color Muted = new Color(0.6f, 0.66f, 0.72f);

    private PlayerHealth health;
    private PlayMovement movement;
    private Canvas canvas;
    private RectTransform panel, hpFrame;
    private Image hpFill, hpTrail, hpFlash, armorBadge, dashFill;
    private Text hpText, armorText, dashText;
    private Graphic armorIcon;
    private Color armorIdle = new Color(0.07f, 0.12f, 0.2f, 1f), armorHit = new Color(0.25f, 0.45f, 0.8f, 1f);
    private Color armorTint = new Color(0.45f, 0.72f, 1f);
    private bool art;
    private float shownHp = 1f, trailHp = 1f, trailHold, hitFlash, armorFlash, healFlash;
    private int previousHp = -1;
    private Vector2 panelBase;

    public string HealthText => hpText != null ? hpText.text : "";
    public string ArmorText => armorText != null ? armorText.text : "";

    private void Awake()
    {
        Instance = this;
        health = GetComponent<PlayerHealth>();
        movement = GetComponent<PlayMovement>();
        PlayMovement.DashInCombatHud = true;
        health.Damaged += OnDamaged;
    }

    private void Start()
    {
        if (!BuildArt()) Build();
        if (health.hpImage != null && health.hpImage.transform.parent != null)
            health.hpImage.transform.parent.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (health != null) health.Damaged -= OnDamaged;
        if (canvas != null) Destroy(canvas.gameObject);
        if (Instance == this) Instance = null;
    }

    private void OnDamaged(int raw, int taken)
    {
        hitFlash = 1f;
        trailHold = 0.35f;
        if (raw > taken) armorFlash = 1f;
    }

    private void Build()
    {
        // Ниже экрана улучшений (80) и плашки событий (78): карточки и баннер перекрывают панель.
        canvas = GameUi.Canvas("Combat HUD", 77);
        panel = GameUi.Box(canvas.transform, "Hero Panel", new Vector2(0.015f, 0.025f), new Vector2(0.36f, 0.19f),
            new Color(0.02f, 0.035f, 0.05f, 0.82f));
        UiArtwork.Round(panel.GetComponent<Image>());
        panelBase = panel.anchoredPosition;
        GameUi.Box(panel, "Accent", new Vector2(0.03f, 0.955f), new Vector2(0.97f, 0.975f), new Color(GameUi.Accent.r, GameUi.Accent.g, GameUi.Accent.b, 0.6f));

        // Сердце (значок из атласа улучшений) и полоса ХП.
        Image heart = GameUi.Box(panel, "Heart", new Vector2(0.03f, 0.47f), new Vector2(0.14f, 0.9f), Color.white).GetComponent<Image>();
        heart.sprite = UiArtwork.Upgrade(UpgradeEffect.MaxHealth);
        heart.preserveAspect = true;
        heart.enabled = heart.sprite != null;
        if (heart.sprite == null) AddVectorIcon(panel, UpgradeEffect.MaxHealth, new Vector2(0.03f, 0.47f), new Vector2(0.14f, 0.9f), HpLow);

        hpFrame = GameUi.Box(panel, "HP Frame", new Vector2(0.16f, 0.52f), new Vector2(0.97f, 0.86f), new Color(0.08f, 0.1f, 0.12f, 1f));
        hpTrail = GameUi.Box(hpFrame, "HP Trail", Vector2.zero, Vector2.one, new Color(1f, 0.85f, 0.4f)).GetComponent<Image>();
        hpFill = GameUi.Box(hpFrame, "HP Fill", Vector2.zero, Vector2.one, HpHigh).GetComponent<Image>();
        GameUi.Box(hpFrame, "HP Shine", new Vector2(0f, 0.55f), Vector2.one, new Color(1f, 1f, 1f, 0.12f));
        hpFlash = GameUi.Box(hpFrame, "HP Flash", Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0f)).GetComponent<Image>();
        foreach (Image bar in new[] { hpTrail, hpFill })
        {
            bar.rectTransform.offsetMin = new Vector2(3f, 3f);
            bar.rectTransform.offsetMax = new Vector2(-3f, -3f);
        }
        hpText = GameUi.Readable(GameUi.Label(hpFrame, "", Vector2.zero, Vector2.one, 22));
        hpText.fontStyle = FontStyle.Bold;

        // Броня: значок щита и процент.
        armorBadge = GameUi.Box(panel, "Armor", new Vector2(0.03f, 0.1f), new Vector2(0.47f, 0.42f), new Color(0.07f, 0.12f, 0.2f, 1f)).GetComponent<Image>();
        UiArtwork.Round(armorBadge);
        armorIcon = AddVectorIcon(armorBadge.rectTransform, UpgradeEffect.Armor, new Vector2(0.03f, 0.1f), new Vector2(0.25f, 0.9f), ArmorColor);
        armorText = GameUi.Readable(GameUi.Label(armorBadge.rectTransform, "", new Vector2(0.25f, 0f), Vector2.one, 19, ArmorColor), 1f);
        armorText.alignment = TextAnchor.MiddleLeft;

        // Рывок: полоса перезарядки с надписью.
        RectTransform dash = GameUi.Box(panel, "Dash", new Vector2(0.5f, 0.1f), new Vector2(0.97f, 0.42f), new Color(0.06f, 0.1f, 0.11f, 1f));
        UiArtwork.Round(dash.GetComponent<Image>());
        dashFill = GameUi.Box(dash, "Dash Fill", Vector2.zero, Vector2.one, new Color(GameUi.Accent.r, GameUi.Accent.g, GameUi.Accent.b, 0.35f)).GetComponent<Image>();
        dashText = GameUi.Readable(GameUi.Label(dash, "", Vector2.zero, Vector2.one, 18), 1f);
        canvas.enabled = false;
    }

    private static UpgradeIcon AddVectorIcon(RectTransform parent, UpgradeEffect effect, Vector2 min, Vector2 max, Color color)
    {
        GameObject go = new GameObject("Icon " + effect, typeof(RectTransform), typeof(UpgradeIcon));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        UpgradeIcon icon = go.GetComponent<UpgradeIcon>();
        icon.Effect = effect;
        icon.color = color;
        icon.raycastTarget = false;
        return icon;
    }

    private void LateUpdate()
    {
        if (canvas == null) return;
        GameSession session = GameSession.Instance;
        bool show = session == null || (session.State != GameRunState.Menu && !session.IsFinished);
        canvas.enabled = show;
        if (!show) return;
        float dt = Time.unscaledDeltaTime;

        int max = Mathf.Max(1, health.maxHP);
        int hp = health.CurrentHP;
        if (previousHp >= 0 && hp > previousHp) healFlash = 1f;
        previousHp = hp;
        float target = Mathf.Clamp01((float)hp / max);
        shownHp = Mathf.Lerp(shownHp, target, 1f - Mathf.Exp(-14f * dt));
        trailHold = Mathf.Max(0f, trailHold - dt);
        if (trailHold <= 0f) trailHp = Mathf.Lerp(trailHp, target, 1f - Mathf.Exp(-4f * dt));
        trailHp = Mathf.Max(trailHp, shownHp);
        SetWidth(hpFill, shownHp);
        SetWidth(hpTrail, trailHp);

        bool low = target <= 0.3f;
        Color hpColor = Color.Lerp(HpLow, HpHigh, Mathf.InverseLerp(0.15f, 0.7f, target));
        if (low) hpColor = Color.Lerp(hpColor, Color.white, 0.25f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f)));
        hpFill.color = hpColor;
        hpText.text = hp + " / " + max;

        hitFlash = Mathf.Max(0f, hitFlash - dt * 4f);
        healFlash = Mathf.Max(0f, healFlash - dt * 3f);
        hpFlash.color = hitFlash > 0f ? new Color(1f, 0.25f, 0.2f, 0.55f * hitFlash) : new Color(0.5f, 1f, 0.6f, 0.35f * healFlash);
        // Тряска панели при ударе.
        panel.anchoredPosition = panelBase + (hitFlash > 0.5f ? Random.insideUnitCircle * 4f * hitFlash : Vector2.zero);

        float armor = Mathf.Min(0.6f, CombatStats.Armor);
        armorFlash = Mathf.Max(0f, armorFlash - dt * 3f);
        armorText.text = armor > 0f ? "Броня " + Mathf.RoundToInt(armor * 100f) + "%" : "Брони нет";
        Color armorNow = armor > 0f ? armorTint : Muted;
        armorText.color = Color.Lerp(armorNow, Color.white, armorFlash);
        if (!art) armorIcon.color = armorText.color;
        armorBadge.color = Color.Lerp(armorIdle, armorHit, armorFlash);

        if (movement != null)
        {
            float left = movement.DashCooldownRemaining;
            float cooldown = Mathf.Max(1f, movement.dashCooldown - CombatStats.DashCooldownReduction);
            SetWidth(dashFill, left <= 0f ? 1f : 1f - left / cooldown);
            dashText.text = left <= 0f ? "Рывок  [Пробел]" : "Рывок  " + left.ToString("0.0") + " с";
            dashText.color = left <= 0f ? Color.white : Muted;
        }
    }

    private static void SetWidth(Image bar, float share)
    {
        if (bar.type == Image.Type.Filled) bar.fillAmount = Mathf.Clamp01(share);
        else bar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(share), 1f);
    }

    // --- Панель из Blender (tools/blender/make_hud_panel.py, вариант C «Броне-плита»): рамка с пазами,
    // сердце, щит, значок рывка и белая заливка полосы (цвет задаёт код). Нет картинок — старый вид (Build).
    private const string ArtPath = "UI/Hud/";
    private static readonly Vector2 PanelSize = new Vector2(340f, 92f);   // было 442x119: панель уменьшена

    [System.Serializable]
    private class ArtLayout { public float[] heart, shield, dash, fill; }

    private static Sprite LoadSprite(string name)
    {
        Texture2D texture = Resources.Load<Texture2D>(ArtPath + name);
        if (texture == null) return null;
        texture.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    // Картинка по раскладке [центр x, центр y, ширина, высота] в долях панели.
    private Image Place(string name, Sprite sprite, float[] box, Transform parent = null)
    {
        Image image = GameUi.Box(parent != null ? parent : panel, name, new Vector2(box[0] - box[2] / 2f, box[1] - box[3] / 2f),
            new Vector2(box[0] + box[2] / 2f, box[1] + box[3] / 2f), Color.white).GetComponent<Image>();
        image.sprite = sprite;
        return image;
    }

    private Image Bar(string name, Sprite sprite, Transform parent, Color color)
    {
        Image bar = GameUi.Box(parent, name, Vector2.zero, Vector2.one, color).GetComponent<Image>();
        bar.sprite = sprite;
        bar.type = Image.Type.Filled;
        bar.fillMethod = Image.FillMethod.Horizontal;
        bar.fillOrigin = 0;
        bar.fillAmount = 1f;
        return bar;
    }

    private bool BuildArt()
    {
        Sprite plate = LoadSprite("hud_panel"), heartArt = LoadSprite("hud_heart"), shieldArt = LoadSprite("hud_shield"),
            dashArt = LoadSprite("hud_dash"), fillArt = LoadSprite("hud_bar_fill");
        TextAsset json = Resources.Load<TextAsset>(ArtPath + "hud_layout");
        if (plate == null || heartArt == null || shieldArt == null || dashArt == null || fillArt == null || json == null) return false;
        ArtLayout layout = JsonUtility.FromJson<ArtLayout>(json.text);
        if (layout.heart == null || layout.shield == null || layout.dash == null || layout.fill == null) return false;
        art = true;

        canvas = GameUi.Canvas("Combat HUD", 77);
        // Постоянный размер (не тянется по соотношению сторон экрана), левый нижний угол.
        panel = GameUi.Box(canvas.transform, "Hero Panel", Vector2.zero, Vector2.zero, Color.white);
        panel.pivot = Vector2.zero;
        panel.sizeDelta = PanelSize;
        panel.anchoredPosition = new Vector2(16f, 14f);
        panel.GetComponent<Image>().sprite = plate;
        panelBase = panel.anchoredPosition;

        // Полоса ХП: след урона, заливка, вспышка — одна картинка, разный цвет.
        RectTransform bar = Place("HP Fill Area", null, layout.fill).rectTransform;
        bar.GetComponent<Image>().enabled = false;
        hpFrame = bar;
        hpTrail = Bar("HP Trail", fillArt, bar, new Color(1f, 0.8f, 0.35f));
        hpFill = Bar("HP Fill", fillArt, bar, HpHigh);
        hpFlash = Bar("HP Flash", fillArt, bar, new Color(1f, 1f, 1f, 0f));
        hpText = GameUi.Readable(GameUi.Label(bar, "", Vector2.zero, Vector2.one, 17));
        hpText.fontStyle = FontStyle.Bold;
        Place("Heart", heartArt, layout.heart);

        // Броня: подсветка паза при поглощении удара, щит, процент.
        armorBadge = Bar("Armor Flash", fillArt, panel, new Color(0f, 0f, 0f, 0f));
        armorBadge.rectTransform.anchorMin = new Vector2(0.035f, 0.13f);
        armorBadge.rectTransform.anchorMax = new Vector2(0.465f, 0.39f);
        armorIdle = new Color(1f, 0.7f, 0.3f, 0f);
        armorHit = new Color(1f, 0.7f, 0.3f, 0.45f);
        armorTint = new Color(0.97f, 0.8f, 0.48f);
        armorIcon = Place("Shield", shieldArt, layout.shield);
        armorText = GameUi.Readable(GameUi.Label(panel, "", new Vector2(0.13f, 0.1f), new Vector2(0.47f, 0.42f), 15, armorTint), 1f);
        armorText.alignment = TextAnchor.MiddleLeft;

        // Рывок: заполнение паза по перезарядке, значок, надпись.
        dashFill = Bar("Dash Fill", fillArt, panel, new Color(1f, 0.55f, 0.12f, 0.3f));
        dashFill.rectTransform.anchorMin = new Vector2(0.505f, 0.13f);
        dashFill.rectTransform.anchorMax = new Vector2(0.965f, 0.39f);
        Place("Dash Icon", dashArt, layout.dash);
        dashText = GameUi.Readable(GameUi.Label(panel, "", new Vector2(0.59f, 0.1f), new Vector2(0.97f, 0.42f), 14), 1f);
        canvas.enabled = false;
        return true;
    }
}
