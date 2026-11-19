using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Тестовая арена из главного меню: волн и Стража нет, всё проверяется вручную.
// Одна компактная панель справа (F1 — скрыть/показать) с вкладками: оружие, враги (+элита/орда/Страж),
// все события арены, улучшения (любые карточки и награды сундука), прочее (дроп, уровень, награда сундука).
// Внизу панели — постоянные кнопки: количество за клик, очистка, бессмертие, лечение, время, герой в центр.
[DefaultExecutionOrder(900)]
public class TestArena : MonoBehaviour
{
    private enum Tab { Weapons, Enemies, Events, Upgrades, Extra }
    private static readonly string[] TabNames = { GameText.Source("startmenu.347"), GameText.Source("testarena.363"), GameText.Source("testarena.364"), GameText.Source("testarena.365"), GameText.Source("testarena.366") };
    private const int Columns = 2, Rows = 9;
    private static readonly int[] Amounts = { 1, 5, 10, 25 };
    private static readonly float[] TimeScales = { 0.25f, 0.5f, 1f, 2f, 3f };

    private struct Item
    {
        public string label;
        public Color color;
        public Action click;
        public bool selected;
    }

    private AutoShoter shooter;
    private WaveManager waves;
    private PlayerHealth health;
    private Canvas canvas;
    private RectTransform root, content;
    private Text statusLabel, pageLabel, amountLabel, immortalLabel, timeLabel, wavesLabel;
    private readonly Button[] tabButtons = new Button[TabNames.Length];
    private readonly int[] pages = new int[TabNames.Length];
    private Tab tab = Tab.Weapons;
    private int amountIndex, timeIndex = 2, currentWeapon;
    private bool immortal;   // по умолчанию выкл: иначе метеориты, огонь и лужи не наносят урон, и события не похожи на игру
    private readonly List<int> weaponIndexes = new List<int>();

    public void Build(int startWeapon)
    {
        shooter = GetComponent<AutoShoter>();
        health = GetComponent<PlayerHealth>();
        waves = FindObjectOfType<WaveManager>();
        currentWeapon = startWeapon;
        canvas = GameUi.Canvas("Test Arena UI", 120);

        weaponIndexes.Clear();
        if (shooter != null && shooter.weapons != null)
            for (int i = 0; i < shooter.weapons.Length; i++)
                if (shooter.weapons[i] != null && shooter.weapons[i].IsSelectable) weaponIndexes.Add(i);

        Color panel = new Color(0.025f, 0.045f, 0.065f, 0.86f);
        root = GameUi.Box(canvas.transform, "Test Panel", new Vector2(0.805f, 0.03f), new Vector2(0.995f, 0.97f), panel, true);
        UiArtwork.Round(root.GetComponent<Image>());
        Text title = GameUi.Label(root, GameText.Source("testarena.367"), new Vector2(0f, 0.955f), new Vector2(1f, 1f), 14, new Color(1f, 0.65f, 0.25f));
        title.resizeTextForBestFit = true;

        float tabWidth = 1f / TabNames.Length;
        for (int i = 0; i < TabNames.Length; i++)
        {
            Tab value = (Tab)i;
            Button button = GameUi.Button(root, TabNames[i], new Vector2(i * tabWidth + 0.004f, 0.91f), new Vector2((i + 1) * tabWidth - 0.004f, 0.953f),
                () => { tab = value; Refill(); });
            Small(button, 11);
            tabButtons[i] = button;
        }

        content = GameUi.Box(root, "Content", new Vector2(0f, 0.255f), new Vector2(1f, 0.905f), Color.clear);
        Button previous = GameUi.Button(root, "◄", new Vector2(0.03f, 0.205f), new Vector2(0.28f, 0.25f), () => { pages[(int)tab]--; Refill(); });
        Button next = GameUi.Button(root, "►", new Vector2(0.72f, 0.205f), new Vector2(0.97f, 0.25f), () => { pages[(int)tab]++; Refill(); });
        Small(previous, 14);
        Small(next, 14);
        pageLabel = GameUi.Label(root, "", new Vector2(0.28f, 0.205f), new Vector2(0.72f, 0.25f), 13, new Color(0.7f, 0.77f, 0.85f));

        // Постоянные кнопки: 2 колонки × 3 ряда.
        Button amount = Control(0, 0, "", () => { amountIndex = (amountIndex + 1) % Amounts.Length; RefreshLabels(); });
        amountLabel = amount.GetComponentInChildren<Text>();
        Control(1, 0, GameText.Source("testarena.368"), ClearEnemies);
        Button immortalButton = Control(0, 1, "", () => { immortal = !immortal; RefreshLabels(); });
        immortalLabel = immortalButton.GetComponentInChildren<Text>();
        Control(1, 1, GameText.Source("testarena.369"), () => { if (health != null) health.Heal(health.maxHP); });
        Button time = Control(0, 2, "", () => { timeIndex = (timeIndex + 1) % TimeScales.Length; RefreshLabels(); });
        timeLabel = time.GetComponentInChildren<Text>();
        Control(1, 2, GameText.Source("testarena.370"), () =>
        {
            transform.position = new Vector3(0f, GroundHeight.At(Vector3.zero) + 0.1f, 0f);
            Say(GameText.Source("testarena.371"));
        });
        Button wavesButton = Control(0, 3, "", () =>
        {
            if (waves == null) return;
            if (waves.Suspended) waves.ResumeSpawning(); else waves.StopSpawning();
            RefreshLabels();
        });
        wavesLabel = wavesButton.GetComponentInChildren<Text>();
        Control(1, 3, GameText.Source("testarena.372"), StopEvent);

        // Keep diagnostic messages above both heroes' ability panels and their hover help.
        RectTransform statusPanel = GameUi.Box(canvas.transform, "Test status", new Vector2(0.3f, 0.38f),
            new Vector2(0.78f, 0.43f), new Color(0.025f, 0.045f, 0.065f, 0.86f));
        UiArtwork.Round(statusPanel.GetComponent<Image>());
        statusLabel = GameUi.Label(statusPanel, "", Vector2.zero, Vector2.one, 16, new Color(0.8f, 0.86f, 0.92f));
        statusPanel.gameObject.SetActive(false);
        Refill();
        RefreshLabels();
    }

