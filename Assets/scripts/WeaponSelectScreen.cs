using UnityEngine;
using UnityEngine.UI;

// Экран выбора оружия: все оружия плитками в сетке слева (без страниц), справа подробности выбранного —
// картинка, роль, описание, полоски «урон / темп / дальность» и одна кнопка: «В бой», «Открыть за N»
// или «Нужно N». Клик по плитке выбирает, повторный клик по выбранной — то же, что кнопка.
public class WeaponSelectScreen : MonoBehaviour
{
    private const int Columns = 4;
    private static readonly string[] Titles = { "Пистолет", "Автомат", "Дробовик", "Тяжёлая винтовка" };
    private static readonly string[] Roles = { "ТОЧНОСТЬ", "ВЫСОКИЙ ТЕМП", "БЛИЖНИЙ БОЙ", "МОЩНЫЙ ВЫСТРЕЛ" };
    private static readonly Color[] Accents = { new Color(0.64f, 0.79f, 0.85f), GameUi.Accent, new Color(0.94f, 0.65f, 0.35f), new Color(0.73f, 0.64f, 0.92f) };
    private static readonly Color Warm = new Color(1f, 0.6f, 0.22f);
    private static readonly Color Muted = new Color(0.55f, 0.62f, 0.7f);

    private WeaponData[] weapons;
    private System.Action<int> begin;
    private System.Action changed;
    private RectTransform[] tiles;
    private Image[] tileBacks, tileArts;
    private Outline[] tileFrames;
    private GameObject[] tileLocks;
    private Text[] tileCosts;
    private Image detailArt, artBackImage;
    private Text detailRole, detailTitle, detailText, actionLabel, currencyText;
    private Image[] statFills = new Image[3];
    private Text[] statValues = new Text[3];
    private Button action;
    private Image actionBack;
    private float[] statMax = new float[3];
    private float defaultRange = 20f;

    public int Selected { get; private set; }

    public static WeaponSelectScreen Build(RectTransform root, WeaponData[] weapons, System.Action<int> begin, System.Action back, System.Action changed,
        float defaultRange = 20f)
    {
        WeaponSelectScreen screen = root.gameObject.AddComponent<WeaponSelectScreen>();
        screen.weapons = weapons ?? new WeaponData[0];
        screen.begin = begin;
        screen.changed = changed;
        screen.defaultRange = defaultRange;
        screen.Create(root, back);
        return screen;
    }

    public static string Title(WeaponData weapon, int index)
    {
        return !string.IsNullOrEmpty(weapon.title) ? weapon.title : index < Titles.Length ? Titles[index] : weapon.weaponName;
    }

    private static string Role(WeaponData weapon, int index)
    {
        return !string.IsNullOrEmpty(weapon.role) ? weapon.role : index < Roles.Length ? Roles[index] : "ОРУЖИЕ";
    }

    private static Color Accent(WeaponData weapon, int index)
    {
        return weapon.kind != WeaponKind.Standard ? UiArtwork.WeaponAccent(weapon.kind) : Accents[index % Accents.Length];
    }

    // Иконка из той же 3D-модели, что герой держит в бою (Resources/UI/Weapons, печётся WeaponIconBaker).
    private static readonly System.Collections.Generic.Dictionary<string, Sprite> Icons = new System.Collections.Generic.Dictionary<string, Sprite>();

    private static Sprite Icon(WeaponData weapon, int index)
    {
        string key = ModelSkins.WeaponKey(weapon, index);
        Sprite s;
        if (!Icons.TryGetValue(key, out s) || s == null) { s = Resources.Load<Sprite>("UI/Weapons/" + key); Icons[key] = s; }
        return s;
    }

    private static Sprite Art(WeaponData weapon, int index)
    {
        Sprite icon = Icon(weapon, index);
        if (icon != null) return icon;
        return weapon.kind != WeaponKind.Standard ? UiArtwork.WeaponGlyph(weapon.kind) : UiArtwork.Weapon(index);
    }

    // Урон за выстрел, выстрелов в секунду, дальность.
    private float Stat(WeaponData weapon, int stat)
    {
        if (stat == 0) return weapon.damage * Mathf.Max(1, weapon.pelletCount);
        if (stat == 1) return 1f / Mathf.Max(0.03f, weapon.fireRate);
        // У старых записей дальность 0 — в игре тогда берётся дальность стрелка из сцены (AutoShoter.range).
        return weapon.range > 0f ? weapon.range : defaultRange;
    }

