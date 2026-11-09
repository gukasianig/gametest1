using UnityEngine;
using UnityEngine.UI;

// Экран «Испытания» (модификаторы забега, RunModifiers) в стиле выбора оружия: слева карточки испытаний
// (что усложняет, на сколько больше деталей, переключатель), справа панель награды — множитель, полоса до ×3
// и список включённых испытаний. Клик по карточке включает/выключает испытание.
public class ChallengeScreen : MonoBehaviour
{
    private const int Columns = 2;
    private static readonly Color Warm = new Color(1f, 0.6f, 0.22f);
    private static readonly Color Muted = new Color(0.55f, 0.62f, 0.7f);
    private static readonly Color Gold = new Color(0.97f, 0.8f, 0.48f);

    private class Card
    {
        public RunModifier modifier;
        public Color accent;
        public RectTransform rect;
        public Image back, chip;
        public Outline frame;
        public Text toggle;
    }

    private Card[] cards;
    private Text currencyText, multiplierText, activeText;
    private Image rewardFill;
    private System.Action changed;

    public static ChallengeScreen Build(RectTransform root, System.Action back, System.Action changed)
    {
        ChallengeScreen screen = root.gameObject.AddComponent<ChallengeScreen>();
        screen.changed = changed;
        screen.Create(root, back);
        return screen;
    }

    // Что затрагивает испытание и его цвет.
    private static string Category(string id)
    {
        switch (id)
        {
            case "mod_fragile": return "ГЕРОЙ";
            case "mod_short": return "ВРЕМЯ";
            default: return "ВРАГИ";
        }
    }

    private static Color Tone(string id)
    {
        switch (id)
        {
            case "mod_fast": return new Color(1f, 0.78f, 0.3f);
            case "mod_thick": return new Color(0.92f, 0.36f, 0.3f);
            case "mod_fragile": return new Color(0.95f, 0.45f, 0.62f);
            case "mod_horde": return new Color(0.72f, 0.55f, 0.95f);
            case "mod_short": return GameUi.Accent;
            default: return Warm;
        }
    }