    private Button Control(int column, int row, string text, UnityEngine.Events.UnityAction action)
    {
        float top = 0.195f - row * 0.049f;
        Button button = GameUi.Button(root, text, new Vector2(0.03f + column * 0.485f, top - 0.045f), new Vector2(0.485f + column * 0.485f, top), action);
        Small(button, 12);
        return button;
    }

    private static void Small(Button button, int size)
    {
        Text label = button.GetComponentInChildren<Text>();
        label.fontSize = size;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 8;
        label.resizeTextMaxSize = size;
        UiArtwork.Round(button.GetComponent<Image>());
    }

    private void Say(string text)
    {
        if (statusLabel == null) return;
        statusLabel.text = text;
        statusLabel.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    // ---------- Содержимое вкладок ----------

    private List<Item> Items()
    {
        var items = new List<Item>();
        switch (tab)
        {
            case Tab.Weapons:
                foreach (int index in weaponIndexes)
                {
                    int captured = index;
                    WeaponData weapon = shooter.weapons[index];
                    items.Add(new Item
                    {
                        label = WeaponSelectScreen.Title(weapon, index),
                        color = new Color(0.08f, 0.27f, 0.31f),
                        selected = index == currentWeapon,
                        click = () => PickWeapon(captured)
                    });
                }
                break;
            case Tab.Enemies:
                if (waves != null && waves.enemies != null)
                    for (int i = 0; i < waves.enemies.Length; i++)
                    {
                        EnemyType type = waves.enemies[i];
                        if (type == null || type.prefab == null) continue;
                        int captured = i;
                        items.Add(new Item { label = GameText.EnemyName(string.IsNullOrEmpty(type.name) ? type.prefab.name : type.name), color = new Color(0.31f, 0.14f, 0.12f), click = () => SpawnEnemy(captured) });
                    }
                items.Add(new Item { label = GameText.Source("testarena.373"), color = new Color(0.5f, 0.3f, 0.08f), click = SpawnElite });
                items.Add(new Item { label = GameText.Source("testarena.374"), color = new Color(0.5f, 0.3f, 0.08f), click = SpawnHorde });
                items.Add(new Item { label = GameText.Source("testarena.375"), color = new Color(0.5f, 0.18f, 0.1f), click = SpawnMiniBoss });
                items.Add(new Item { label = GameText.Source("testarena.376"), color = new Color(0.45f, 0.1f, 0.3f), click = () => SpawnBoss(BossKind.Guardian) });
                items.Add(new Item { label = GameText.Source("testarena.377"), color = new Color(0.4f, 0.12f, 0.45f), click = () => SpawnBoss(BossKind.SwarmQueen) });
                items.Add(new Item { label = GameText.Source("testarena.378"), color = new Color(0.15f, 0.25f, 0.45f), click = () => SpawnBoss(BossKind.StormSniper) });
                break;
            case Tab.Events:
                foreach (ArenaEventKind kind in Enum.GetValues(typeof(ArenaEventKind)))
                {
                    ArenaEventKind captured = kind;
                    Color tone = ArenaEvents.Tone(kind);
                    items.Add(new Item { label = ArenaEvents.Title(kind), color = new Color(tone.r * 0.35f, tone.g * 0.35f, tone.b * 0.35f), click = () => StartEvent(captured) });
                }
                items.Add(new Item { label = GameText.Source("testarena.379"), color = new Color(0.3f, 0.12f, 0.12f), click = StopEvent });
                break;
            case Tab.Upgrades:
                PlayerProgression progression = PlayerProgression.Instance;
                if (progression != null && progression.catalog != null)
                    foreach (UpgradeDefinition definition in progression.catalog.entries) items.Add(UpgradeItem(progression, definition));
                if (progression != null)
                {
                    foreach (UpgradeDefinition definition in PlayerProgression.ChestUpgrades) items.Add(UpgradeItem(progression, definition));
                    // Эволюция текущего оружия (в бою — только из сундука): в тест-арене выдаётся сразу.
                    AutoShoter gun = progression.GetComponent<AutoShoter>();
                    if (gun != null && gun.CanEvolve) items.Add(UpgradeItem(progression, PlayerProgression.EvolutionUpgrades[gun.EvolutionSlot]));
                }
                break;
            default:
                items.Add(new Item { label = GameText.Source("testarena.380"), color = new Color(0.12f, 0.4f, 0.22f), click = () => DropNear(PickupKind.Health) });
                items.Add(new Item { label = GameText.Source("testarena.381"), color = new Color(0.12f, 0.3f, 0.45f), click = () => DropNear(PickupKind.Magnet) });
                items.Add(new Item { label = GameText.Source("testarena.382"), color = new Color(0.1f, 0.35f, 0.5f), click = SpawnCrystals });
                items.Add(new Item { label = GameText.Source("testarena.383"), color = new Color(0.1f, 0.4f, 0.4f), click = LevelUp });
                items.Add(new Item { label = GameText.Source("testarena.384"), color = new Color(0.4f, 0.2f, 0.5f), click = ChestReward });
                items.Add(new Item { label = GameText.Source("testarena.385"), color = new Color(0.4f, 0.15f, 0.15f), click = () => { if (health != null) health.TakeDamage(30); } });
                break;
        }
        return items;
    }

    private Item UpgradeItem(PlayerProgression progression, UpgradeDefinition definition)
    {
        int rank = progression.Rank(definition.id);
        return new Item
        {
            label = definition.title + "  " + rank + "/" + definition.maxRanks,
            color = UpgradeCatalog.RarityColor(definition.rarity) * 0.45f,
            selected = rank >= definition.maxRanks,
            click = () => GrantUpgrade(definition)
        };
    }

    private void Refill()
    {
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        for (int i = 0; i < tabButtons.Length; i++)
            tabButtons[i].GetComponent<Image>().color = i == (int)tab ? new Color(0.12f, 0.5f, 0.45f) : new Color(0.08f, 0.2f, 0.25f);

        List<Item> items = Items();
        int perPage = Columns * Rows;
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(items.Count / (float)perPage));
        int page = ((pages[(int)tab] % pageCount) + pageCount) % pageCount;
        pages[(int)tab] = page;
        pageLabel.text = (page + 1) + " / " + pageCount;

        float rowHeight = 1f / Rows;
        for (int n = 0; n < perPage; n++)
        {
            int index = page * perPage + n;
            if (index >= items.Count) break;
            Item item = items[index];
            int column = n % Columns, row = n / Columns;
            float top = 1f - row * rowHeight;
            float left = column / (float)Columns;
            Button button = GameUi.Button(content, item.label, new Vector2(left + 0.012f, top - rowHeight + 0.006f), new Vector2(left + 1f / Columns - 0.012f, top - 0.006f),
                () => { item.click(); });
            button.GetComponent<Image>().color = item.selected ? Color.Lerp(item.color, new Color(0.2f, 0.8f, 0.6f), 0.45f) : item.color;
            Small(button, 12);
        }
    }