    private void Create(RectTransform root, System.Action back)
    {
        Text tag = GameUi.Readable(GameUi.Label(root, "С Н А Р Я Ж Е Н И Е", new Vector2(0.04f, 0.885f), new Vector2(0.5f, 0.925f), 15, Warm), 1f);
        tag.alignment = TextAnchor.MiddleLeft;
        Text header = GameUi.Readable(GameUi.Label(root, "Выбери оружие", new Vector2(0.04f, 0.8f), new Vector2(0.56f, 0.89f), 40, Color.white), 2f);
        header.alignment = TextAnchor.MiddleLeft;
        header.fontStyle = FontStyle.Bold;
        currencyText = GameUi.Readable(GameUi.Label(root, "", new Vector2(0.6f, 0.82f), new Vector2(0.96f, 0.88f), 20, new Color(0.97f, 0.8f, 0.48f)), 1f);
        currencyText.alignment = TextAnchor.MiddleRight;

        // Сетка плиток.
        int count = weapons.Length;
        int selectable = 0;
        for (int i = 0; i < count; i++) if (weapons[i] != null && weapons[i].IsSelectable) selectable++;
        tiles = new RectTransform[count];
        tileBacks = new Image[count];
        tileArts = new Image[count];
        tileFrames = new Outline[count];
        tileLocks = new GameObject[count];
        tileCosts = new Text[count];
        int rows = Mathf.Max(1, Mathf.CeilToInt(selectable / (float)Columns));
        const float left = 0.04f, right = 0.56f, top = 0.785f, bottom = 0.155f, gap = 0.012f;
        float w = (right - left - gap * (Columns - 1)) / Columns;
        float h = (top - bottom - gap * 1.6f * (rows - 1)) / rows;
        int slot = 0;
        for (int i = 0; i < count; i++)
        {
            if (weapons[i] == null || !weapons[i].IsSelectable) continue;
            int col = slot % Columns, row = slot / Columns; slot++;
            float x = left + col * (w + gap), y = top - row * (h + gap * 1.6f);
            Tile(root, i, new Vector2(x, y - h), new Vector2(x + w, y));
        }
        if (count == 0) GameUi.Label(root, "Нет настроенного оружия", new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.6f), 24);

