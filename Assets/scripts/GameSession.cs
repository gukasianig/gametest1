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
        if (Application.targetFrameRate < 0) Application.targetFrameRate = 60;
        waves = FindObjectOfType<WaveManager>();
        if (ArenaLayout.Instance == null) new GameObject("Industrial Arena").AddComponent<ArenaLayout>();
        BuildUI();
    }

    public static int ConsumeRestart()
    {
        int index = RestartWeapon;
        RestartWeapon = -1;
        return index;
    }

    public void Begin(int weapon)
    {
        SelectedWeapon = weapon;
        survivalSeconds = RunModifiers.SurvivalSeconds(survivalSeconds);
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
        int seconds = Mathf.FloorToInt(Mathf.Max(0f, survivalSeconds - Elapsed));
        clock.text = Boss != null ? "Уничтожь Стража арены" :
            "До Стража  " + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00") + "   •   Esc — пауза";
        if (Boss != null && Boss.Health != null)
            bossFill.rectTransform.anchorMax = new Vector2((float)Boss.Health.CurrentHealth / Boss.Health.maxHealth, 1f);
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
        RunReward reward = MetaProgress.RecordRun(SelectedWeapon, victory, Elapsed, kills, level);
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
        RestartWeapon = repeat ? SelectedWeapon : -1;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void BuildUI()
    {
        overlay = GameUi.Canvas("Session UI", 150);
        clock = GameUi.Label(overlay.transform, "", new Vector2(0.2f, 0.84f), new Vector2(0.8f, 0.9f), 20);
        announcement = GameUi.Label(overlay.transform, "", new Vector2(0.1f, 0.65f), new Vector2(0.9f, 0.82f), 32,
            new Color(1f, 0.65f, 0.25f));
        bossBar = GameUi.Box(overlay.transform, "Boss HP", new Vector2(0.2f, 0.77f), new Vector2(0.8f, 0.83f), GameUi.Background);
        bossFill = GameUi.Box(bossBar, "Fill", Vector2.zero, Vector2.one, new Color(0.65f, 0.17f, 0.1f)).GetComponent<Image>();
        GameUi.Label(bossBar, "СТРАЖ АРЕНЫ", Vector2.zero, Vector2.one, 20);
        bossBar.gameObject.SetActive(false);
        pause = GameUi.Box(overlay.transform, "Pause", Vector2.zero, Vector2.one, GameUi.Background, true);
        GameUi.Label(pause, "ПАУЗА", new Vector2(0.2f, 0.72f), new Vector2(0.8f, 0.88f), 44);
        GameUi.Button(pause, "Продолжить", new Vector2(0.33f, 0.53f), new Vector2(0.67f, 0.64f), TogglePause);
        GameUi.Button(pause, "Настройки", new Vector2(0.33f, 0.37f), new Vector2(0.67f, 0.48f), () => settings.gameObject.SetActive(true));
        GameUi.Button(pause, "В главное меню", new Vector2(0.33f, 0.21f), new Vector2(0.67f, 0.32f), () => Reload(false));
        settings = GameUi.Box(overlay.transform, "Settings", Vector2.zero, Vector2.one, GameUi.Background, true);
        GameUi.Label(settings, "НАСТРОЙКИ", new Vector2(0.2f, 0.77f), new Vector2(0.8f, 0.9f), 36);
        Setting("Ограничение FPS", 0.58f, () => Application.targetFrameRate = Application.targetFrameRate == 60 ? 120 : 60,
            () => Application.targetFrameRate + " FPS");
        Setting("Качество графики", 0.4f, () => QualitySettings.SetQualityLevel((QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length),
            () => QualitySettings.names[QualitySettings.GetQualityLevel()]);
        GameUi.Button(settings, "Назад", new Vector2(0.35f, 0.18f), new Vector2(0.65f, 0.28f), () => settings.gameObject.SetActive(false));
        pause.gameObject.SetActive(false);
        settings.gameObject.SetActive(false);
        result = GameUi.Box(overlay.transform, "Results", Vector2.zero, Vector2.one, GameUi.Background, true);
        resultTitle = GameUi.Label(result, "", new Vector2(0.1f, 0.73f), new Vector2(0.9f, 0.9f), 42);
        resultBody = GameUi.Label(result, "", new Vector2(0.15f, 0.34f), new Vector2(0.85f, 0.71f), 25);
        GameUi.Button(result, "Заново", new Vector2(0.18f, 0.14f), new Vector2(0.48f, 0.27f), () => Reload(true));
        GameUi.Button(result, "В главное меню", new Vector2(0.52f, 0.14f), new Vector2(0.82f, 0.27f), () => Reload(false));
        result.gameObject.SetActive(false);
    }

    private void Setting(string title, float y, UnityEngine.Events.UnityAction action, System.Func<string> value)
    {
        Button button = null;
        button = GameUi.Button(settings, title + ": " + value(), new Vector2(0.2f, y), new Vector2(0.8f, y + 0.11f), () =>
        {
            action();
            button.GetComponentInChildren<Text>().text = title + ": " + value();
        });
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (overlay != null) Destroy(overlay.gameObject);
    }
}