    // ---------- Действия ----------

    private void PickWeapon(int index)
    {
        if (shooter == null || !shooter.SelectWeapon(index)) return;
        currentWeapon = index;
        WeaponData weapon = shooter.weapons[index];
        Say(GameText.Source("testarena.386") + WeaponSelectScreen.Title(weapon, index));
        Refill();
    }

    private void SpawnEnemy(int typeIndex)
    {
        if (waves == null) return;
        int made = waves.SpawnTest(typeIndex, Amounts[amountIndex], transform.position);
        Say(GameText.Source("testarena.387") + made + " × " + GameText.EnemyName(waves.enemies[typeIndex].name) + GameText.Source("testarena.388") + EnemyAI.ActiveCount);
    }

    private void SpawnElite()
    {
        if (waves == null) return;
        int made = 0;
        for (int i = 0; i < Amounts[amountIndex]; i++) if (waves.SpawnElite() != null) made++;
        Say(GameText.Source("testarena.389") + made);
    }

    private void SpawnHorde()
    {
        if (waves == null) return;
        waves.StartCoroutine(waves.HordeEvent(Mathf.Max(8, Amounts[amountIndex] * 3)));
        Say(GameText.Source("testarena.390"));
    }

    private void SpawnMiniBoss()
    {
        WaveManager spawner = FindObjectOfType<WaveManager>();
        GameObject brute = spawner != null ? spawner.SpawnMiniBoss() : null;
        Say(brute != null ? GameText.Source("testarena.391") : GameText.Source("testarena.392"));
    }