        // Панель подробностей.
        RectTransform panel = GameUi.Box(root, "Weapon Detail", new Vector2(0.59f, 0.155f), new Vector2(0.96f, 0.785f), new Color(0.03f, 0.045f, 0.06f, 0.92f));
        UiArtwork.Round(panel.GetComponent<Image>());
        detailRole = GameUi.Label(panel, "", new Vector2(0.07f, 0.9f), new Vector2(0.93f, 0.97f), 15, Warm);
        detailRole.alignment = TextAnchor.MiddleLeft;
        detailTitle = GameUi.Readable(GameUi.Label(panel, "", new Vector2(0.07f, 0.8f), new Vector2(0.93f, 0.91f), 34, Color.white), 1.5f);
        detailTitle.alignment = TextAnchor.MiddleLeft;
        detailTitle.fontStyle = FontStyle.Bold;
        RectTransform artBack = GameUi.Box(panel, "Art Back", new Vector2(0.07f, 0.47f), new Vector2(0.93f, 0.79f), new Color(0.06f, 0.085f, 0.11f, 1f));
        UiArtwork.Round(artBack.GetComponent<Image>());
        artBackImage = artBack.GetComponent<Image>();
        detailArt = UiArtwork.Picture(artBack, null, new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.97f));
        detailText = GameUi.Label(panel, "", new Vector2(0.07f, 0.375f), new Vector2(0.93f, 0.46f), 16, new Color(0.75f, 0.81f, 0.87f));
        detailText.alignment = TextAnchor.UpperLeft;
        detailText.horizontalOverflow = HorizontalWrapMode.Wrap;
        string[] names = { "Урон", "Темп", "Дальность" };
        for (int s = 0; s < 3; s++)
        {
            float y = 0.335f - s * 0.055f;
            Text name = GameUi.Label(panel, names[s], new Vector2(0.07f, y - 0.05f), new Vector2(0.32f, y), 16, Muted);
            name.alignment = TextAnchor.MiddleLeft;
            RectTransform bar = GameUi.Box(panel, "Stat Bar", new Vector2(0.32f, y - 0.034f), new Vector2(0.76f, y - 0.016f), new Color(0.08f, 0.1f, 0.13f, 1f));
            statFills[s] = GameUi.Box(bar, "Fill", Vector2.zero, Vector2.one, GameUi.Accent).GetComponent<Image>();
            statValues[s] = GameUi.Label(panel, "", new Vector2(0.77f, y - 0.05f), new Vector2(0.95f, y), 16, Color.white);
            statValues[s].alignment = TextAnchor.MiddleRight;
            for (int i = 0; i < count; i++)
                if (weapons[i] != null && weapons[i].IsSelectable) statMax[s] = Mathf.Max(statMax[s], Stat(weapons[i], s));
        }
        action = GameUi.Button(panel, "", new Vector2(0.07f, 0.04f), new Vector2(0.93f, 0.15f), Confirm);
        actionBack = action.GetComponent<Image>();
        UiArtwork.Round(actionBack);
        actionLabel = GameUi.Readable(action.GetComponentInChildren<Text>(), 1.5f);
        actionLabel.fontStyle = FontStyle.Bold;
        actionLabel.fontSize = 26;
        actionLabel.resizeTextMaxSize = 26;

        Button backButton = GameUi.Button(root, "Назад", new Vector2(0.04f, 0.055f), new Vector2(0.2f, 0.125f), () => { if (back != null) back(); });
        UiArtwork.Round(backButton.GetComponent<Image>());
        Text hint = GameUi.Label(root, "Клик по плитке — выбрать, ещё клик — в бой", new Vector2(0.22f, 0.055f), new Vector2(0.56f, 0.125f), 15, Muted);
        hint.alignment = TextAnchor.MiddleLeft;
        Selected = 0;
    }

    private void Tile(RectTransform root, int index, Vector2 min, Vector2 max)
    {
        WeaponData weapon = weapons[index];
        Color accent = Accent(weapon, index);
        RectTransform tile = GameUi.Box(root, "Weapon Card " + index, min, max, new Color(0.04f, 0.06f, 0.08f, 0.9f), true);
        UiArtwork.Round(tile.GetComponent<Image>());
        tiles[index] = tile;
        tileBacks[index] = tile.GetComponent<Image>();
        Button button = tile.gameObject.AddComponent<Button>();
        button.targetGraphic = tileBacks[index];
        button.onClick.AddListener(() => Click(index));
        tileFrames[index] = tile.gameObject.AddComponent<Outline>();
        tileFrames[index].effectDistance = new Vector2(2.5f, -2.5f);
        GameUi.Box(tile, "Accent", new Vector2(0.08f, 0.94f), new Vector2(0.92f, 0.965f), new Color(accent.r, accent.g, accent.b, 0.7f));
        tileArts[index] = UiArtwork.Picture(tile, Art(weapon, index), new Vector2(0.12f, 0.3f), new Vector2(0.88f, 0.9f));
        tileArts[index].color = Icon(weapon, index) != null ? Color.white : weapon.kind != WeaponKind.Standard ? accent : Color.white;
        Text name = GameUi.Readable(GameUi.Label(tile, Title(weapon, index), new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.3f), 16, Color.white), 1f);
        name.fontStyle = FontStyle.Bold;
        // Замок: затемнение и цена в углу.
        GameObject locked = GameUi.Box(tile, "Locked", Vector2.zero, Vector2.one, new Color(0.01f, 0.015f, 0.02f, 0.55f)).gameObject;
        UiArtwork.Round(locked.GetComponent<Image>());
        RectTransform price = GameUi.Box(locked.transform, "Price", new Vector2(0.5f, 0.74f), new Vector2(0.97f, 0.97f), new Color(0.02f, 0.03f, 0.04f, 0.95f));
        UiArtwork.Round(price.GetComponent<Image>());
        tileCosts[index] = GameUi.Label(price, "", Vector2.zero, Vector2.one, 14, new Color(0.97f, 0.8f, 0.48f));
        tileLocks[index] = locked;
    }

    private void Click(int index)
    {
        if (index == Selected) { Confirm(); return; }
        Select(index);
    }

    public void Select(int index)
    {
        if (index < 0 || index >= weapons.Length || tiles[index] == null) return;
        Selected = index;
        Refresh();
    }

    // «В бой» для открытого оружия, покупка для закрытого (если хватает деталей).
    public void Confirm()
    {
        int index = Selected;
        if (index < 0 || index >= weapons.Length) return;
        if (!Unlocked(index))
        {
            if (MetaProgress.TryUnlock(MetaProgress.WeaponKey(index)))
            {
                Refresh();
                if (changed != null) changed();
            }
            return;
        }
        if (begin != null) begin(index);
    }

    private static bool Unlocked(int index)
    {
        MetaUnlock unlock = MetaProgress.FindUnlock(MetaProgress.WeaponKey(index));
        return unlock == null || MetaProgress.IsUnlocked(unlock.id) || MetaProgress.DevUnlockAllWeapons;
    }

    public void Refresh()
    {
        if (tiles == null) return;
        if (Selected < 0 || Selected >= tiles.Length || tiles[Selected] == null) Selected = 0;
        currencyText.text = MetaProgress.CurrencyName + ":  " + MetaProgress.Currency;
        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i] == null) continue;
            bool open = Unlocked(i);
            tileLocks[i].SetActive(!open);
            if (!open)
            {
                MetaUnlock unlock = MetaProgress.FindUnlock(MetaProgress.WeaponKey(i));
                tileCosts[i].text = unlock != null ? unlock.cost.ToString() : "";
            }
            bool chosen = i == Selected;
            Color accent = Accent(weapons[i], i);
            tileFrames[i].effectColor = chosen ? accent : new Color(0f, 0f, 0f, 0f);
            tileBacks[i].color = chosen ? new Color(0.07f + accent.r * 0.08f, 0.09f + accent.g * 0.08f, 0.12f + accent.b * 0.08f, 0.95f)
                : new Color(0.04f, 0.06f, 0.08f, 0.9f);
            tiles[i].localScale = chosen ? Vector3.one * 1.04f : Vector3.one;
        }

        WeaponData weapon = weapons[Selected];
        Color tone = Accent(weapon, Selected);
        detailRole.text = Role(weapon, Selected);
        detailRole.color = tone;
        detailTitle.text = Title(weapon, Selected);
        artBackImage.color = Color.Lerp(new Color(0.06f, 0.085f, 0.11f, 1f), tone, 0.22f);
        detailArt.sprite = Art(weapon, Selected);
        detailArt.color = Icon(weapon, Selected) == null && weapon.kind != WeaponKind.Standard ? tone : Color.white;
        string about = !string.IsNullOrEmpty(weapon.description) ? weapon.description :
            "Обычное оружие: стреляет в ближайшего врага" + (weapon.pelletCount > 1 ? ", " + weapon.pelletCount + " дробин за выстрел." : ".");
        detailText.text = about;
        for (int s = 0; s < 3; s++)
        {
            float value = Stat(weapon, s);
            statFills[s].rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value / Mathf.Max(0.01f, statMax[s])), 1f);
            statFills[s].color = tone;
            statValues[s].text = s == 0 ? (weapon.pelletCount > 1 ? weapon.damage + "×" + weapon.pelletCount : weapon.damage.ToString()) :
                s == 1 ? value.ToString("0.#") + " /с" : Mathf.RoundToInt(value) + " м";
        }

        bool unlocked = Unlocked(Selected);
        MetaUnlock price = MetaProgress.FindUnlock(MetaProgress.WeaponKey(Selected));
        bool affordable = price != null && MetaProgress.Currency >= price.cost;
        string currency = MetaProgress.CurrencyName.ToLowerInvariant();
        actionLabel.text = unlocked ? "В  БОЙ" : affordable ? "ОТКРЫТЬ ЗА " + price.cost + " " + currency.ToUpperInvariant() : "НУЖНО " + price.cost + " " + currency;
        actionBack.color = unlocked ? new Color(0.75f, 0.38f, 0.1f, 1f) : affordable ? new Color(0.08f, 0.38f, 0.33f, 1f) : new Color(0.12f, 0.14f, 0.17f, 1f);
        actionLabel.color = unlocked || affordable ? Color.white : Muted;
        action.interactable = unlocked || affordable;
    }
}
