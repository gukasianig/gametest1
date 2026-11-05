using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(AutoShoter))]
[RequireComponent(typeof(PlayerProgression))]
public class StartMenu : MonoBehaviour
{
    private AutoShoter shooter;
    private Canvas menu;
    private Canvas[] gameplayCanvases;
    private bool[] canvasStates;
    private RectTransform home;
    private RectTransform selection;
    private RectTransform workshop, modifiersScreen;
    private ModifierRow[] modifierRows;
    private Text summary, workshopCurrency, selectionHint, modifiersTotal;
    private RectTransform[] weaponLocks;
    private Text[] weaponLockText;
    private WorkshopRow[] workshopRows;
    private bool waiting;
    public bool WaitingForWeapon => waiting;

    private void Awake()
    {
        if (GetComponent<GameSession>() == null) gameObject.AddComponent<GameSession>();
        if (GetComponent<RunStatistics>() == null) gameObject.AddComponent<RunStatistics>();
    }

    private void Start()
    {
        shooter = GetComponent<AutoShoter>();
        gameplayCanvases = FindObjectsOfType<Canvas>();
        canvasStates = new bool[gameplayCanvases.Length];
        for (int i = 0; i < gameplayCanvases.Length; i++)
        {
            canvasStates[i] = gameplayCanvases[i].enabled;
            gameplayCanvases[i].enabled = false;
        }
        if (EventSystem.current == null)
            new GameObject("Menu EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        menu = GameUi.Canvas("Start Menu", 100);
        RectTransform background = GameUi.Box(menu.transform, "Background", Vector2.zero, Vector2.one,
            new Color(0.027f, 0.037f, 0.055f), true);
        GameUi.Box(background, "Top accent", new Vector2(0.06f, 0.963f), new Vector2(0.94f, 0.965f),
            new Color(0.23f, 0.64f, 0.56f, 0.5f));
        GameUi.Box(background, "Bottom accent", new Vector2(0.06f, 0.035f), new Vector2(0.94f, 0.037f),
            new Color(0.23f, 0.34f, 0.38f, 0.5f));

        home = GameUi.Box(background, "Home", Vector2.zero, Vector2.one, Color.clear);
        GameUi.Label(home, "ARENA / SURVIVAL", new Vector2(0.1f, 0.85f), new Vector2(0.9f, 0.93f), 18, GameUi.Accent);
        GameUi.Label(home, "ГЛАВНОЕ МЕНЮ", new Vector2(0.1f, 0.66f), new Vector2(0.9f, 0.75f), 20, GameUi.Accent);
        GameUi.Label(home, "Выживи в арене", new Vector2(0.1f, 0.48f), new Vector2(0.9f, 0.64f), 46,
            new Color(0.92f, 0.94f, 0.96f));
        GameUi.Label(home, "10 минут выживания. Один Страж. Твоя сборка.", new Vector2(0.12f, 0.39f), new Vector2(0.88f, 0.47f),
            19, new Color(0.58f, 0.65f, 0.72f));
        Button start = GameUi.Button(home, "Старт", new Vector2(0.07f, 0.21f), new Vector2(0.34f, 0.33f), ShowWeapons);
        UiArtwork.Round(start.GetComponent<Image>());
        Button shop = GameUi.Button(home, "Мастерская", new Vector2(0.365f, 0.21f), new Vector2(0.635f, 0.33f), ShowWorkshop);
        UiArtwork.Round(shop.GetComponent<Image>());
        Button conditions = GameUi.Button(home, "Условия", new Vector2(0.66f, 0.21f), new Vector2(0.93f, 0.33f), ShowModifiers);
        UiArtwork.Round(conditions.GetComponent<Image>());
        summary = GameUi.Label(home, "", new Vector2(0.06f, 0.07f), new Vector2(0.94f, 0.19f), 17, new Color(0.58f, 0.65f, 0.72f));
        BuildWorkshop(background);
        BuildModifiers(background);
        RefreshHome();

        selection = GameUi.Box(background, "Weapon Selection", Vector2.zero, Vector2.one, Color.clear);
        GameUi.Label(selection, "СНАРЯЖЕНИЕ", new Vector2(0.08f, 0.87f), new Vector2(0.92f, 0.93f), 16, GameUi.Accent);
        GameUi.Label(selection, "Выбери оружие", new Vector2(0.08f, 0.77f), new Vector2(0.92f, 0.87f), 36,
            new Color(0.92f, 0.94f, 0.96f));
        selectionHint = GameUi.Label(selection, "", new Vector2(0.06f, 0.7f), new Vector2(0.94f, 0.77f), 16,
            new Color(0.57f, 0.64f, 0.72f));
        int count = shooter.weapons == null ? 0 : shooter.weapons.Length;
        weaponLocks = new RectTransform[Mathf.Max(count, 1)];
        weaponLockText = new Text[Mathf.Max(count, 1)];
        weaponCards = new RectTransform[Mathf.Max(count, 1)];
        weaponPages = Mathf.Max(1, Mathf.CeilToInt(count / (float)CardsPerPage));
        for (int i = 0; i < count; i++)
        {
            WeaponData weapon = shooter.weapons[i];
            if (weapon == null || !weapon.IsUsable) continue;
            // Four cards (2x2) per page; the page buttons below switch between pages.
            int slot = i % CardsPerPage;
            float x = slot % 2 == 0 ? 0.06f : 0.52f;
            float top = 0.67f - (slot / 2) * 0.26f;
            WeaponCard(i, weapon, new Vector2(x, top - 0.23f), new Vector2(x + 0.42f, top));
        }
        if (count == 0)
            GameUi.Label(selection, "Нет настроенного оружия", new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.6f), 24);
        Button back = GameUi.Button(selection, "Назад", new Vector2(0.41f, 0.065f), new Vector2(0.59f, 0.14f), () =>
        {
            selection.gameObject.SetActive(false);
            home.gameObject.SetActive(true);
        });
        UiArtwork.Round(back.GetComponent<Image>());
        if (weaponPages > 1)
        {
            Button previous = GameUi.Button(selection, "◄", new Vector2(0.17f, 0.065f), new Vector2(0.3f, 0.14f), PreviousWeaponPage);
            UiArtwork.Round(previous.GetComponent<Image>());
            Button next = GameUi.Button(selection, "►", new Vector2(0.7f, 0.065f), new Vector2(0.83f, 0.14f), NextWeaponPage);
            UiArtwork.Round(next.GetComponent<Image>());
        }
        selection.gameObject.SetActive(false);
        ShowWeaponPage(0);
        waiting = true;
        Time.timeScale = 0f;
        int restart = GameSession.ConsumeRestart();
        if (restart >= 0) BeginGame(restart);
    }

