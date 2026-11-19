using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Кооператив на одном ПК (этап 1, 11.11.2026, план docs/COOP_PLAN.md).
// Игрок 1 — WASD + Пробел/Левый Shift, игрок 2 — стрелки + Правый Shift/Правый Ctrl/Enter или геймпад (стик + A).
// Второй герой — копия первого без «менеджеров» сессии (меню, сессия, статистика, события).
// С этапа 2 у каждого героя свой PlayerProgression: опыт, карточки, бонусы (HeroStats), улучшения оружия.
public static class CoopMode
{
    // Выбор на экране оружия; живёт до выхода из игры (перезапуск забега его сохраняет).
    public static int Players { get; set; } = 1;
    // Кооператив закрыт ключом (F9 в меню); по умолчанию закрыт.
    public static bool Unlocked
    {
        get { return PlayerPrefs.GetInt("CoopUnlocked", 0) == 1; }
        set { PlayerPrefs.SetInt("CoopUnlocked", value ? 1 : 0); PlayerPrefs.Save(); }
    }
    public static bool Selected => Players > 1;
    // Оружие игрока 2 (этап 4): выбирается после игрока 1, хранится для «Повторить»; -1 — как у игрока 1.
    public static int SecondWeapon { get; set; } = -1;
    // В бою на арене действительно двое (в тест-арене второй герой не появляется).
    public static bool Active => Players > 1 && heroes.Count > 1;

    // Дальше друг от друга герои не расходятся: камера должна держать обоих.
    public const float MaxSpread = 24f;
    public const float ReviveRadius = 2.6f;
    public const float ReviveSeconds = 3f;
    public const float ReviveHealth = 0.5f;

    public static readonly Color[] Colors = { new Color(1f, 0.6f, 0.22f), new Color(0.3f, 0.7f, 1f) };

    private static readonly List<PlayerHealth> heroes = new List<PlayerHealth>();
    public static IReadOnlyList<PlayerHealth> Heroes => heroes;

    public static void Register(PlayerHealth hero) { if (hero != null && !heroes.Contains(hero)) heroes.Add(hero); }
    public static void Unregister(PlayerHealth hero) { heroes.Remove(hero); }

    public static int IndexOf(Component hero)
    {
        PlayMovement movement = hero != null ? hero.GetComponent<PlayMovement>() : null;
        return movement != null ? movement.playerIndex : 0;
    }

    public static bool AnyOtherAlive(PlayerHealth self)
    {
        foreach (PlayerHealth hero in heroes)
            if (hero != null && hero != self && !hero.IsDead) return true;
        return false;
    }

    public static PlayerHealth Partner(PlayerHealth self)
    {
        foreach (PlayerHealth hero in heroes)
            if (hero != null && hero != self) return hero;
        return null;
    }

    public static Transform NearestAlive(Vector3 position)
    {
        Transform best = null;
        float bestDistance = float.MaxValue;
        foreach (PlayerHealth hero in heroes)
        {
            if (hero == null || hero.IsDead) continue;
            Vector3 delta = hero.transform.position - position;
            delta.y = 0f;
            float distance = delta.sqrMagnitude;
            if (distance < bestDistance) { bestDistance = distance; best = hero.transform; }
        }
        return best;
    }

    // Урон зон опасности (огонь, кислота) по всем героям в радиусе.
    public static void DamageHeroesWithin(Vector3 centre, float radius, int damage, HeroDamageCause cause = HeroDamageCause.Unknown)
    {
        for (int i = heroes.Count - 1; i >= 0; i--)
        {
            PlayerHealth hero = heroes[i];
            if (hero != null && WeaponTargets.Flat(centre, hero.transform.position) <= radius) hero.TakeDamage(damage, cause);
        }
    }

    // Центр и разброс героев для камеры (поверженный герой тоже в кадре — к нему бегут поднимать).
    public static bool TryFrame(out Vector3 centre, out float spread)
    {
        centre = Vector3.zero;
        spread = 0f;
        int count = 0;
        foreach (PlayerHealth hero in heroes)
            if (hero != null) { centre += hero.transform.position; count++; }
        if (count < 2) return false;
        centre /= count;
        foreach (PlayerHealth hero in heroes)
            if (hero != null) spread = Mathf.Max(spread, WeaponTargets.Flat(centre, hero.transform.position) * 2f);
        return true;
    }

