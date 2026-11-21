using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameRunState { Menu, Playing, Upgrade, Paused, Victory, Defeat }

public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }
    public static int RestartWeapon { get; private set; } = -1;
    public GameRunState State { get; private set; } = GameRunState.Menu;
    public float Elapsed { get; private set; }
    [Min(30f)] public float survivalSeconds = 600f;
    public bool BossWarning { get; private set; }
    public BossController Boss { get; private set; }
    public int RewardExperience { get; private set; }
    public int SelectedWeapon { get; private set; }
    public bool IsFinished => State == GameRunState.Victory || State == GameRunState.Defeat;
    private GameRunState beforePause;
    private Canvas overlay;
    private RectTransform pause, settings, result;
    private Text clock, announcement, resultTitle, resultBody;
    // Полоса HP босса и мини-босса (19.11.2026: BossHealthBar вместо простой красной полосы).
    private BossHealthBar bossHud;
    // Мини-босс «Громила» на середине забега (MiniBoss): появился ли и когда повторить попытку появления.
    private bool miniSpawned;
    private float miniRetry;
    private float uiTimer;
    private WaveManager waves;

    private void Awake()
    {
        Instance = this;
        waves = FindObjectOfType<WaveManager>();
        if (ArenaLayout.Instance == null) new GameObject("Industrial Arena").AddComponent<ArenaLayout>();
        if (GetComponent<ArenaEvents>() == null) gameObject.AddComponent<ArenaEvents>();
        BuildUI();
        CombatFeel.Create();   // остановка кадра, вспышки, красная рамка, стрелки к элитам (11.11.2026)
    }

    public static int ConsumeRestart()
    {
        int index = RestartWeapon;
        RestartWeapon = -1;
        return index;
    }

    // Test arena (main menu): no waves, no Guardian, no rewards. Reset on every scene load.
    public static bool TestMode { get; set; }

    public void Begin(int weapon)
    {
        SelectedWeapon = weapon;
        survivalSeconds = TestMode || EndlessMode ? 1000000f : RunModifiers.SurvivalSeconds(survivalSeconds);
        EndlessRound = 0;
        cycleStart = 0f;
        endlessWarned = false;
        if (TestMode)
        {
            if (waves != null) waves.StopSpawning();
            TestArena arena = GetComponent<TestArena>();
            if (arena == null) arena = gameObject.AddComponent<TestArena>();
            arena.Build(weapon);
        }
        Elapsed = 0f;
        BossWarning = false;
        State = GameRunState.Playing;
        Time.timeScale = 1f;
        overlay.enabled = true;
        if (GetComponent<WorkshopExpedition>() == null) gameObject.AddComponent<WorkshopExpedition>();
        GetComponent<WorkshopExpedition>().BeginRun();
        FirstRunHints.TryShow(this);   // подсказки первого забега (B07)
    }

    private void Update()
    {
        if (State == GameRunState.Menu) return;
        if (Input.GetKeyDown(KeyCode.Escape) && !IsFinished) TogglePause();
        if (State == GameRunState.Playing)
        {
            Elapsed += Time.deltaTime;
            if (!BossWarning && Elapsed >= survivalSeconds - 10f) PrepareBoss();
            if (BossWarning && Boss == null && !bossDown && Elapsed >= survivalSeconds) SpawnBoss();
            if (EndlessMode && !TestMode) TickEndless();
            float miniAt = EndlessMode ? cycleStart + EndlessCycle * MiniBoss.SpawnShare : survivalSeconds * MiniBoss.SpawnShare;
            if (!TestMode && !miniSpawned && !BossWarning && waves != null && Elapsed >= miniAt && Time.unscaledTime >= miniRetry)
            {
                miniRetry = Time.unscaledTime + 1f;
                if (waves.SpawnMiniBoss() != null)
                {
                    miniSpawned = true;
                    Announce(GameText.Source("gamesession.117"), 4f);
                    GameAudio.Play(Sfx.Explosion, 0.5f);
                }
            }
        }
        if (Time.unscaledTime < uiTimer) return;
        uiTimer = Time.unscaledTime + 0.1f;
        if (announceUntil > 0f && Time.unscaledTime >= announceUntil && !BossWarning && Boss == null)
        {
            announcement.text = "";
            announceUntil = 0f;
        }
        // The level-up screen has its own title in the same place: hide the timer while cards are open.
        clockPlate.gameObject.SetActive(!IsFinished && State != GameRunState.Upgrade && !TestMode);
        RefreshClock();
        if (Boss != null && Boss.Health != null && !IsFinished)
            bossHud.Set(Boss.Title, Boss.Accent, Boss.Health.CurrentHealth, Boss.Health.maxHealth, Boss.Enraged);
        // Полоса мини-босса — та же, пока финального босса нет.
        MiniBoss mini = MiniBoss.Active;
        bool miniAlive = Boss == null && !IsFinished && mini != null && mini.Health != null && !mini.Health.isDead;
        if (miniAlive) bossHud.Set(MiniBoss.Title, MiniBoss.Accent, mini.Health.CurrentHealth, Mathf.Max(1, mini.Health.maxHealth), mini.Enraged);
        else if (Boss == null || IsFinished) bossHud.Hide();
    }

    private float announceUntil;

    // Short on-screen message (director events). The boss announcement keeps priority and is never cleared by this.
    public void Announce(string text, float seconds)
    {
        if (BossWarning || announcement == null) return;
        announcement.text = text;
        announceUntil = Time.unscaledTime + seconds;
        announcedAt = Time.unscaledTime;
    }

    private RectTransform clockPlate;
    private Text clockCaption;
    private Image clockProgress;
    private Outline clockBorder;
    private float announcedAt = -10f;

    private void RefreshClock()
    {
        float duration = Mathf.Max(1f, EndlessMode ? EndlessCycle : survivalSeconds);
        float passed = Mathf.Max(0f, Elapsed - (EndlessMode ? cycleStart : 0f));
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, duration - passed));
        bool fighting = Boss != null;
        bool urgent = fighting || seconds <= 10;
        bool near = seconds <= 30;
        Color accent = urgent ? new Color(1f, .4f, .3f) : near ? new Color(1f, .72f, .24f) : new Color(.16f, .72f, .9f);
        string caption = near ? GameText.Source("hud.boss-near") : GameText.Source("gamesession.120");
        clockCaption.text = fighting ? GameText.Source("hud.boss-arena") : EndlessMode ? GameText.Source("gamesession.118") + (EndlessRound + 1) + GameText.Source("gamesession.119") : caption;
        clock.text = fighting ? GameText.Source("hud.boss-fight") : (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        clock.color = urgent || near ? accent : Color.white;
        clockCaption.color = urgent || near ? accent : new Color(.72f, .83f, .88f);
        clockProgress.color = accent;
        clockProgress.rectTransform.anchorMax = new Vector2(fighting ? 1f : Mathf.Clamp01(passed / duration), 1f);
        clockProgress.gameObject.SetActive(fighting || passed > 0f);
        clockBorder.effectColor = new Color(accent.r, accent.g, accent.b, urgent ? .75f : .4f);
    }

    // Анонс «выпрыгивает»: короткий рост с перелётом и затухание в конце.
    private void LateUpdate()
    {
        if (announcement == null) return;
        float age = Time.unscaledTime - announcedAt;
        float pop = age < 0.25f ? Mathf.Lerp(0.6f, 1.12f, age / 0.25f) : age < 0.4f ? Mathf.Lerp(1.12f, 1f, (age - 0.25f) / 0.15f) : 1f;
        announcement.rectTransform.localScale = Vector3.one * pop;
        Color c = announcement.color;
        float left = announceUntil > 0f ? announceUntil - Time.unscaledTime : 1f;
        c.a = Mathf.Clamp01(left / 0.4f);
        announcement.color = c;
    }

    public bool EnterUpgrade()
    {
        if (State != GameRunState.Playing) return false;
        State = GameRunState.Upgrade;
        Time.timeScale = 0f;
        return true;
    }

    public void LeaveUpgrade()
    {
        if (State != GameRunState.Upgrade) return;
        State = GameRunState.Playing;
        Time.timeScale = 1f;
    }

    public void TogglePause()
    {
        if (IsFinished || State == GameRunState.Menu) return;
        if (State == GameRunState.Paused)
        {
            State = beforePause;
            Time.timeScale = State == GameRunState.Upgrade ? 0f : 1f;
            pause.gameObject.SetActive(false);
            settings.gameObject.SetActive(false);
            return;
        }
        beforePause = State;
        State = GameRunState.Paused;
        Time.timeScale = 0f;
        pause.gameObject.SetActive(true);
    }

    // ---------------- Бесконечный режим (12.11.2026) ----------------
    // Решения владельца: боссы по кругу (каждые 5 минут: на середине Громила, в конце случайный финальный босс),
    // с каждым кругом враги и боссы сильнее; забег кончается только поражением; открывается после первой победы.
    public static bool EndlessMode { get; set; }
    public const float EndlessCycle = 300f;
    public const float EndlessGrowth = 0.4f;
    public static int EndlessRound { get; private set; }
    // Множитель здоровья врагов и боссов в бесконечном режиме (1 в обычном).
    public static float EndlessFactor => EndlessMode ? 1f + EndlessGrowth * EndlessRound : 1f;
    private float cycleStart;
    private bool endlessWarned;
    private int endlessBossKills;

    private void TickEndless()
    {
        float t = Elapsed - cycleStart;
        if (!endlessWarned && t >= EndlessCycle - 10f)
        {
            endlessWarned = true;
            NextBoss = BossController.RandomKind();
            Announce(GameText.Source("gamesession.122") + BossController.Titles[(int)NextBoss] + "!", 4f);
        }
        if (endlessWarned && Boss == null && t >= EndlessCycle && Time.unscaledTime >= miniRetry)
        {
            miniRetry = Time.unscaledTime + 1f;
            SpawnBoss();
        }
    }

    // Босс круга побеждён: сундук и кристаллы, новый круг, враги сильнее.
    private void EndlessBossDown(Health health)
    {
        Vector3 at = health.transform.position;
        at.y = GroundHeight.At(at);
        SmallChest.Spawn(at);
        for (int i = 0; i < 16; i++)
        {
            float a = i * Mathf.PI / 8f;
            ExperiencePickup.Spawn(at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 3f, 20 + 10 * EndlessRound, merge: false);
        }
        EnemyFx.Explosion(at, 5f, 1f, true);
        if (CombatFeel.Instance != null) { CombatFeel.Instance.Flash(new Color(1f, 0.8f, 0.45f), 0.5f); CombatFeel.Instance.HitStop(0.15f, 0.05f); }
        endlessBossKills++;
        EndlessRound++;
        cycleStart = Elapsed;
        endlessWarned = false;
        miniSpawned = false;
        Boss = null;
        if (bossHud != null) bossHud.Hide();
        Announce(GameText.Source("gamesession.118") + EndlessRound + GameText.Source("gamesession.123") + EndlessFactor.ToString("0.0") + ")", 4f);
    }

    public int EndlessScore => Mathf.RoundToInt(Elapsed * 2f) + (GetComponent<RunStatistics>() != null ? GetComponent<RunStatistics>().Kills : 0) +
        endlessBossKills * 300;

    private void PrepareBoss()
    {
        BossWarning = true;
        // 12.11.2026 (просьба владельца): обычные враги продолжают появляться и не удаляются, пока идёт предупреждение и бой с боссом.
        NextBoss = BossController.RandomKind();   // случайный финальный босс (12.11.2026)
        announcement.text = GameText.Source("gamesession.124") + BossController.Titles[(int)NextBoss] + GameText.Source("gamesession.125");
        announceUntil = 0f;
        announcedAt = Time.unscaledTime;
    }

    private void SpawnBoss()
    {
        Vector3 position;
        if (!ArenaLayout.Instance.TryBossSpawn(Camera.main, transform, out position)) return;
        Boss = BossController.Create(position, transform, NextBoss);
        GetComponent<RunStatistics>()?.BeginBoss(NextBoss);
        Boss.Health.Died += BossDied;
        announcement.text = "";
    }

    // Тест-арена: вызвать Стража по кнопке.
    public void TestSpawnBoss() { if (TestMode && Boss == null) SpawnBoss(); }
    public void TestSpawnBoss(BossKind kind) { NextBoss = kind; TestSpawnBoss(); }

    // Какой финальный босс придёт (выбирается случайно при предупреждении за 10 с).
    public BossKind NextBoss { get; private set; }

    private bool bossDown;

    private void BossDied(Health health)
    {
        if (IsFinished || bossDown) return;
        if (Boss != null) WorkshopProjects.RecordBoss(2 + (int)Boss.Kind);
        GetComponent<RunStatistics>()?.EndBoss(true);
        if (TestMode)
        {
            // В тест-арене победа над Стражем не заканчивает забег.
            Boss = null;
            if (bossHud != null) bossHud.Hide();
            Announce(GameText.Source("gamesession.126"), 2f);
            return;
        }
        if (EndlessMode) { EndlessBossDown(health); return; }
        RewardExperience = 200;
        // Опыт за Стража — каждому герою (кооператив: у каждого свой уровень).
        foreach (PlayerHealth hero in CoopMode.Heroes)
            if (hero != null && hero.TryGetComponent(out PlayerProgression progression)) progression.AddExperience(RewardExperience, false);
        // Замедленная смерть Стража (CombatFeel), затем экран победы; второй Страж за это время не появится.
        bossDown = true;
        CombatFeel.BossKilled(health.transform.position, () => { if (!IsFinished) Finish(true); });
    }

    private PlayerHealth defeatedHero;

    public void Defeat(PlayerHealth hero = null)
    {
        if (IsFinished) return;
        defeatedHero = hero;
        Finish(false);
    }

    private void Finish(bool victory)
    {
        State = victory ? GameRunState.Victory : GameRunState.Defeat;
        Time.timeScale = 0f;
        if (waves != null) waves.StopSpawning();
        foreach (PlayerHealth hero in CoopMode.Heroes)
            if (hero != null && hero.TryGetComponent(out PlayerProgression progression)) progression.CloseChoices();
        RunStatistics stats = GetComponent<RunStatistics>();
        if (stats != null) stats.EndRun();
        if (!TestMode) MetaProgress.Save();
        if (stats != null && !victory && defeatedHero != null)
        {
            stats.Report.hasDeathCause = true;
            stats.Report.deathCause = defeatedHero.LastDamageCause;
        }
        pause.gameObject.SetActive(false);
        settings.gameObject.SetActive(false);
        bossHud.Hide();
        announcement.text = "";
        clockPlate.gameObject.SetActive(false);
        foreach (Canvas canvas in FindObjectsOfType<Canvas>())
            if (canvas.isRootCanvas && canvas != overlay && canvas.sortingOrder < overlay.sortingOrder) canvas.enabled = false;
        resultTitle.text = victory ? GameText.Source("gamesession.127") : GameText.Source("gamesession.128");
        resultTitle.color = victory ? GameUi.Accent : new Color(1f, 0.45f, 0.32f);
        int seconds = Mathf.FloorToInt(Elapsed);
        int kills = stats != null ? stats.Kills : 0;
        int level = GetComponent<PlayerProgression>().Level;
        if (EndlessMode && !TestMode)
        {
            // Бесконечный режим: свои рекорды (время, счёт, круг); детали — как за обычный забег плюс 50 за каждый круг.
            int score = EndlessScore;
            EndlessResult record = MetaProgress.RecordEndless(SelectedWeapon, Elapsed, kills, level, score, EndlessRound,
                stats != null ? stats.DamageDealt : 0, stats != null ? stats.Report : null, waves != null ? waves.wave : 0);
            resultTitle.text = GameText.Source("gamesession.129");
            resultTitle.color = new Color(1f, 0.7f, 0.3f);
            resultBody.text = GameText.Source("results.rounds") + EndlessRound + " · " + GameText.Source("results.score") + score +
                (record.newBestScore || record.newBestRound || record.newBestTime ? GameText.Source("gamesession.131") : "") +
                "    " + MetaProgress.CurrencyName + ": +" + record.reward + GameText.Source("gamesession.135") + MetaProgress.Currency + ")";
            AppendDeathSummary(victory);
            RunResultsView.Show(result, this, stats, GetComponent<PlayerProgression>(), waves);
            result.gameObject.SetActive(true);
            return;
        }
        RunReward reward = TestMode ? new RunReward() : MetaProgress.RecordRun(SelectedWeapon, victory, Elapsed, kills, level,
            stats != null ? stats.DamageDealt : 0, waves != null ? waves.wave : 0, stats != null ? stats.Report : null);
        resultBody.text = MetaProgress.CurrencyName + ": +" + reward.amount +
            (reward.multiplier > 1.001f ? "  (×" + reward.multiplier.ToString("0.##") + GameText.Source("gamesession.138") : "") +
            GameText.Source("gamesession.135") + MetaProgress.Currency + ")" +
            (reward.threat > 0 ? GameText.Source("gamesession.139") + reward.threat + (reward.newBestThreat ? GameText.Source("gamesession.140") : "") : "") +
            (reward.firstVictoryBonus > 0 ? GameText.Source("gamesession.141") + reward.firstVictoryBonus + GameText.Source("gamesession.142") : "") +
            (reward.trophy > 0 ? GameText.Source("cards.trophy.result") + reward.trophy + ")" : "") +
            (reward.newBestTime || reward.newBestKills ? GameText.Source("gamesession.131") : "");
        AppendDeathSummary(victory);
        RunResultsView.Show(result, this, stats, GetComponent<PlayerProgression>(), waves);
        result.gameObject.SetActive(true);
    }

    private void AppendDeathSummary(bool victory)
    {
        if (!victory && defeatedHero != null) resultBody.text += "\n" + defeatedHero.DeathSummary;
    }

    public void Reload(bool repeat)
    {
        RestartWeapon = repeat && !TestMode ? SelectedWeapon : -1;
        TestMode = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void BuildUI()
    {
        overlay = GameUi.Canvas("Session UI", 150);
        // Compact horizontal timer below XP; the lower rail shows progress toward the round boss.
        clockPlate = GameUi.Box(overlay.transform, "Clock", new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Color(.025f, .065f, .085f, .96f));
        clockPlate.pivot = new Vector2(.5f, 1f);
        clockPlate.sizeDelta = new Vector2(280f, 40f);
        clockPlate.anchoredPosition = new Vector2(0f, -62f);
        UiArtwork.Round(clockPlate.GetComponent<Image>());
        clockBorder = clockPlate.gameObject.AddComponent<Outline>();
        clockBorder.effectDistance = new Vector2(1f, -1f);
        clockCaption = GameUi.Label(clockPlate, GameText.Source("gamesession.120"), new Vector2(.04f, .18f), new Vector2(.65f, .96f), 13);
        clockCaption.alignment = TextAnchor.MiddleLeft;
        clockCaption.rectTransform.offsetMin = new Vector2(4f, 0f);
        clockCaption.rectTransform.offsetMax = new Vector2(-4f, 0f);
        clock = GameUi.Label(clockPlate, "", new Vector2(.65f, .18f), new Vector2(.97f, .96f), 24);
        clock.rectTransform.offsetMin = clock.rectTransform.offsetMax = Vector2.zero;
        clock.fontStyle = FontStyle.Bold;
        RectTransform clockTrack = GameUi.Box(clockPlate, "Boss approach", new Vector2(.055f, .08f), new Vector2(.945f, .145f), new Color(.12f, .18f, .22f));
        clockProgress = GameUi.Box(clockTrack, "Progress", Vector2.zero, Vector2.one, GameUi.Accent).GetComponent<Image>();
        announcement = GameUi.Readable(GameUi.Label(overlay.transform, "", new Vector2(0.1f, 0.66f), new Vector2(0.9f, 0.8f), 34,
            new Color(1f, 0.65f, 0.25f)), 2f);
        announcement.fontStyle = FontStyle.Bold;
        bossHud = BossHealthBar.Create(overlay.transform);
        bossHud.Hide();
        pause = GameUi.Box(overlay.transform, "Pause", Vector2.zero, Vector2.one, GameUi.Background, true);
        GameUi.Label(pause, GameText.Source("gamesession.146"), new Vector2(0.2f, 0.72f), new Vector2(0.8f, 0.88f), 44);
        GameUi.Button(pause, GameText.Source("gamesession.147"), new Vector2(0.33f, 0.58f), new Vector2(0.67f, 0.67f), TogglePause);
        // Заново: тот же забег с тем же оружием, текущий забег не засчитывается (как и выход в меню из паузы).
        GameUi.Button(pause, GameText.Source("gamesession.148"), new Vector2(0.33f, 0.46f), new Vector2(0.67f, 0.55f), () => Reload(true));
        GameUi.Button(pause, GameText.Source("gamesession.149"), new Vector2(0.33f, 0.34f), new Vector2(0.67f, 0.43f), () => settings.gameObject.SetActive(true));
        GameUi.Button(pause, GameText.Source("gamesession.150"), new Vector2(0.33f, 0.22f), new Vector2(0.67f, 0.31f), () => Reload(false));
        settings = SettingsPanel.Build(overlay.transform, () => settings.gameObject.SetActive(false));
        pause.gameObject.SetActive(false);
        settings.gameObject.SetActive(false);
        result = GameUi.Box(overlay.transform, "Results", Vector2.zero, Vector2.one, GameUi.Background, true);
        GameUi.Box(result, "Header accent", new Vector2(.05f, .885f), new Vector2(.09f, .892f), GameUi.Accent);
        resultTitle = GameUi.Label(result, "", new Vector2(.05f, .89f), new Vector2(.95f, .99f), 38);
        resultTitle.alignment = TextAnchor.MiddleLeft;
        resultTitle.fontStyle = FontStyle.Bold;
        resultBody = GameUi.Label(result, "", new Vector2(.05f, .12f), new Vector2(.95f, .24f), 17);
        GameUi.Button(result, GameText.Source("gamesession.148"), new Vector2(.25f, .025f), new Vector2(.49f, .105f), () => Reload(true));
        GameUi.Button(result, GameText.Source("gamesession.150"), new Vector2(.51f, .025f), new Vector2(.75f, .105f), () => Reload(false));
        result.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (overlay != null) Destroy(overlay.gameObject);
    }
}