    private const int CardsPerPage = 4;
    private RectTransform[] weaponCards;
    private int weaponPages = 1;
    public int WeaponPage { get; private set; }
    public int WeaponPageCount { get { return weaponPages; } }

    public void NextWeaponPage() { ShowWeaponPage(WeaponPage + 1); }
    public void PreviousWeaponPage() { ShowWeaponPage(WeaponPage - 1); }

    public void ShowWeaponPage(int page)
    {
        WeaponPage = ((page % weaponPages) + weaponPages) % weaponPages;
        for (int i = 0; i < weaponCards.Length; i++)
            if (weaponCards[i] != null) weaponCards[i].gameObject.SetActive(i / CardsPerPage == WeaponPage);
        RefreshWeaponLocks();
    }

    public void ShowWeapons()
    {
        RefreshWeaponLocks();
        home.gameObject.SetActive(false);
        selection.gameObject.SetActive(true);
    }

    public void ShowWorkshop()
    {
        home.gameObject.SetActive(false);
        RefreshWorkshop();
        workshop.gameObject.SetActive(true);
    }

    private void RefreshHome()
    {
        MetaRecord best = MetaProgress.Record("all");
        string line = MetaProgress.CurrencyName + ": " + MetaProgress.Currency;
        if (best != null)
            line += "   •   Лучшее время " + FormatTime(best.bestTime) + "   •   Убийств " + best.bestKills +
                "   •   Забегов " + MetaProgress.Data.runs;
        summary.text = line + "\n" + RunModifiers.Summary();
    }

    public void ShowModifiers()
    {
        home.gameObject.SetActive(false);
        RefreshModifiers();
        modifiersScreen.gameObject.SetActive(true);
    }