    // Убирает из скорости движение «от напарника», когда герои уже на пределе разброса.
    public static Vector3 Leash(PlayMovement mover, Vector3 position, Vector3 velocity)
    {
        PlayerHealth self = mover.GetComponent<PlayerHealth>();
        PlayerHealth partner = Partner(self);
        if (partner == null) return velocity;
        Vector3 away = position - partner.transform.position;
        away.y = 0f;
        if (away.magnitude < MaxSpread) return velocity;
        away.Normalize();
        float outward = Vector3.Dot(velocity, away);
        if (outward > 0f) velocity -= away * outward;
        return velocity;
    }

    // Копия героя игрока 1. Клон собирается под выключенным родителем, чтобы Awake «менеджеров» не успел сработать.
    public static GameObject SpawnSecond(GameObject hero, int weapon)
    {
        GameObject holder = new GameObject("Coop Spawn");
        holder.SetActive(false);
        Vector3 offset = hero.transform.right * 2.5f;
        Vector3 position = hero.transform.position + offset;
        if (ArenaLayout.Instance != null && !ArenaLayout.Instance.IsWalkable(position, 0.5f)) position = hero.transform.position - offset;
        GameObject clone = Object.Instantiate(hero, position, hero.transform.rotation, holder.transform);
        clone.name = "Player 2";

        // Порядок важен: StartMenu требует PlayerProgression, тот — AutoShoter и PlayMovement.
        Strip<StartMenu>(clone);
        Strip<TestArena>(clone);
        Strip<RunStatistics>(clone);
        Strip<ArenaEvents>(clone);
        Strip<GameSession>(clone);
        Strip<CombatHud>(clone);
        Strip<CoopHero>(clone);
        // PlayerProgression остаётся: у игрока 2 свой опыт, карточки и бонусы (этап 2); синглтон Instance он не занимает.

        PlayMovement movement = clone.GetComponent<PlayMovement>();
        if (movement != null) movement.playerIndex = 1;
        PlayerHealth health = clone.GetComponent<PlayerHealth>();
        if (health != null)
        {
            // Полоски и экран поражения в сцене принадлежат игроку 1.
            health.hpImage = null;
            health.hpDamageImage = null;
            health.gameOverUI = null;
        }

        clone.transform.SetParent(null, true);
        Object.Destroy(holder);
        AutoShoter shooter = clone.GetComponent<AutoShoter>();
        if (shooter != null) shooter.SelectWeapon(weapon);

        hero.AddComponent<CoopHero>();
        clone.AddComponent<CoopHero>();
        return clone;
    }

    private static void Strip<T>(GameObject target) where T : Component
    {
        foreach (T component in target.GetComponents<T>()) Object.DestroyImmediate(component);
    }

    // Ввод игрока 2: стрелки или стик геймпада (оси «Pad Horizontal/Vertical» в InputManager).
    public static Vector2 ReadMove(int playerIndex)
    {
        if (playerIndex == 0)
            return Vector2.ClampMagnitude(Keys(KeyCode.A, KeyCode.D, KeyCode.S, KeyCode.W), 1f);
        Vector2 pad = new Vector2(Input.GetAxis("Pad Horizontal"), Input.GetAxis("Pad Vertical"));
        return Vector2.ClampMagnitude(Keys(KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.DownArrow, KeyCode.UpArrow) + pad, 1f);
    }

    public static bool DashPressed(int playerIndex)
    {
        if (playerIndex == 0) return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift);
        return Input.GetKeyDown(KeyCode.RightShift) || Input.GetKeyDown(KeyCode.RightControl) ||
            Input.GetKeyDown(KeyCode.Keypad0) || Input.GetKeyDown(KeyCode.JoystickButton0);
    }

    // Выбор карточки без паузы (этап 3): И1 — 1/2/3, переброс R; И2 — Num1/2/3, переброс Num4, геймпад X/Y/B и LB.
    public static int CardPressed(int playerIndex)
    {
        if (playerIndex == 0)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) return 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha3)) return 2;
            return -1;
        }
        if (Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.JoystickButton2)) return 0;
        if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.JoystickButton3)) return 1;
        if (Input.GetKeyDown(KeyCode.Keypad3) || Input.GetKeyDown(KeyCode.JoystickButton1)) return 2;
        return -1;
    }

    public static bool RerollPressed(int playerIndex)
    {
        return playerIndex == 0 ? Input.GetKeyDown(KeyCode.R)
            : Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.JoystickButton4);
    }

    private static readonly string[] padCards = { "X", "Y", "B" };
    public static string CardKey(int playerIndex, int card) { return playerIndex == 0 ? (card + 1).ToString() : "Num" + (card + 1) + " / " + padCards[card]; }
    public static string RerollKey(int playerIndex) { return playerIndex == 0 ? "R" : "Num4 / LB"; }
    public static string ChoiceHint(int playerIndex)
    {
        return playerIndex == 0 ? GameText.Source("coopmode.104") : GameText.Source("coopmode.105");
    }

    private static Vector2 Keys(KeyCode left, KeyCode right, KeyCode down, KeyCode up)
    {
        return new Vector2((Input.GetKey(right) ? 1f : 0f) - (Input.GetKey(left) ? 1f : 0f),
            (Input.GetKey(up) ? 1f : 0f) - (Input.GetKey(down) ? 1f : 0f));
    }

    public static string ControlsHint =>
        GameText.Source("coopmode.106");
}

