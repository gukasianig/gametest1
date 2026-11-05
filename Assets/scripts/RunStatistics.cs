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
        started = true;
        finished = false;
        panel.SetActive(false);
    }

    public void EndRun() { finished = true; }

    public void RecordDamage(int amount)
    {
        if (started && !finished && amount > 0) DamageDealt += amount;
    }

    public void RecordKill()
    {
        if (started && !finished) Kills++;
    }

    private void Update()
    {
        if (!started) return;
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
        text.Append("Время выживания     ").Append(seconds / 60).Append(':')
            .Append((seconds % 60).ToString("00"));
        text.Append("\nУбийства     ").Append(Kills);
        text.Append("\nНанесённый урон     ").Append(DamageDealt);
        if (waves != null) text.Append("\nВолна     ").Append(waves.wave);
        if (progression != null) text.Append("\nУровень героя     ").Append(progression.Level);
        if (progression != null) text.Append("\nРадиус притяжения     ")
            .Append(progression.AttractionRadius.ToString("0.##")).Append(" м  (×")
            .Append(progression.PickupRadiusMultiplier.ToString("0.###")).Append(')');
        if (health != null) text.Append("\nЗдоровье     ").Append(health.CurrentHP).Append(" / ").Append(health.maxHP);
        if (movement != null) text.Append("\nСкорость движения     ")
            .Append((movement.speed * movement.SpeedMultiplier).ToString("0.##")).Append(" м/с  (×")
            .Append(movement.SpeedMultiplier.ToString("0.###")).Append(')');
        if (shooter != null && shooter.baseWeapon != null)
        {
            WeaponData weapon = shooter.baseWeapon;
            text.Append("\n\n<color=#65D8CA>ОРУЖИЕ: ").Append(weapon.weaponName).Append("</color>");
            float damageMultiplier = progression != null ? progression.DamageMultiplier : 1f;
            text.Append("\nУрон одной пули     ").Append(shooter.ExactDamage.ToString("0.##"))
                .Append("  (×").Append(damageMultiplier.ToString("0.###")).Append(')');
            float attackMultiplier = progression != null ? progression.AttackSpeedMultiplier : 1f;
            text.Append("\nВыстрелов в секунду     ")
                .Append((1f / Mathf.Max(0.03f, shooter.CurrentFireInterval)).ToString("0.###"))
                .Append("  (×").Append(attackMultiplier.ToString("0.###")).Append(')');
            text.Append("\nПуль за выстрел     ").Append(shooter.CurrentPelletCount);
            text.Append("\nДальность поиска цели     ").Append(shooter.range.ToString("0.#")).Append(" м");
            text.Append("\nСкорость пули     ").Append(weapon.bulletSpeed.ToString("0.#")).Append(" м/с");
            text.Append("\nРазброс     ").Append(shooter.CurrentSpread.ToString("0.#")).Append('°');
        }
        body.text = text.ToString();
        hint.text = finished ? "Забег завершён  •  Tab — закрыть" : "Бой продолжается  •  Tab — закрыть";
    }

    private Text Label(Transform parent, string name, Vector2 min, Vector2 max, int size, Color color)
    {
        Text label = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(parent, false);
        label.rectTransform.anchorMin = min;
        label.rectTransform.anchorMax = max;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        title.text = "СТАТИСТИКА ЗАБЕГА";
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