    private void BuildModifiers(RectTransform background)
    {
        modifiersScreen = GameUi.Box(background, "Modifiers", Vector2.zero, Vector2.one, Color.clear);
        GameUi.Label(modifiersScreen, "УСЛОВИЯ СМЕНЫ", new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.94f), 16, GameUi.Accent);
        GameUi.Label(modifiersScreen, "Усложни забег — получи больше деталей", new Vector2(0.08f, 0.79f), new Vector2(0.92f, 0.88f), 32,
            new Color(0.92f, 0.94f, 0.96f));
        modifiersTotal = GameUi.Label(modifiersScreen, "", new Vector2(0.08f, 0.71f), new Vector2(0.92f, 0.79f), 22, GameUi.Accent);
        int count = RunModifiers.Catalog.Length;
        float pitch = Mathf.Min(0.135f, 0.53f / Mathf.Max(1, count));
        modifierRows = new ModifierRow[count];
        for (int i = 0; i < count; i++)
        {
            RunModifier modifier = RunModifiers.Catalog[i];
            float top = 0.69f - i * pitch;
            RectTransform row = GameUi.Box(modifiersScreen, "Row " + modifier.id, new Vector2(0.06f, top - pitch + 0.015f),
                new Vector2(0.94f, top), new Color(0.055f, 0.075f, 0.1f), true);
            UiArtwork.Round(row.GetComponent<Image>());
            Text title = GameUi.Label(row, "<b>" + modifier.title + "</b>\n<color=#8FA0B3>" + modifier.description + "</color>",
                new Vector2(0.02f, 0f), new Vector2(0.62f, 1f), 20, new Color(0.93f, 0.95f, 0.97f));
            title.alignment = TextAnchor.MiddleLeft;
            Text bonus = GameUi.Label(row, "+" + Mathf.RoundToInt(modifier.reward * 100f) + "% к награде",
                new Vector2(0.62f, 0f), new Vector2(0.78f, 1f), 17, new Color(0.62f, 0.69f, 0.77f));
            string id = modifier.id;
            Button toggle = GameUi.Button(row, "", new Vector2(0.8f, 0.15f), new Vector2(0.98f, 0.85f), () => ToggleModifier(id));
            UiArtwork.Round(toggle.GetComponent<Image>());
            modifierRows[i] = new ModifierRow { modifier = modifier, button = toggle, label = toggle.GetComponentInChildren<Text>() };
        }
        Button clear = GameUi.Button(modifiersScreen, "Сбросить", new Vector2(0.2f, 0.04f), new Vector2(0.4f, 0.115f), () =>
        {
            RunModifiers.Clear();
            RefreshModifiers();
        });
        UiArtwork.Round(clear.GetComponent<Image>());
        Button back = GameUi.Button(modifiersScreen, "Назад", new Vector2(0.6f, 0.04f), new Vector2(0.8f, 0.115f), () =>
        {
            modifiersScreen.gameObject.SetActive(false);
            RefreshHome();
            home.gameObject.SetActive(true);
        });
        UiArtwork.Round(back.GetComponent<Image>());
        modifiersScreen.gameObject.SetActive(false);
    }

    private void ToggleModifier(string id)
    {
        RunModifiers.Toggle(id);
        RefreshModifiers();
    }

    private void RefreshModifiers()
    {
        modifiersTotal.text = RunModifiers.ActiveCount == 0 ? "Условия обычные   •   награда ×1" :
            "Включено: " + RunModifiers.ActiveCount + "   •   награда ×" + RunModifiers.RewardMultiplier.ToString("0.##");
        foreach (ModifierRow row in modifierRows)
        {
            bool on = RunModifiers.IsActive(row.modifier.id);
            row.label.text = on ? "Включено" : "Включить";
            row.button.GetComponent<Image>().color = on ? new Color(0.12f, 0.45f, 0.4f) : new Color(0.08f, 0.27f, 0.31f);
        }
    }

    private class ModifierRow
    {
        public RunModifier modifier;
        public Text label;
        public Button button;
    }

    public static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(Mathf.Max(0f, seconds));
        return total / 60 + ":" + (total % 60).ToString("00");
    }

    private void BuildWorkshop(RectTransform background)
    {
        workshop = GameUi.Box(background, "Workshop", Vector2.zero, Vector2.one, Color.clear);
        GameUi.Label(workshop, "МАСТЕРСКАЯ", new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.94f), 16, GameUi.Accent);
        GameUi.Label(workshop, "Постоянные улучшения", new Vector2(0.08f, 0.79f), new Vector2(0.92f, 0.88f), 34,
            new Color(0.92f, 0.94f, 0.96f));
        workshopCurrency = GameUi.Label(workshop, "", new Vector2(0.08f, 0.71f), new Vector2(0.92f, 0.79f), 22, GameUi.Accent);
        int count = MetaProgress.Catalog.Length;
        float pitch = Mathf.Min(0.135f, 0.53f / Mathf.Max(1, count));
        workshopRows = new WorkshopRow[count];
        for (int i = 0; i < count; i++)
        {
            MetaUpgrade upgrade = MetaProgress.Catalog[i];
            float top = 0.69f - i * pitch;
            RectTransform row = GameUi.Box(workshop, "Row " + upgrade.id, new Vector2(0.06f, top - pitch + 0.015f),
                new Vector2(0.94f, top), new Color(0.055f, 0.075f, 0.1f), true);
            UiArtwork.Round(row.GetComponent<Image>());
            Text title = GameUi.Label(row, "<b>" + upgrade.title + "</b>\n<color=#8FA0B3>" + upgrade.description + "</color>",
                new Vector2(0.02f, 0f), new Vector2(0.5f, 1f), 20, new Color(0.93f, 0.95f, 0.97f));
            title.alignment = TextAnchor.MiddleLeft;
            Text state = GameUi.Label(row, "", new Vector2(0.5f, 0f), new Vector2(0.74f, 1f), 18, new Color(0.62f, 0.69f, 0.77f));
            string id = upgrade.id;
            Button buy = GameUi.Button(row, "", new Vector2(0.76f, 0.15f), new Vector2(0.98f, 0.85f), () => BuyUpgrade(id));
            UiArtwork.Round(buy.GetComponent<Image>());
            workshopRows[i] = new WorkshopRow { upgrade = upgrade, state = state, button = buy, label = buy.GetComponentInChildren<Text>() };
        }
        Button back = GameUi.Button(workshop, "Назад", new Vector2(0.41f, 0.04f), new Vector2(0.59f, 0.115f), () =>
        {
            workshop.gameObject.SetActive(false);
            RefreshHome();
            home.gameObject.SetActive(true);
        });
        UiArtwork.Round(back.GetComponent<Image>());
        workshop.gameObject.SetActive(false);
    }

    private void BuyUpgrade(string id)
    {
        MetaProgress.Buy(id);
        RefreshWorkshop();
    }

    private void RefreshWorkshop()
    {
        workshopCurrency.text = MetaProgress.CurrencyName + ": " + MetaProgress.Currency;
        foreach (WorkshopRow row in workshopRows)
        {
            int rank = MetaProgress.Rank(row.upgrade.id);
            bool maxed = rank >= row.upgrade.maxRank;
            row.state.text = "Ранг " + rank + " / " + row.upgrade.maxRank + "\n" + MetaProgress.EffectText(row.upgrade, rank) +
                (maxed ? "" : " → " + MetaProgress.EffectText(row.upgrade, rank + 1));
            row.label.text = maxed ? "Максимум" : "Улучшить  " + row.upgrade.Cost(rank);
            row.button.interactable = MetaProgress.CanBuy(row.upgrade);
        }
    }

    private class WorkshopRow
    {
        public MetaUpgrade upgrade;
        public Text state, label;
        public Button button;
    }

    private void WeaponCard(int index, WeaponData weapon, Vector2 min, Vector2 max)
    {
        string[] titles = { "Пистолет", "Автомат", "Дробовик", "Тяжёлая винтовка" };
        string[] roles = { "ТОЧНОСТЬ", "ВЫСОКИЙ ТЕМП", "БЛИЖНИЙ БОЙ", "МОЩНЫЙ ВЫСТРЕЛ" };
        Color[] accents = { new Color(0.64f, 0.79f, 0.85f), GameUi.Accent,
            new Color(0.94f, 0.65f, 0.35f), new Color(0.73f, 0.64f, 0.92f) };
        bool special = weapon.kind != WeaponKind.Standard;
        Color accent = special ? UiArtwork.WeaponAccent(weapon.kind) : accents[index % accents.Length];
        RectTransform card = GameUi.Box(selection, "Weapon Card " + index, min, max, new Color(0.055f, 0.075f, 0.1f), true);
        UiArtwork.Round(card.GetComponent<Image>());
        weaponCards[index] = card;
        Button button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = card.GetComponent<Image>();
        button.onClick.AddListener(() => ChooseWeapon(index));
        Image art = UiArtwork.Picture(card, special ? UiArtwork.WeaponGlyph(weapon.kind) : UiArtwork.Weapon(index),
            new Vector2(0.035f, 0.21f), new Vector2(0.43f, 0.86f));
        if (special) art.color = accent;
        string roleText = !string.IsNullOrEmpty(weapon.role) ? weapon.role : index < roles.Length ? roles[index] : "ОРУЖИЕ";
        string titleText = !string.IsNullOrEmpty(weapon.title) ? weapon.title : index < titles.Length ? titles[index] : weapon.weaponName;
        Text role = GameUi.Label(card, roleText, new Vector2(0.46f, 0.73f), new Vector2(0.97f, 0.94f), 12, accent);
        role.alignment = TextAnchor.MiddleLeft;
        Text title = GameUi.Label(card, titleText,
            new Vector2(0.46f, 0.49f), new Vector2(0.98f, 0.77f), 27, new Color(0.93f, 0.95f, 0.97f));
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleLeft;
        string stats = special ?
            "Урон  " + weapon.damage + "    •    " + (1f / Mathf.Max(0.03f, weapon.fireRate)).ToString("0.#") + " /с\n" + weapon.description :
            "Урон  " + weapon.damage + "    •    " + (1f / Mathf.Max(0.03f, weapon.fireRate)).ToString("0.#") +
            " /с\nСнарядов за выстрел  " + Mathf.Max(1, weapon.pelletCount);
        Text detail = GameUi.Label(card, stats, new Vector2(0.46f, 0.12f), new Vector2(0.98f, 0.5f),
            16, new Color(0.62f, 0.69f, 0.77f));
        detail.alignment = TextAnchor.MiddleLeft;
        GameUi.Box(card, "Accent", new Vector2(0.04f, 0.965f), new Vector2(0.96f, 0.977f), new Color(accent.r, accent.g, accent.b, 0.5f));
        UpgradeCardFx effects = card.gameObject.AddComponent<UpgradeCardFx>();
        effects.icon = art.transform;
        effects.Present(accent, index);
        // Drawn last so it dims the card; it does not block clicks, so the card button still opens the purchase.
        RectTransform shade = GameUi.Box(card, "Locked", Vector2.zero, Vector2.one, new Color(0.02f, 0.03f, 0.04f, 0.84f));
        UiArtwork.Round(shade.GetComponent<Image>());
        weaponLocks[index] = shade;
        weaponLockText[index] = GameUi.Label(shade, "", new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.8f), 24);
    }

    private void ChooseWeapon(int index)
    {
        if (!MetaProgress.IsWeaponUnlocked(index))
        {
            if (MetaProgress.TryUnlock(MetaProgress.WeaponKey(index))) { RefreshWeaponLocks(); RefreshHome(); }
            return;
        }
        BeginGame(index);
    }

    private void RefreshWeaponLocks()
    {
        for (int i = 0; i < weaponLocks.Length; i++)
        {
            if (weaponLocks[i] == null) continue;
            MetaUnlock unlock = MetaProgress.FindUnlock(MetaProgress.WeaponKey(i));
            bool locked = unlock != null && !MetaProgress.IsUnlocked(unlock.id);
            weaponLocks[i].gameObject.SetActive(locked);
            if (!locked) continue;
            bool affordable = MetaProgress.Currency >= unlock.cost;
            weaponLockText[i].text = "ЗАКРЫТО\n" + (affordable ? "Нажми, чтобы открыть за " : "Нужно ") + unlock.cost + " " +
                MetaProgress.CurrencyName.ToLowerInvariant();
            weaponLockText[i].color = affordable ? GameUi.Accent : new Color(0.62f, 0.69f, 0.77f);
        }
        if (selectionHint != null)
            selectionHint.text = (weaponPages > 1 ? "Страница " + (WeaponPage + 1) + " из " + weaponPages + "   •   " : "") +
                "Нажми на карточку, чтобы начать. Закрытое оружие открывается за детали (у тебя: " + MetaProgress.Currency + ").";
    }

    private void BeginGame(int index)
    {
        if (!waiting || !MetaProgress.IsWeaponUnlocked(index) || !shooter.SelectWeapon(index)) return;
        waiting = false;
        GetComponent<PlayerProgression>().ApplyMetaBonuses();
        GetComponent<GameSession>().Begin(index);
        GetComponent<RunStatistics>().BeginRun();
        for (int i = 0; i < gameplayCanvases.Length; i++)
            if (gameplayCanvases[i] != null) gameplayCanvases[i].enabled = canvasStates[i];
        menu.gameObject.SetActive(false);
        Time.timeScale = 1f;
        if (ArenaLayout.Instance != null) ArenaLayout.Instance.Tint(gameObject, GameUi.Accent);
    }

    private void OnDestroy()
    {
        if (waiting) Time.timeScale = 1f;
        if (menu != null) Destroy(menu.gameObject);
    }
}
