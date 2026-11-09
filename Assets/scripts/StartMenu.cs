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
    private RectTransform workshop, modifiersScreen, settingsScreen, statsScreen;
    private ChallengeScreen challengeScreen;
    private Text statsHeader;
    private Text[] statsNames = new Text[3], statsValues = new Text[3];
    private Text lastRunConditions;
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
        GameAudio.SetMusic(false);
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
        home = GameUi.Box(background, "Home", Vector2.zero, Vector2.one, Color.clear);
        // Фон из Blender (ночная арена); без картинки остаётся тёмная заливка.
        MenuBackdrop.Build(background, home);

        // Заголовок слева: метка, крупное название, акцентная черта, подзаголовок.
        Text tag = GameUi.Readable(GameUi.Label(home, "A R E N A   /   S U R V I V A L", new Vector2(0.06f, 0.865f), new Vector2(0.6f, 0.905f), 16,
            new Color(1f, 0.62f, 0.25f)), 1f);
        tag.alignment = TextAnchor.MiddleLeft;
        Text title = GameUi.Readable(GameUi.Label(home, "ВЫЖИВИ\nВ АРЕНЕ", new Vector2(0.06f, 0.655f), new Vector2(0.6f, 0.87f), 74,
            new Color(0.96f, 0.97f, 0.98f)), 2.5f);
        title.alignment = TextAnchor.MiddleLeft;
        title.fontStyle = FontStyle.Bold;
        title.lineSpacing = 0.9f;
        RectTransform titleLine = GameUi.Box(home, "Title Line", new Vector2(0.06f, 0.648f), new Vector2(0.17f, 0.654f), new Color(1f, 0.6f, 0.22f));
        Text subtitle = GameUi.Readable(GameUi.Label(home, "10 минут выживания. Один Страж. Твоя сборка.", new Vector2(0.06f, 0.595f),
            new Vector2(0.6f, 0.64f), 19, new Color(0.7f, 0.77f, 0.84f)), 1f);
        subtitle.alignment = TextAnchor.MiddleLeft;

        // Кнопки столбиком: «Старт» крупнее и с тёплым акцентом.
        Color warm = new Color(1f, 0.6f, 0.22f);
        Button start = MenuButton("Старт", 0.555f, 0.085f, ShowWeapons, warm, 32, new Color(0.13f, 0.07f, 0.03f, 0.88f));
        start.GetComponent<MenuButtonFx>().pulse = true;
        Button workshopButton = MenuButton("Мастерская", 0.455f, 0.062f, ShowWorkshop, GameUi.Accent, 24);
        Button modifiersButton = MenuButton("Испытания", 0.386f, 0.062f, ShowModifiers, GameUi.Accent, 24);
        Button statsButton = MenuButton("Статистика", 0.317f, 0.062f, ShowStatistics, GameUi.Accent, 24);
        Button settingsButton = MenuButton("Настройки", 0.248f, 0.062f, () =>
        {
            home.gameObject.SetActive(false);
            settingsScreen.gameObject.SetActive(true);
        }, GameUi.Accent, 24);
        Button testButton = MenuButton("Тест-арена", 0.179f, 0.062f, BeginTestArena, new Color(0.55f, 0.62f, 0.7f), 22);
        settingsScreen = SettingsPanel.Build(background, () =>
        {
            settingsScreen.gameObject.SetActive(false);
            home.gameObject.SetActive(true);
        });
        // Выбор арены: меняет тему сразу (арена пересобирается за меню).
        Button arenaButton = MenuButton("", 0.101f, 0.05f,() => { }, new Color(0.55f, 0.62f, 0.7f), 18);
        Text arenaLabel = arenaButton.GetComponentInChildren<Text>();
        System.Action refreshArena = () => { if (arenaLabel != null) arenaLabel.text = "Арена:  " + ArenaThemes.Names[(int)ArenaThemes.Current] + "   ►"; };
        refreshArena();
        arenaButton.onClick.AddListener(() => { ArenaThemes.Set(ArenaThemes.Next(ArenaThemes.Current)); ArenaThemes.Apply(); refreshArena(); });
        // Зелёная арена (открытое поле) — единственная карта: темы Завод/Пещера/Ледник к ней не относятся, кнопку прячем.
        if (ArenaLayout.Instance != null && ArenaLayout.Instance.openField) arenaButton.gameObject.SetActive(false);
        // Выход из игры: справа внизу, над номером версии.
        Button quitButton = MenuButton("Выход", 0.137f, 0.062f, QuitGame, new Color(1f, 0.42f, 0.32f), 22);
        RectTransform quitRect = (RectTransform)quitButton.transform;
        quitRect.anchorMin = new Vector2(0.79f, quitRect.anchorMin.y);
        quitRect.anchorMax = new Vector2(0.95f, quitRect.anchorMax.y);
        Text version = GameUi.Label(home, "v" + Application.version, new Vector2(0.7f, 0.025f), new Vector2(0.97f, 0.06f), 14,
            new Color(0.45f, 0.52f, 0.58f));
        version.alignment = TextAnchor.MiddleRight;

        // Появление: метка, заголовок, черта, подзаголовок, затем кнопки по очереди и версия (MenuIntro).
        MenuIntro intro = home.gameObject.AddComponent<MenuIntro>();
        intro.Add(tag, 0f, 40f);
        intro.Add(title, 0.06f, 70f);
        intro.Add(titleLine, 0.2f, 0f, true);
        intro.Add(subtitle, 0.24f, 40f);
        Button[] column = { start, workshopButton, modifiersButton, statsButton, settingsButton, testButton, arenaButton };
        for (int i = 0; i < column.Length; i++) intro.Add(column[i], 0.32f + 0.055f * i, 60f);
        intro.Add(quitButton, 0.7f, 0f);
        intro.Add(version, 0.72f, 0f);
        BuildWorkshop(background);
        BuildModifiers(background);
        BuildStatistics(background);

        selection = GameUi.Box(background, "Weapon Selection", Vector2.zero, Vector2.one, Color.clear);
        // Выбор оружия: сетка плиток + панель подробностей (WeaponSelectScreen).
        weaponScreen = WeaponSelectScreen.Build(selection, shooter.weapons, ChooseWeapon, () =>
        {
            selection.gameObject.SetActive(false);
            home.gameObject.SetActive(true);
        }, null, shooter.range);
        selection.gameObject.SetActive(false);
        waiting = true;
        Time.timeScale = 0f;
        int restart = GameSession.ConsumeRestart();
        if (restart >= 0) BeginGame(restart);
    }

    // Кнопка главного меню в левом столбце: тёмная плашка, цветная полоса слева, подпись слева, эффект наведения.
    private Button MenuButton(string text, float top, float height, UnityEngine.Events.UnityAction action, Color accent, int size,
        Color? back = null)
    {
        Button button = GameUi.Button(home, text, new Vector2(0.06f, top - height), new Vector2(0.31f, top), action);
        Image image = button.GetComponent<Image>();
        image.color = back ?? new Color(0.03f, 0.05f, 0.07f, 0.8f);
        UiArtwork.Round(image);
        Text label = button.GetComponentInChildren<Text>();
        label.alignment = TextAnchor.MiddleLeft;
        label.rectTransform.offsetMin = new Vector2(30f, 4f);
        label.fontSize = size;
        label.resizeTextMaxSize = size;
        if (size >= 30) label.fontStyle = FontStyle.Bold;
        GameUi.Readable(label, 1f);
        Image stripe = GameUi.Box(button.transform, "Stripe", new Vector2(0f, 0.12f), new Vector2(0.018f, 0.88f), accent).GetComponent<Image>();
        MenuButtonFx fx = button.gameObject.AddComponent<MenuButtonFx>();
        fx.stripe = stripe;
        fx.label = label;
        fx.accent = accent;
        fx.Init(image);
        return button;
    }

    private WeaponSelectScreen weaponScreen;
    public WeaponSelectScreen WeaponScreen => weaponScreen;

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

    public static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ShowStatistics()
    {
        home.gameObject.SetActive(false);
        RefreshStatistics();
        statsScreen.gameObject.SetActive(true);
    }

    // Экран «Статистика»: три панели — за всё время, последний забег, рекорды по оружию.
    private void BuildStatistics(RectTransform background)
    {
        statsScreen = GameUi.Box(background, "Statistics", Vector2.zero, Vector2.one, new Color(0.02f, 0.03f, 0.045f, 0.92f), true);
        Text caption = GameUi.Label(statsScreen, "СТАТИСТИКА", new Vector2(0.04f, 0.9f), new Vector2(0.96f, 0.95f), 16, GameUi.Accent);
        caption.alignment = TextAnchor.MiddleLeft;
        statsHeader = GameUi.Readable(GameUi.Label(statsScreen, "", new Vector2(0.04f, 0.83f), new Vector2(0.96f, 0.9f), 30,
            new Color(0.94f, 0.95f, 0.97f)), 1f);
        statsHeader.alignment = TextAnchor.MiddleLeft;
        string[] titles = { "ЗА ВСЁ ВРЕМЯ", "ПОСЛЕДНИЙ ЗАБЕГ", "РЕКОРДЫ ПО ОРУЖИЮ" };
        float[] lefts = { 0.03f, 0.355f, 0.68f };
        for (int i = 0; i < 3; i++)
        {
            RectTransform panel = GameUi.Box(statsScreen, "Panel " + i, new Vector2(lefts[i], 0.15f), new Vector2(lefts[i] + 0.29f, 0.81f),
                new Color(0.055f, 0.075f, 0.1f, 0.95f), true);
            UiArtwork.Round(panel.GetComponent<Image>());
            Text title = GameUi.Label(panel, titles[i], new Vector2(0.04f, 0.9f), new Vector2(0.96f, 0.98f), 18,
                i == 1 ? new Color(1f, 0.62f, 0.25f) : GameUi.Accent);
            title.alignment = TextAnchor.MiddleLeft;
            title.fontStyle = FontStyle.Bold;
            GameUi.Box(panel, "Line", new Vector2(0.04f, 0.895f), new Vector2(0.96f, 0.899f), new Color(1f, 1f, 1f, 0.08f));
            float bottom = i == 1 ? 0.15f : 0.03f;
            statsNames[i] = StatsColumn(panel, new Vector2(0.03f, bottom), new Vector2(0.6f, 0.88f), TextAnchor.UpperLeft,
                new Color(0.62f, 0.69f, 0.77f));
            statsValues[i] = StatsColumn(panel, new Vector2(0.4f, bottom), new Vector2(0.97f, 0.88f), TextAnchor.UpperRight,
                new Color(0.94f, 0.95f, 0.97f));
            if (i == 1)
            {
                lastRunConditions = GameUi.Label(panel, "", new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.15f), 14,
                    new Color(0.62f, 0.69f, 0.77f));
                lastRunConditions.alignment = TextAnchor.LowerLeft;
            }
        }
        Button back = GameUi.Button(statsScreen, "Назад", new Vector2(0.4f, 0.04f), new Vector2(0.6f, 0.115f), () =>
        {
            statsScreen.gameObject.SetActive(false);
            home.gameObject.SetActive(true);
        });
        UiArtwork.Round(back.GetComponent<Image>());
        statsScreen.gameObject.SetActive(false);
    }

    // Столбец таблицы: фиксированный размер шрифта, без переноса, чтобы строки названий и значений совпадали.
    private static Text StatsColumn(RectTransform panel, Vector2 min, Vector2 max, TextAnchor anchor, Color color)
    {
        Text text = GameUi.Label(panel, "", min, max, 16, color);
        text.alignment = anchor;
        text.resizeTextForBestFit = false;
        text.fontSize = 16;
        text.lineSpacing = 1.25f;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static void FillColumns(Text names, Text values, System.Collections.Generic.List<string[]> rows)
    {
        var left = new System.Text.StringBuilder();
        var right = new System.Text.StringBuilder();
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0) { left.Append('\n'); right.Append('\n'); }
            left.Append(rows[i][0]);
            right.Append(rows[i].Length > 1 ? rows[i][1] : "");
        }
        names.text = left.ToString();
        values.text = right.ToString();
    }

    private string WeaponName(int index)
    {
        if (shooter != null && shooter.weapons != null && index >= 0 && index < shooter.weapons.Length && shooter.weapons[index] != null)
            return shooter.weapons[index].weaponName;
        return index >= 0 ? "Оружие " + (index + 1) : "—";
    }

    private static string LongTime(double seconds)
    {
        long total = (long)System.Math.Max(0d, seconds);
        return total >= 3600 ? total / 3600 + " ч " + (total % 3600 / 60).ToString("00") + " мин" :
            total / 60 + " мин " + (total % 60).ToString("00") + " с";
    }

    private static string Number(long value) { return value.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")); }

    private void RefreshStatistics()
    {
        MetaSave data = MetaProgress.Data;
        statsHeader.text = data.runs == 0 ? "Пока нет ни одного забега" :
            "Забегов: " + data.runs + "   •   " + MetaProgress.CurrencyName + ": " + MetaProgress.Currency;
        const string record = "  <color=#FFB060>★</color>";

        // За всё время.
        MetaRecord best = MetaProgress.Record("all");
        int runs = Mathf.Max(1, data.runs);
        var overall = new System.Collections.Generic.List<string[]>
        {
            new[] { "Забегов", data.runs.ToString() },
            new[] { "Побед", data.victories + (data.runs > 0 ? "  (" + Mathf.RoundToInt(100f * data.victories / runs) + "%)" : "") },
            new[] { "Время в бою", LongTime(data.totalSeconds) },
            new[] { "Всего убийств", Number(data.totalKills) },
            new[] { "Всего урона", Number(data.totalDamage) },
            new[] { MetaProgress.CurrencyName + " сейчас", Number(MetaProgress.Currency) },
            new[] { MetaProgress.CurrencyName + " заработано", Number(data.totalEarned) },
            new[] { "" },
            new[] { "Лучшее время", best != null ? FormatTime(best.bestTime) : "—" },
            new[] { "Больше всего убийств", best != null ? best.bestKills.ToString() : "—" },
            new[] { "Лучший уровень", best != null ? best.bestLevel.ToString() : "—" },
            new[] { "Больше всего урона", best != null && best.bestDamage > 0 ? Number(best.bestDamage) : "—" },
            new[] { "" },
            new[] { "Среднее время", data.runs > 0 ? FormatTime((float)(data.totalSeconds / runs)) : "—" },
            new[] { "Среднее убийств", data.runs > 0 ? (data.totalKills / (float)runs).ToString("0.#") : "—" },
            new[] { "Средний уровень", data.runs > 0 ? (data.totalLevels / (float)runs).ToString("0.#") : "—" },
        };
        FillColumns(statsNames[0], statsValues[0], overall);

        // Последний забег.
        var last = new System.Collections.Generic.List<string[]>();
        if (!data.hasLastRun)
        {
            last.Add(new[] { "Нет данных" });
            last.Add(new[] { "Сыграй забег, и он появится здесь" });
            lastRunConditions.text = "";
        }
        else
        {
            MetaLastRun run = data.lastRun;
            float dps = run.seconds > 0.5f ? run.damage / run.seconds : 0f;
            last.Add(new[] { "Итог", run.victory ? "<color=#40D9B8>Победа</color>" : "<color=#FF7352>Поражение</color>" });
            last.Add(new[] { "Дата", string.IsNullOrEmpty(run.date) ? "—" : run.date });
            last.Add(new[] { "Оружие", WeaponName(run.weapon) });
            last.Add(new[] { "" });
            last.Add(new[] { "Время", FormatTime(run.seconds) + (run.newBestTime ? record : "") });
            last.Add(new[] { "Убийства", run.kills + (run.newBestKills ? record : "") });
            last.Add(new[] { "Убийств в минуту", run.seconds > 1f ? (run.kills * 60f / run.seconds).ToString("0.#") : "—" });
            last.Add(new[] { "Нанесённый урон", Number(run.damage) });
            last.Add(new[] { "Урон в секунду", dps > 0f ? dps.ToString("0") : "—" });
            last.Add(new[] { "Волна", run.wave > 0 ? run.wave.ToString() : "—" });
            last.Add(new[] { "Уровень героя", run.level.ToString() });
            last.Add(new[] { "" });
            last.Add(new[] { "Награда", "+" + run.reward + " " + MetaProgress.CurrencyName.ToLower() +
                (run.multiplier > 1.001f ? "  (×" + run.multiplier.ToString("0.##") + ")" : "") });
            lastRunConditions.text = (string.IsNullOrEmpty(run.modifiers) ? "" : run.modifiers) +
                (run.newBestTime || run.newBestKills ? "\n<color=#FFB060>★</color> — новый рекорд" : "");
        }
        FillColumns(statsNames[1], statsValues[1], last);

        // Рекорды по оружию: забеги, лучшее время, убийства.
        var weapons = new System.Collections.Generic.List<string[]>();
        int count = shooter != null && shooter.weapons != null ? shooter.weapons.Length : 0;
        for (int i = 0; i < count; i++)
        {
            MetaRecord entry = MetaProgress.Record(MetaProgress.WeaponKey(i));
            if (entry == null || entry.runs == 0) continue;
            weapons.Add(new[] { WeaponName(i), entry.runs + " заб. · " + FormatTime(entry.bestTime) + " · " + entry.bestKills + " уб." +
                (entry.victories > 0 ? " · " + entry.victories + " поб." : "") });
        }
        if (weapons.Count == 0) weapons.Add(new[] { "Нет данных" });
        else weapons.Insert(0, new[] { "<color=#6B7785>оружие</color>", "<color=#6B7785>забеги · время · убийства</color>" });
        FillColumns(statsNames[2], statsValues[2], weapons);
    }

    public void ShowModifiers()
    {
        home.gameObject.SetActive(false);
        challengeScreen.Refresh();
        modifiersScreen.gameObject.SetActive(true);
    }

    // Экран «Испытания» (ChallengeScreen): карточки модификаторов забега и панель награды.
    private void BuildModifiers(RectTransform background)
    {
        modifiersScreen = GameUi.Box(background, "Modifiers", Vector2.zero, Vector2.one, Color.clear);
        challengeScreen = ChallengeScreen.Build(modifiersScreen, () =>
        {
            modifiersScreen.gameObject.SetActive(false);
            home.gameObject.SetActive(true);
        }, null);
        modifiersScreen.gameObject.SetActive(false);
    }

    public static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(Mathf.Max(0f, seconds));
        return total / 60 + ":" + (total % 60).ToString("00");
    }

    private WorkshopTree workshopTree;

    private void BuildWorkshop(RectTransform background)
    {
        workshop = GameUi.Box(background, "Workshop", Vector2.zero, Vector2.one, Color.clear);
        workshopTree = workshop.gameObject.AddComponent<WorkshopTree>();
        workshopTree.Build(shooter, () =>
        {
            workshop.gameObject.SetActive(false);
            RefreshWeaponLocks();
            home.gameObject.SetActive(true);
        });
        workshop.gameObject.SetActive(false);
    }

    private void RefreshWorkshop()
    {
        workshopTree.Refresh();
    }
    // Вызывается экраном выбора для открытого оружия.
    private void ChooseWeapon(int index)
    {
        if (!MetaProgress.IsWeaponUnlocked(index)) return;
        BeginGame(index);
    }

    private void RefreshWeaponLocks()
    {
        if (weaponScreen != null) weaponScreen.Refresh();
    }

    // Test arena: no waves or Guardian, every weapon available, enemies and weapons chosen in-game (see TestArena).
    public void BeginTestArena()
    {
        if (!waiting) return;
        int first = -1;
        for (int i = 0; shooter.weapons != null && i < shooter.weapons.Length; i++)
            if (shooter.weapons[i] != null && shooter.weapons[i].IsSelectable) { first = i; break; }
        if (first < 0) return;
        GameSession.TestMode = true;
        BeginGame(first);
    }

    private void BeginGame(int index)
    {
        if (!waiting || !(GameSession.TestMode || MetaProgress.IsWeaponUnlocked(index)) || !shooter.SelectWeapon(index)) return;
        waiting = false;
        GameAudio.SetMusic(true);
        GetComponent<PlayerProgression>().ApplyMetaBonuses();
        GetComponent<GameSession>().Begin(index);
        GetComponent<RunStatistics>().BeginRun();
        for (int i = 0; i < gameplayCanvases.Length; i++)
            if (gameplayCanvases[i] != null) gameplayCanvases[i].enabled = canvasStates[i];
        menu.gameObject.SetActive(false);
        Time.timeScale = 1f;
        // Tint the placeholder only. The operator has its own armor materials.
        if (ArenaLayout.Instance != null && !TryGetComponent<HeroLocomotion>(out _))
            ArenaLayout.Instance.Tint(gameObject, GameUi.Accent);
    }

    private void OnDestroy()
    {
        if (waiting) Time.timeScale = 1f;
        if (menu != null) Destroy(menu.gameObject);
    }
}
