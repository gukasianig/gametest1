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
    private Image bossFill;
    private RectTransform bossBar;
    private float uiTimer;
    private WaveManager waves;

    private void Awake()
    {
        Instance = this;
        waves = FindObjectOfType<WaveManager>();
        if (ArenaLayout.Instance == null) new GameObject("Industrial Arena").AddComponent<ArenaLayout>();
        if (GetComponent<ArenaEvents>() == null) gameObject.AddComponent<ArenaEvents>();
        BuildUI();
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
        survivalSeconds = TestMode ? 1000000f : RunModifiers.SurvivalSeconds(survivalSeconds);
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
    }

    private void Update()
    {
        if (State == GameRunState.Menu) return;
        if (Input.GetKeyDown(KeyCode.Escape) && !IsFinished) TogglePause();
        if (State == GameRunState.Playing)
        {
            Elapsed += Time.deltaTime;
            if (!BossWarning && Elapsed >= survivalSeconds - 10f) PrepareBoss();
            if (BossWarning && Boss == null && Elapsed >= survivalSeconds) SpawnBoss();
        }
        if (Time.unscaledTime < uiTimer) return;
        uiTimer = Time.unscaledTime + 0.1f;
        if (announceUntil > 0f && Time.unscaledTime >= announceUntil && !BossWarning && Boss == null)
        {
            announcement.text = "";
            announceUntil = 0f;
        }
        // The level-up screen has its own title in the same place: hide the timer while cards are open.
        clockPlate.gameObject.SetActive(State != GameRunState.Upgrade && !TestMode);
        int seconds = Mathf.FloorToInt(Mathf.Max(0f, survivalSeconds - Elapsed));
        clockCaption.text = Boss != null ? "СТРАЖ АРЕНЫ" : "ДО СТРАЖА";
        clock.text = Boss != null ? "Уничтожь!" : (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        // Последние 10 секунд до Стража — красные цифры.
        clock.color = Boss == null && seconds <= 10 ? new Color(1f, 0.4f, 0.3f) : Color.white;
        if (Boss != null && Boss.Health != null)
            bossFill.rectTransform.anchorMax = new Vector2((float)Boss.Health.CurrentHealth / Boss.Health.maxHealth, 1f);
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
    private float announcedAt = -10f;

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

    private void PrepareBoss()
    {
        BossWarning = true;
        if (waves != null) waves.StopSpawning();
        foreach (EnemyAI enemy in FindObjectsOfType<EnemyAI>()) Destroy(enemy.gameObject);
        announcement.text = "ПРИБЛИЖАЕТСЯ СТРАЖ\nСобери опыт и приготовься";
        announceUntil = 0f;
        announcedAt = Time.unscaledTime;
    }

    private void SpawnBoss()
    {
        Vector3 position;
        if (!ArenaLayout.Instance.TryBossSpawn(Camera.main, transform, out position)) return;
        Boss = BossController.Create(position, transform);
        Boss.Health.Died += BossDied;
        announcement.text = "";
        bossBar.gameObject.SetActive(true);
    }

    private void BossDied(Health health)
    {
        if (IsFinished) return;
        RewardExperience = 200;
        GetComponent<PlayerProgression>().AddExperience(RewardExperience, false);
        Finish(true);
    }

    public void Defeat() { if (!IsFinished) Finish(false); }

    private void Finish(bool victory)
    {
        State = victory ? GameRunState.Victory : GameRunState.Defeat;
        Time.timeScale = 0f;
        if (waves != null) waves.StopSpawning();
        GetComponent<PlayerProgression>().CloseChoices();
        RunStatistics stats = GetComponent<RunStatistics>();
        if (stats != null) stats.EndRun();
        pause.gameObject.SetActive(false);
        settings.gameObject.SetActive(false);
        bossBar.gameObject.SetActive(false);
        announcement.text = "";
        resultTitle.text = victory ? "АРЕНА ПРОЙДЕНА" : "ЗАБЕГ ЗАВЕРШЁН";
        resultTitle.color = victory ? GameUi.Accent : new Color(1f, 0.45f, 0.32f);
        int seconds = Mathf.FloorToInt(Elapsed);
        int kills = stats != null ? stats.Kills : 0;
        int level = GetComponent<PlayerProgression>().Level;
        RunReward reward = TestMode ? new RunReward() : MetaProgress.RecordRun(SelectedWeapon, victory, Elapsed, kills, level,
            stats != null ? stats.DamageDealt : 0, waves != null ? waves.wave : 0);
        resultBody.text = "Время  " + seconds / 60 + ":" + (seconds % 60).ToString("00") +
            (reward.newBestTime ? "  (новый рекорд!)" : "") +
            "\nУбийства  " + kills + (reward.newBestKills ? "  (новый рекорд!)" : "") +
            "\nУровень  " + level +
            "\n" + MetaProgress.CurrencyName + ": +" + reward.amount +
            (reward.multiplier > 1.001f ? "  (×" + reward.multiplier.ToString("0.##") + " за условия)" : "") +
            "  (всего " + MetaProgress.Currency + ")" +
            (victory ? "\nНаграда за Стража: +200 опыта" : "\nПотрать детали в Мастерской и попробуй снова");
        result.gameObject.SetActive(true);
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
        // Таймер: плашка под полосой опыта, крупные цифры и подпись.
        clockPlate = GameUi.Box(overlay.transform, "Clock", new Vector2(0.42f, 0.845f), new Vector2(0.58f, 0.925f),
            new Color(0.02f, 0.035f, 0.05f, 0.78f));
        UiArtwork.Round(clockPlate.GetComponent<Image>());
        clockCaption = GameUi.Label(clockPlate, "ДО СТРАЖА", new Vector2(0f, 0.62f), Vector2.one, 13, new Color(0.6f, 0.68f, 0.76f));
        clock = GameUi.Readable(GameUi.Label(clockPlate, "", new Vector2(0f, 0f), new Vector2(1f, 0.72f), 30));
        clock.fontStyle = FontStyle.Bold;
        announcement = GameUi.Readable(GameUi.Label(overlay.transform, "", new Vector2(0.1f, 0.66f), new Vector2(0.9f, 0.8f), 34,
            new Color(1f, 0.65f, 0.25f)), 2f);
        announcement.fontStyle = FontStyle.Bold;
        bossBar = GameUi.Box(overlay.transform, "Boss HP", new Vector2(0.2f, 0.77f), new Vector2(0.8f, 0.83f), GameUi.Background);
        bossFill = GameUi.Box(bossBar, "Fill", Vector2.zero, Vector2.one, new Color(0.65f, 0.17f, 0.1f)).GetComponent<Image>();
        GameUi.Label(bossBar, "СТРАЖ АРЕНЫ", Vector2.zero, Vector2.one, 20);
        bossBar.gameObject.SetActive(false);
        pause = GameUi.Box(overlay.transform, "Pause", Vector2.zero, Vector2.one, GameUi.Background, true);
        GameUi.Label(pause, "ПАУЗА", new Vector2(0.2f, 0.72f), new Vector2(0.8f, 0.88f), 44);
        GameUi.Button(pause, "Продолжить", new Vector2(0.33f, 0.58f), new Vector2(0.67f, 0.67f), TogglePause);
        // Заново: тот же забег с тем же оружием, текущий забег не засчитывается (как и выход в меню из паузы).
        GameUi.Button(pause, "Заново", new Vector2(0.33f, 0.46f), new Vector2(0.67f, 0.55f), () => Reload(true));
        GameUi.Button(pause, "Настройки", new Vector2(0.33f, 0.34f), new Vector2(0.67f, 0.43f), () => settings.gameObject.SetActive(true));
        GameUi.Button(pause, "В главное меню", new Vector2(0.33f, 0.22f), new Vector2(0.67f, 0.31f), () => Reload(false));
        settings = SettingsPanel.Build(overlay.transform, () => settings.gameObject.SetActive(false));
        pause.gameObject.SetActive(false);
        settings.gameObject.SetActive(false);
        result = GameUi.Box(overlay.transform, "Results", Vector2.zero, Vector2.one, GameUi.Background, true);
        resultTitle = GameUi.Label(result, "", new Vector2(0.1f, 0.73f), new Vector2(0.9f, 0.9f), 42);
        resultBody = GameUi.Label(result, "", new Vector2(0.15f, 0.34f), new Vector2(0.85f, 0.71f), 25);
        GameUi.Button(result, "Заново", new Vector2(0.18f, 0.14f), new Vector2(0.48f, 0.27f), () => Reload(true));
        GameUi.Button(result, "В главное меню", new Vector2(0.52f, 0.14f), new Vector2(0.82f, 0.27f), () => Reload(false));
        result.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (overlay != null) Destroy(overlay.gameObject);
    }
}