// Висит на каждом герое в кооперативе: цветное кольцо под ногами, круг подъёма поверженного, панель игрока 2.
public class CoopHero : MonoBehaviour
{
    private const int Segments = 48;
    private static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();

    private PlayerHealth health;
    private PlayMovement movement;
    private int index;
    private LineRenderer marker, reviveRing, reviveProgress;
    private float reviveAmount;
    private bool wasDead;

    private Canvas hudCanvas;
    private RectTransform hpFill, xpFill;
    private PlayerProgression progression;
    private Text hudText;
    private float hudTimer;

    private void Start()
    {
        health = GetComponent<PlayerHealth>();
        movement = GetComponent<PlayMovement>();
        index = CoopMode.IndexOf(this);
        Color color = CoopMode.Colors[Mathf.Clamp(index, 0, CoopMode.Colors.Length - 1)];
        marker = Ring("Coop Marker", color, 0.12f);
        reviveRing = Ring("Revive Ring", new Color(1f, 0.85f, 0.35f), 0.1f);
        reviveProgress = Ring("Revive Progress", new Color(0.35f, 1f, 0.55f), 0.22f);
        reviveRing.enabled = reviveProgress.enabled = false;
        if (index > 0) BuildHud(color);
    }

    private void Update()
    {
        if (health == null) return;
        Vector3 feet = transform.position;
        feet.y = GroundHeight.At(feet) + 0.08f;
        Circle(marker, feet, 0.9f, 1f);
        marker.enabled = !health.IsDead;

        bool playing = GameSession.Instance != null && GameSession.Instance.State == GameRunState.Playing;
        if (health.IsDead != wasDead)
        {
            wasDead = health.IsDead;
            reviveAmount = 0f;
            if (wasDead && GameSession.Instance != null && CoopMode.AnyOtherAlive(health))
                GameSession.Instance.Announce(GameText.Source("coopmode.107") + (index + 1) + GameText.Source("coopmode.108"), 4f);
        }
        if (health.IsDead && playing && Time.timeScale > 0f) TickRevive(feet);
        reviveRing.enabled = reviveProgress.enabled = health.IsDead && playing;
        if (health.IsDead)
        {
            Circle(reviveRing, feet, CoopMode.ReviveRadius, 1f);
            Circle(reviveProgress, feet + Vector3.up * 0.02f, CoopMode.ReviveRadius, reviveAmount);
        }
        UpdateHud(playing);
    }

    private void TickRevive(Vector3 feet)
    {
        bool helped = false;
        foreach (PlayerHealth hero in CoopMode.Heroes)
            if (hero != null && hero != health && !hero.IsDead &&
                WeaponTargets.Flat(feet, hero.transform.position) <= CoopMode.ReviveRadius)
                helped = true;
        // Ушёл из круга — прогресс медленно тает, а не сбрасывается сразу.
        reviveAmount = Mathf.Clamp01(reviveAmount + Time.deltaTime / CoopMode.ReviveSeconds * (helped ? 1f : -0.5f));
        if (reviveAmount < 1f) return;
        reviveAmount = 0f;
        health.Revive(CoopMode.ReviveHealth);
        // Подъём: зелёное кольцо и вспышка.
        Color heal = new Color(0.35f, 1f, 0.55f);
        EnemyFx.Ring(feet, CoopMode.ReviveRadius, heal, 0.6f, 0.35f);
        EnemyFx.LightFlash(feet + Vector3.up * 1.5f, heal, 7f, 3.5f, 0.5f);
        EnemyFx.Puff(feet + Vector3.up * 0.8f, heal, 0.9f, 0.6f, Vector3.up * 1.5f);
        if (GameSession.Instance != null) GameSession.Instance.Announce(GameText.Source("coopmode.107") + (index + 1) + GameText.Source("coopmode.109"), 2f);
    }

