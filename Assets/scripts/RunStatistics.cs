using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class RunStatistics : MonoBehaviour
{
    public static RunStatistics Instance { get; private set; }
    public int Kills { get; private set; }
    public long DamageDealt { get; private set; }
    public float Elapsed { get; private set; }
    private bool started;
    private bool finished;
    private Canvas canvas;
    private GameObject panel;
    private Text body;
    private Text hint;
    private AutoShoter shooter;
    private PlayMovement movement;
    private PlayerProgression progression;
    private PlayerHealth health;
    private WaveManager waves;
    private float nextRefresh;
    public RunReport Report { get; private set; } = new RunReport();
    private RunBossReport activeBoss;
    private float bossStarted;

    public void RecordIncoming(int amount, HeroDamageCause cause)
    {
        if (!started || finished || amount <= 0) return;
        var entry = Report.incoming.Find(hit => hit.cause == cause);
        if (entry == null) { entry = new RunDamageReport { cause = cause }; Report.incoming.Add(entry); }
        entry.amount += amount;
    }

    public void BeginBoss(BossKind kind)
    {
        if (!started || finished) return;
        EndBoss(false);
        activeBoss = new RunBossReport { kind = kind };
        Report.bosses.Add(activeBoss);
        bossStarted = GameSession.Instance != null ? GameSession.Instance.Elapsed : Elapsed;
    }

    public void EndBoss(bool defeated)
    {
        if (activeBoss == null) return;
        activeBoss.seconds = Mathf.Max(0, (GameSession.Instance != null ? GameSession.Instance.Elapsed : Elapsed) - bossStarted);
        activeBoss.defeated = defeated;
        activeBoss = null;
    }
    private void Awake()
    {
        Instance = this;
        shooter = GetComponent<AutoShoter>();
        movement = GetComponent<PlayMovement>();
        progression = GetComponent<PlayerProgression>();
        health = GetComponent<PlayerHealth>();
        waves = FindObjectOfType<WaveManager>();
        BuildUI();
    }

    public void BeginRun()
    {
        Kills = 0;
        DamageDealt = 0;
        Elapsed = 0f;
        Report = new RunReport();
        activeBoss = null;
        started = true;
        finished = false;
        panel.SetActive(false);
    }

    public void EndRun()
    {
        EndBoss(false);
        finished = true;
        panel.SetActive(false);
        Report.heroes.Clear();
        Report.endless = GameSession.EndlessMode;
        Report.rounds = GameSession.EndlessRound;
        Report.score = GameSession.Instance != null ? GameSession.Instance.EndlessScore : 0;
        foreach (PlayerHealth hero in CoopMode.Heroes)
        {
            if (hero == null || !hero.TryGetComponent(out AutoShoter weapon) || !hero.TryGetComponent(out PlayerProgression progress)) continue;
            var build = new RunHeroReport { weapon = weapon.startWeaponIndex, evolved = weapon.Evolved };
            build.branch = WeaponElements.Equipped(build.weapon);
            build.branchRank = WeaponElements.Rank(build.weapon, build.branch);
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var entry in RunReport.Definitions(progress))
            {
                int rank = progress.Rank(entry.id);
                if (rank > 0 && seen.Add(entry.id)) build.upgrades.Add(new RunUpgradeReport { id = entry.id, rank = rank });
            }
            Report.heroes.Add(build);
        }
        Report.incoming.Sort((a, b) => b.amount.CompareTo(a.amount));
    }

    public void RecordDamage(int amount)
    {
        if (started && !finished && amount > 0) DamageDealt += amount;
    }

    public void RecordKill()
    {
        if (started && !finished)
        {
            Kills++;
            WorkshopProjects.RecordKill(GameSession.Instance != null ? GameSession.Instance.SelectedWeapon : -1);
        }
    }

    private void Update()
    {
        if (!started || finished) return;
        if (!finished) Elapsed = GameSession.Instance != null ? GameSession.Instance.Elapsed : Elapsed + Time.deltaTime;
        // Do not cover level-up choices. Tab never changes the game's time scale.
        if (!finished && Time.timeScale <= 0f)
        {
            panel.SetActive(false);
            return;
        }
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            panel.SetActive(!panel.activeSelf);
            nextRefresh = 0f;
        }
        if (!panel.activeSelf || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.2f;
        Refresh();
    }

    private void Refresh()
    {
        int seconds = Mathf.FloorToInt(Elapsed);
        StringBuilder text = new StringBuilder(600);
        text.Append(GameText.Source("runstatistics.281")).Append(seconds / 60).Append(':')
            .Append((seconds % 60).ToString("00"));
        text.Append(GameText.Source("runstatistics.282")).Append(Kills);
        text.Append(GameText.Source("runstatistics.283")).Append(DamageDealt);
        if (waves != null) text.Append(GameText.Source("runstatistics.284")).Append(waves.wave);
        if (progression != null) text.Append(GameText.Source("runstatistics.285")).Append(progression.Level);
        if (progression != null) text.Append(GameText.Source("runstatistics.286"))
            .Append(progression.AttractionRadius.ToString("0.##")).Append(GameText.Source("runstatistics.287"))
            .Append(progression.PickupRadiusMultiplier.ToString("0.###")).Append(')');
        if (health != null) text.Append(GameText.Source("runstatistics.288")).Append(health.CurrentHP).Append(" / ").Append(health.maxHP);
        if (movement != null) text.Append(GameText.Source("runstatistics.289"))
            .Append(movement.EffectiveSpeed.ToString("0.##")).Append(GameText.Source("runstatistics.290"))
            .Append(movement.SpeedMultiplier.ToString("0.###")).Append(')');
        if (shooter != null && shooter.baseWeapon != null)
        {
            WeaponData weapon = shooter.baseWeapon;
            text.Append(GameText.Source("runstatistics.291")).Append(WeaponSelectScreen.Title(weapon, shooter.startWeaponIndex)).Append("</color>");
            float damageMultiplier = progression != null ? progression.DamageMultiplier : 1f;
            text.Append(GameText.Source("runstatistics.292")).Append(shooter.ExactDamage.ToString("0.##"))
                .Append("  (×").Append(damageMultiplier.ToString("0.###")).Append(')');
            float attackMultiplier = progression != null ? progression.AttackSpeedMultiplier : 1f;
            text.Append(GameText.Source("runstatistics.293"))
                .Append((1f / Mathf.Max(0.03f, shooter.CurrentFireInterval)).ToString("0.###"))
                .Append("  (×").Append(attackMultiplier.ToString("0.###")).Append(')');
            text.Append(GameText.Source("runstatistics.294")).Append(shooter.CurrentPelletCount);
            text.Append(GameText.Source("runstatistics.295")).Append(shooter.range.ToString("0.#")).Append(GameText.Source("arenaevents.040"));
            text.Append(GameText.Source("runstatistics.296")).Append(weapon.bulletSpeed.ToString("0.#")).Append(GameText.Source("runstatistics.297"));
            text.Append(GameText.Source("runstatistics.298")).Append(shooter.CurrentSpread.ToString("0.#")).Append('°');
            AppendEvolution(text);
        }
        body.text = text.ToString();
        hint.text = finished ? GameText.Source("runstatistics.299") : GameText.Source("runstatistics.300");
    }

    // Статус эволюции оружия (21.11.2026): готовая эволюция или прогресс рецепта «Урон пуль» + парная карточка, затем сундук.
    private void AppendEvolution(StringBuilder text)
    {
        int slot = shooter.EvolutionSlot;
        if (slot < 0) return;
        AutoShoter.EvolutionData evo = AutoShoter.Evolutions[slot];
        if (shooter.Evolved)
        {
            text.Append(GameText.Source("runstatistics.evo.done")).Append(evo.title).Append("</color>");
            return;
        }
        if (progression == null) return;
        int damage = progression.EffectRanks(UpgradeEffect.Damage), partner = progression.EffectRanks(evo.partner);
        bool ready = damage >= PlayerProgression.EvolutionDamageRanks && partner >= evo.partnerRanks;
        text.Append(GameText.Source("runstatistics.evo.next")).Append(evo.title).Append("</color>: ");
        AppendRecipePart(text, progression.EffectTitle(UpgradeEffect.Damage), damage, PlayerProgression.EvolutionDamageRanks);
        text.Append(", ");
        AppendRecipePart(text, progression.EffectTitle(evo.partner), partner, evo.partnerRanks);
        text.Append(GameText.Source(ready ? "runstatistics.evo.ready" : "runstatistics.evo.chest"));
    }

    private static void AppendRecipePart(StringBuilder text, string title, int have, int need)
    {
        text.Append(have >= need ? "<color=#7BE08A>" : "<color=#C8D2DE>").Append(title).Append(' ')
            .Append(Mathf.Min(have, need)).Append('/').Append(need).Append("</color>");
    }

    private Text Label(Transform parent, string name, Vector2 min, Vector2 max, int size, Color color)
    {
        Text label = new GameObject(name, typeof(RectTransform), typeof(LocalizedUiText)).GetComponent<Text>();
        label.transform.SetParent(parent, false);
        label.rectTransform.anchorMin = min;
        label.rectTransform.anchorMax = max;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        label.font = GameUi.DefaultFont;
        label.fontSize = size;
        label.color = color;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 12;
        label.resizeTextMaxSize = size;
        label.raycastTarget = false;
        return label;
    }

    private void BuildUI()
    {
        canvas = new GameObject("Run Statistics", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        panel = new GameObject("Statistics Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.18f, 0.08f);
        rect.anchorMax = new Vector2(0.82f, 0.89f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.025f, 0.045f, 0.065f, 0.96f);
        panel.GetComponent<Image>().raycastTarget = false;
        Text title = Label(panel.transform, "Title", new Vector2(0.06f, 0.87f), new Vector2(0.94f, 0.98f),
            30, new Color(0.4f, 0.9f, 0.83f));
        title.text = GameText.Source("runstatistics.301");
        title.alignment = TextAnchor.MiddleLeft;
        body = Label(panel.transform, "Stats", new Vector2(0.06f, 0.1f), new Vector2(0.94f, 0.86f), 22, Color.white);
        body.alignment = TextAnchor.UpperLeft;
        hint = Label(panel.transform, "Hint", new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.09f), 16, Color.gray);
        hint.alignment = TextAnchor.MiddleLeft;
        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (canvas != null) Destroy(canvas.gameObject);
    }
}