    private void SpawnBoss(BossKind kind)
    {
        GameSession session = GameSession.Instance;
        if (session == null) return;
        session.TestSpawnBoss(kind);
        Say(session.Boss != null ? BossController.Titles[(int)kind] + GameText.Source("testarena.393") : GameText.Source("testarena.394"));
    }

    private void StartEvent(ArenaEventKind kind)
    {
        ArenaEvents events = ArenaEvents.Instance;
        if (events == null) { Say(GameText.Source("testarena.395")); return; }
        events.Begin(kind);
        Say(GameText.Source("testarena.396") + ArenaEvents.Title(kind));
    }

    private void StopEvent()
    {
        if (ArenaEvents.Instance != null) ArenaEvents.Instance.End(false);
        Say(GameText.Source("testarena.397"));
    }

    private void GrantUpgrade(UpgradeDefinition definition)
    {
        PlayerProgression progression = PlayerProgression.Instance;
        if (progression == null) return;
        Say(progression.TestGrant(definition) ? GameText.Source("testarena.398") + definition.title : GameText.Source("testarena.399"));
        Refill();
    }

    private Vector3 SpotNear(float distance)
    {
        Vector2 around = UnityEngine.Random.insideUnitCircle.normalized * distance;
        Vector3 spot = transform.position + new Vector3(around.x, 0f, around.y);
        spot.y = GroundHeight.At(spot);
        return spot;
    }

    private void DropNear(PickupKind kind)
    {
        DropPickup.Spawn(SpotNear(3f), kind);
        Say(kind == PickupKind.Health ? GameText.Source("testarena.400") : GameText.Source("testarena.401"));
    }

    private void SpawnCrystals()
    {
        for (int i = 0; i < 10; i++) ExperiencePickup.Spawn(SpotNear(UnityEngine.Random.Range(2f, 4f)), 5);
        Say(GameText.Source("testarena.402"));
    }

    private void LevelUp()
    {
        PlayerProgression progression = PlayerProgression.Instance;
        if (progression == null) return;
        progression.TestLevelUp();
        Say(GameText.Source("testarena.403"));
    }

    private void ChestReward()
    {
        PlayerProgression progression = PlayerProgression.Instance;
        Say(progression != null && progression.OfferChestReward() ? GameText.Source("testarena.384") : GameText.Source("testarena.404"));
    }

    private void ClearEnemies()
    {
        foreach (EnemyAI enemy in FindObjectsOfType<EnemyAI>()) Destroy(enemy.gameObject);
        GameSession session = GameSession.Instance;
        if (session != null && session.Boss != null && session.Boss.Health != null) session.Boss.Health.TakeDamage(int.MaxValue / 2);
        Say(GameText.Source("testarena.405"));
    }

    private void RefreshLabels()
    {
        amountLabel.text = GameText.Source("testarena.406") + Amounts[amountIndex];
        immortalLabel.text = immortal ? GameText.Source("testarena.407") : GameText.Source("testarena.408");
        timeLabel.text = GameText.Source("testarena.409") + TimeScales[timeIndex].ToString("0.##");
        if (wavesLabel != null && waves != null) wavesLabel.text = waves.Suspended ? GameText.Source("testarena.410") : GameText.Source("testarena.411");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1) && root != null) root.gameObject.SetActive(!root.gameObject.activeSelf);
        if (immortal && health != null && !health.IsDead) health.GrantInvulnerability(0.5f);
        GameSession session = GameSession.Instance;
        if (canvas != null) canvas.enabled = session != null && session.State == GameRunState.Playing;
        // Замедление/ускорение времени работает только в бою: пауза и карточки сами ставят 0 и 1.
        if (session != null && session.State == GameRunState.Playing && !Mathf.Approximately(Time.timeScale, TimeScales[timeIndex]))
            Time.timeScale = TimeScales[timeIndex];
    }

    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }
}