    private void Create(RectTransform root, System.Action back)
    {
        Text tag = GameUi.Readable(GameUi.Label(root, "И С П Ы Т А Н И Я", new Vector2(0.04f, 0.885f), new Vector2(0.5f, 0.925f), 15, Warm), 1f);
        tag.alignment = TextAnchor.MiddleLeft;
        Text header = GameUi.Readable(GameUi.Label(root, "Рискни ради деталей", new Vector2(0.04f, 0.8f), new Vector2(0.56f, 0.89f), 40, Color.white), 2f);
        header.alignment = TextAnchor.MiddleLeft;
        header.fontStyle = FontStyle.Bold;
        currencyText = GameUi.Readable(GameUi.Label(root, "", new Vector2(0.6f, 0.82f), new Vector2(0.96f, 0.88f), 20, Gold), 1f);
        currencyText.alignment = TextAnchor.MiddleRight;

        // Карточки испытаний сеткой 2 × N.
        int count = RunModifiers.Catalog.Length;
        cards = new Card[count];
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)Columns));
        const float left = 0.04f, right = 0.56f, top = 0.785f, bottom = 0.155f, gap = 0.014f;
        float w = (right - left - gap * (Columns - 1)) / Columns;
        float h = (top - bottom - gap * 1.6f * (rows - 1)) / rows;
        for (int i = 0; i < count; i++)
        {
            int col = i % Columns, row = i / Columns;
            float x = left + col * (w + gap), y = top - row * (h + gap * 1.6f);
            cards[i] = CreateCard(root, RunModifiers.Catalog[i], new Vector2(x, y - h), new Vector2(x + w, y));
        }

        // Панель награды.
        RectTransform panel = GameUi.Box(root, "Challenge Reward", new Vector2(0.59f, 0.155f), new Vector2(0.96f, 0.785f), new Color(0.03f, 0.045f, 0.06f, 0.92f));
        UiArtwork.Round(panel.GetComponent<Image>());
        Text rewardTag = GameUi.Label(panel, "НАГРАДА ЗА ЗАБЕГ", new Vector2(0.07f, 0.9f), new Vector2(0.93f, 0.97f), 15, Warm);
        rewardTag.alignment = TextAnchor.MiddleLeft;
        multiplierText = GameUi.Readable(GameUi.Label(panel, "", new Vector2(0.07f, 0.7f), new Vector2(0.93f, 0.9f), 72, Gold), 2f);
        multiplierText.alignment = TextAnchor.MiddleLeft;
        multiplierText.fontStyle = FontStyle.Bold;
        Text unit = GameUi.Label(panel, "деталей за каждый забег", new Vector2(0.07f, 0.64f), new Vector2(0.93f, 0.7f), 16, Muted);
        unit.alignment = TextAnchor.MiddleLeft;
        RectTransform bar = GameUi.Box(panel, "Reward Bar", new Vector2(0.07f, 0.585f), new Vector2(0.93f, 0.61f), new Color(0.08f, 0.1f, 0.13f, 1f));
        rewardFill = GameUi.Box(bar, "Fill", Vector2.zero, Vector2.one, Warm).GetComponent<Image>();
        Text low = GameUi.Label(panel, "×1", new Vector2(0.07f, 0.53f), new Vector2(0.4f, 0.58f), 14, Muted);
        low.alignment = TextAnchor.MiddleLeft;
        Text high = GameUi.Label(panel, "предел ×" + RunModifiers.MaxRewardMultiplier.ToString("0.#"), new Vector2(0.5f, 0.53f), new Vector2(0.93f, 0.58f), 14, Muted);
        high.alignment = TextAnchor.MiddleRight;
        GameUi.Box(panel, "Divider", new Vector2(0.07f, 0.505f), new Vector2(0.93f, 0.508f), new Color(1f, 1f, 1f, 0.08f));
        Text activeTag = GameUi.Label(panel, "ВКЛЮЧЕНО", new Vector2(0.07f, 0.43f), new Vector2(0.93f, 0.49f), 15, Warm);
        activeTag.alignment = TextAnchor.MiddleLeft;
        activeText = GameUi.Label(panel, "", new Vector2(0.07f, 0.16f), new Vector2(0.93f, 0.43f), 18, new Color(0.88f, 0.91f, 0.94f));
        activeText.alignment = TextAnchor.UpperLeft;
        activeText.horizontalOverflow = HorizontalWrapMode.Wrap;
        activeText.resizeTextForBestFit = true;
        activeText.resizeTextMinSize = 12;
        activeText.supportRichText = true;
        Text note = GameUi.Label(panel, "Испытания сохраняются до следующего забега.", new Vector2(0.07f, 0.04f), new Vector2(0.93f, 0.13f), 14, Muted);
        note.alignment = TextAnchor.MiddleLeft;
        note.horizontalOverflow = HorizontalWrapMode.Wrap;

        Button backButton = GameUi.Button(root, "Назад", new Vector2(0.04f, 0.055f), new Vector2(0.2f, 0.125f), () => { if (back != null) back(); });
        UiArtwork.Round(backButton.GetComponent<Image>());
        Button clear = GameUi.Button(root, "Сбросить", new Vector2(0.215f, 0.055f), new Vector2(0.375f, 0.125f), () =>
        {
            RunModifiers.Clear();
            Refresh();
            if (changed != null) changed();
        });
        clear.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.17f, 1f);
        UiArtwork.Round(clear.GetComponent<Image>());
        Text hint = GameUi.Label(root, "Клик по карточке — включить или выключить", new Vector2(0.39f, 0.055f), new Vector2(0.58f, 0.125f), 14, Muted);
        hint.alignment = TextAnchor.MiddleLeft;
        hint.horizontalOverflow = HorizontalWrapMode.Wrap;
        Refresh();
    }

    // Карточка: корень «Row <id>» (без Button), внутри во всю карточку прозрачная кнопка-переключатель
    // с подписью «Включить/Включено» в плашке справа внизу (так её находит MetaProgressChecks).
    private Card CreateCard(RectTransform root, RunModifier modifier, Vector2 min, Vector2 max)
    {
        Card card = new Card { modifier = modifier, accent = Tone(modifier.id) };
        card.rect = GameUi.Box(root, "Row " + modifier.id, min, max, new Color(0.04f, 0.06f, 0.08f, 0.9f));
        card.back = card.rect.GetComponent<Image>();
        UiArtwork.Round(card.back);
        card.frame = card.rect.gameObject.AddComponent<Outline>();
        card.frame.effectDistance = new Vector2(2.5f, -2.5f);

        string id = modifier.id;
        Button button = GameUi.Button(card.rect, "", Vector2.zero, Vector2.one, () => Toggle(id));
        Image hit = button.GetComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        button.targetGraphic = card.back;
        card.toggle = button.GetComponentInChildren<Text>();
        RectTransform chip = GameUi.Box(button.transform, "Switch", new Vector2(0.6f, 0.08f), new Vector2(0.95f, 0.27f), Color.white);
        chip.SetAsFirstSibling();
        card.chip = chip.GetComponent<Image>();
        UiArtwork.Round(card.chip);
        card.toggle.rectTransform.anchorMin = new Vector2(0.6f, 0.08f);
        card.toggle.rectTransform.anchorMax = new Vector2(0.95f, 0.27f);
        card.toggle.rectTransform.offsetMin = card.toggle.rectTransform.offsetMax = Vector2.zero;
        card.toggle.fontSize = 16;
        card.toggle.resizeTextMaxSize = 16;
        card.toggle.fontStyle = FontStyle.Bold;

        GameUi.Box(card.rect, "Accent", new Vector2(0.05f, 0.93f), new Vector2(0.95f, 0.955f), new Color(card.accent.r, card.accent.g, card.accent.b, 0.75f));
        Text category = GameUi.Label(card.rect, Category(id), new Vector2(0.05f, 0.76f), new Vector2(0.6f, 0.9f), 14, card.accent);
        category.alignment = TextAnchor.MiddleLeft;
        Text title = GameUi.Readable(GameUi.Label(card.rect, modifier.title, new Vector2(0.05f, 0.56f), new Vector2(0.95f, 0.78f), 24, Color.white), 1f);
        title.alignment = TextAnchor.MiddleLeft;
        title.fontStyle = FontStyle.Bold;
        Text about = GameUi.Label(card.rect, modifier.description, new Vector2(0.05f, 0.3f), new Vector2(0.95f, 0.56f), 16, new Color(0.75f, 0.81f, 0.87f));
        about.alignment = TextAnchor.UpperLeft;
        about.horizontalOverflow = HorizontalWrapMode.Wrap;
        Text reward = GameUi.Label(card.rect, "+" + Mathf.RoundToInt(modifier.reward * 100f) + "% деталей", new Vector2(0.05f, 0.08f), new Vector2(0.58f, 0.27f), 17, Gold);
        reward.alignment = TextAnchor.MiddleLeft;
        reward.fontStyle = FontStyle.Bold;
        return card;
    }

    private void Toggle(string id)
    {
        RunModifiers.Toggle(id);
        Refresh();
        if (changed != null) changed();
    }

    public void Refresh()
    {
        if (cards == null) return;
        currencyText.text = MetaProgress.CurrencyName + ":  " + MetaProgress.Currency;
        string list = "";
        foreach (Card card in cards)
        {
            bool on = RunModifiers.IsActive(card.modifier.id);
            Color a = card.accent;
            card.toggle.text = on ? "Включено" : "Включить";
            card.toggle.color = on ? Color.white : Muted;
            card.chip.color = on ? new Color(a.r * 0.75f, a.g * 0.75f, a.b * 0.75f, 1f) : new Color(0.1f, 0.12f, 0.15f, 1f);
            card.frame.effectColor = on ? a : new Color(0f, 0f, 0f, 0f);
            card.back.color = on ? new Color(0.07f + a.r * 0.1f, 0.08f + a.g * 0.08f, 0.1f + a.b * 0.08f, 0.95f) : new Color(0.04f, 0.06f, 0.08f, 0.9f);
            card.rect.localScale = on ? Vector3.one * 1.03f : Vector3.one;
            if (on) list += "<color=#" + ColorUtility.ToHtmlStringRGB(a) + ">•</color>  " + card.modifier.title +
                "   <color=#F7CC7A>+" + Mathf.RoundToInt(card.modifier.reward * 100f) + "%</color>\n";
        }
        float multiplier = RunModifiers.RewardMultiplier;
        multiplierText.text = "×" + multiplier.ToString("0.##");
        rewardFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((multiplier - 1f) / (RunModifiers.MaxRewardMultiplier - 1f)), 1f);
        activeText.text = list.Length > 0 ? list.TrimEnd('\n') : "<color=#8C9EB2>Ничего — обычный забег.\nВключи испытание слева, чтобы получать больше деталей.</color>";
    }
}