    private void BuildHud(Color color)
    {
        hudCanvas = GameUi.Canvas("Coop HUD P" + (index + 1), 78);
        RectTransform box = GameUi.Box(hudCanvas.transform, "Player Panel", new Vector2(0.74f, 0.03f), new Vector2(0.98f, 0.125f), GameUi.Background);
        RectTransform title = GameUi.Box(box, "Accent", new Vector2(0f, 0f), new Vector2(0.025f, 1f), color);
        title.SetAsLastSibling();
        RectTransform bar = GameUi.Box(box, "HP Back", new Vector2(0.06f, 0.24f), new Vector2(0.96f, 0.46f), new Color(0.12f, 0.15f, 0.18f, 1f));
        hpFill = GameUi.Box(bar, "HP Fill", Vector2.zero, Vector2.one, new Color(0.35f, 0.85f, 0.45f));
        // Опыт игрока 2 (свой уровень и карточки — этап 2).
        RectTransform xpBar = GameUi.Box(box, "XP Back", new Vector2(0.06f, 0.08f), new Vector2(0.96f, 0.17f), new Color(0.12f, 0.15f, 0.18f, 1f));
        xpFill = GameUi.Box(xpBar, "XP Fill", Vector2.zero, Vector2.one, new Color(0.1f, 0.62f, 0.85f));
        progression = GetComponent<PlayerProgression>();
        hudText = GameUi.Readable(GameUi.Label(box, "", new Vector2(0.06f, 0.5f), new Vector2(0.96f, 0.95f), 18, Color.white), 1f);
        hudText.alignment = TextAnchor.MiddleLeft;
    }

    private void UpdateHud(bool playing)
    {
        if (hudCanvas == null) return;
        hudCanvas.enabled = playing || (GameSession.Instance != null && GameSession.Instance.State == GameRunState.Upgrade);
        if (Time.unscaledTime < hudTimer) return;
        hudTimer = Time.unscaledTime + 0.1f;
        float fraction = health.maxHP > 0 ? Mathf.Clamp01((float)health.CurrentHP / health.maxHP) : 0f;
        hpFill.anchorMax = new Vector2(fraction, 1f);
        string state;
        if (health.IsDead) state = GameText.Source("coopmode.110") + Mathf.RoundToInt(reviveAmount * 100f) + "%)";
        else
        {
            float dash = movement != null ? movement.DashCooldownRemaining : 0f;
            state = health.CurrentHP + "/" + health.maxHP + GameText.Source("coopmode.111") + (dash <= 0f ? GameText.Source("coopmode.112") : dash.ToString("0.0") + GameText.Source("arenaevents.006"));
        }
        string level = "";
        if (progression != null)
        {
            level = GameText.Source("coopmode.113") + progression.Level;
            xpFill.anchorMax = new Vector2(Mathf.Clamp01((float)progression.Experience / Mathf.Max(1, progression.ExperienceRequired)), 1f);
        }
        hudText.text = GameText.Source("coopmode.114") + (index + 1) + level + "   " + state;
    }

    private LineRenderer Ring(string name, Color color, float width)
    {
        GameObject item = new GameObject(name, typeof(LineRenderer));
        LineRenderer line = item.GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.widthMultiplier = width;
        line.positionCount = Segments + 1;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        if (!materials.TryGetValue(color, out Material material) || material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.color = color;
            materials[color] = material;
        }
        line.sharedMaterial = material;
        return line;
    }

    private static void Circle(LineRenderer line, Vector3 centre, float radius, float share)
    {
        share = Mathf.Clamp01(share);
        for (int i = 0; i <= Segments; i++)
        {
            float angle = Mathf.PI * 2f * share * i / Segments;
            line.SetPosition(i, centre + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius);
        }
    }

    private void OnDestroy()
    {
        if (marker != null) Destroy(marker.gameObject);
        if (reviveRing != null) Destroy(reviveRing.gameObject);
        if (reviveProgress != null) Destroy(reviveProgress.gameObject);
        if (hudCanvas != null) Destroy(hudCanvas.gameObject);
    }
}
