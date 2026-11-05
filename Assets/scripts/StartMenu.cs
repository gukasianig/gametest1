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
        Button start = GameUi.Button(home, "Старт", new Vector2(0.34f, 0.21f), new Vector2(0.66f, 0.33f), ShowWeapons);
        UiArtwork.Round(start.GetComponent<Image>());

        selection = GameUi.Box(background, "Weapon Selection", Vector2.zero, Vector2.one, Color.clear);
        GameUi.Label(selection, "СНАРЯЖЕНИЕ", new Vector2(0.08f, 0.87f), new Vector2(0.92f, 0.93f), 16, GameUi.Accent);
        GameUi.Label(selection, "Выбери оружие", new Vector2(0.08f, 0.77f), new Vector2(0.92f, 0.87f), 36,
            new Color(0.92f, 0.94f, 0.96f));
        GameUi.Label(selection, "Каждое оружие задаёт свой стиль боя. Нажми на карточку, чтобы начать.",
            new Vector2(0.06f, 0.7f), new Vector2(0.94f, 0.77f), 16, new Color(0.57f, 0.64f, 0.72f));
        int count = shooter.weapons == null ? 0 : shooter.weapons.Length;
        int rows = Mathf.Max(2, Mathf.CeilToInt(count / 2f));
        for (int i = 0; i < count; i++)
        {
            WeaponData weapon = shooter.weapons[i];
            if (weapon == null || weapon.bulletPrefab == null) continue;
            float x = i % 2 == 0 ? 0.06f : 0.52f;
            float top = 0.67f - (i / 2) * (0.52f / rows);
            WeaponCard(i, weapon, new Vector2(x, top - 0.46f / rows), new Vector2(x + 0.42f, top));
        }
        if (count == 0)
            GameUi.Label(selection, "Нет настроенного оружия", new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.6f), 24);
        Button back = GameUi.Button(selection, "Назад", new Vector2(0.41f, 0.065f), new Vector2(0.59f, 0.14f), () =>
        {
            selection.gameObject.SetActive(false);
            home.gameObject.SetActive(true);
        });
        UiArtwork.Round(back.GetComponent<Image>());
        selection.gameObject.SetActive(false);
        waiting = true;
        Time.timeScale = 0f;
        int restart = GameSession.ConsumeRestart();
        if (restart >= 0) BeginGame(restart);
    }

    public void ShowWeapons()
    {
        home.gameObject.SetActive(false);
        selection.gameObject.SetActive(true);
    }

    private void WeaponCard(int index, WeaponData weapon, Vector2 min, Vector2 max)
    {
        string[] titles = { "Пистолет", "Автомат", "Дробовик", "Тяжёлая винтовка" };
        string[] roles = { "ТОЧНОСТЬ", "ВЫСОКИЙ ТЕМП", "БЛИЖНИЙ БОЙ", "МОЩНЫЙ ВЫСТРЕЛ" };
        Color[] accents = { new Color(0.64f, 0.79f, 0.85f), GameUi.Accent,
            new Color(0.94f, 0.65f, 0.35f), new Color(0.73f, 0.64f, 0.92f) };
        Color accent = accents[index % accents.Length];
        RectTransform card = GameUi.Box(selection, "Weapon Card " + index, min, max, new Color(0.055f, 0.075f, 0.1f), true);
        UiArtwork.Round(card.GetComponent<Image>());
        Button button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = card.GetComponent<Image>();
        button.onClick.AddListener(() => BeginGame(index));
        Image art = UiArtwork.Picture(card, UiArtwork.Weapon(index), new Vector2(0.035f, 0.21f), new Vector2(0.43f, 0.86f));
        Text role = GameUi.Label(card, index < roles.Length ? roles[index] : "ОРУЖИЕ", new Vector2(0.46f, 0.73f),
            new Vector2(0.97f, 0.94f), 12, accent);
        role.alignment = TextAnchor.MiddleLeft;
        Text title = GameUi.Label(card, index < titles.Length ? titles[index] : weapon.weaponName,
            new Vector2(0.46f, 0.49f), new Vector2(0.98f, 0.77f), 27, new Color(0.93f, 0.95f, 0.97f));
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleLeft;
        string stats = "Урон  " + weapon.damage + "    •    " + (1f / Mathf.Max(0.03f, weapon.fireRate)).ToString("0.#") +
            " /с\nСнарядов за выстрел  " + Mathf.Max(1, weapon.pelletCount);
        Text detail = GameUi.Label(card, stats, new Vector2(0.46f, 0.12f), new Vector2(0.98f, 0.5f),
            16, new Color(0.62f, 0.69f, 0.77f));
        detail.alignment = TextAnchor.MiddleLeft;
        GameUi.Box(card, "Accent", new Vector2(0.04f, 0.965f), new Vector2(0.96f, 0.977f), new Color(accent.r, accent.g, accent.b, 0.5f));
        UpgradeCardFx effects = card.gameObject.AddComponent<UpgradeCardFx>();
        effects.icon = art.transform;
        effects.Present(accent, index);
    }

    private void BeginGame(int index)
    {
        if (!waiting || !shooter.SelectWeapon(index)) return;
        waiting = false;
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
