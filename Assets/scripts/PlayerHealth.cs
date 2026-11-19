using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum HeroDamageCause
{
    Unknown, Contact, Projectile, Mortar, Charge, Explosion, EliteExplosion,
    BossSlam, BossProjectile, BossCharge, Lightning, Beam, Acid, Fire, Meteor
}

public class PlayerHealth : MonoBehaviour, IDamagable
{
    // Бонусы своего героя (кооператив: у каждого свой набор, CombatStats.Of).
    private HeroStats heroStats;
    private HeroStats Stats => heroStats ?? (heroStats = CombatStats.Of(this));
    public Image hpImage;
    public Image hpDamageImage;
    public int maxHP = 100;
    public GameObject gameOverUI;

    private bool isDead;
    private bool returningToMenu;
    private int currentHP;
    public int CurrentHP => currentHP;
    public bool IsDead => isDead;
    public HeroDamageCause LastDamageCause { get; private set; }
    public int LastDamage { get; private set; }
    public string DeathSummary => GameText.Source("death.summary") +
        GameText.Source("death.cause." + LastDamageCause.ToString().ToLowerInvariant()) +
        GameText.Source("death.damage") + LastDamage;
    private float damageHold;

    // Удар по герою: (урон до брони, урон после брони). Нужен экрану боя (вспышка ХП и брони).
    public event System.Action<int, int> Damaged;

    private void OnEnable() { CoopMode.Register(this); }
    private void OnDisable() { CoopMode.Unregister(this); }

    private void Start()
    {
        // Панель боя (CombatHud) — только у игрока 1; у игрока 2 своя панель (CoopHero).
        if (CoopMode.IndexOf(this) == 0 && GetComponent<CombatHud>() == null) gameObject.AddComponent<CombatHud>();
        if (GetComponent<HeroAbilities>() == null) gameObject.AddComponent<HeroAbilities>();
        if (GetComponent<HeroPresentation>() == null) gameObject.AddComponent<HeroPresentation>();
        Time.timeScale = 1f;
        currentHP = Mathf.Max(1, maxHP);
        StyleHealthBar();
        if (hpImage != null) hpImage.fillAmount = 1f;
        if (hpDamageImage != null) hpDamageImage.fillAmount = 1f;
        if (gameOverUI != null)
        {
            CreateReturnButton();
            gameOverUI.SetActive(false);
        }
    }

    private float invulnerableUntil;
    public bool Invulnerable => Time.time < invulnerableUntil;

    // Short protection after a dash. Extends, never shortens, an existing one.
    public void GrantInvulnerability(float seconds)
    {
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + Mathf.Max(0f, seconds));
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, HeroDamageCause.Unknown);
    }

    public void TakeDamage(int damage, HeroDamageCause cause)
    {
        if (isDead || damage <= 0 || Invulnerable || (GameSession.Instance != null && GameSession.Instance.State != GameRunState.Playing)) return;
        damage = Mathf.Max(1, Mathf.RoundToInt(damage * RunModifiers.HeroDamageTaken));   // испытание «Тяжёлые удары»
        int raw = damage;
        // Синергия «Стальная воля»: при низком HP броня +15% (предел 75% вместо 60%).
        float armor = Mathf.Min(0.6f, Stats.Armor) + (SteelWillActive ? 0.15f : 0f);
        if (armor > 0f) damage = Mathf.Max(1, Mathf.RoundToInt(damage * (1f - Mathf.Min(0.75f, armor))));
        int lostHP = Mathf.Min(currentHP, damage);
        currentHP = Mathf.Max(0, currentHP - damage);
        if (currentHP > 0) GrantInvulnerability(.18f);
        RunStatistics statistics = GameSession.Instance != null ? GameSession.Instance.GetComponent<RunStatistics>() : GetComponent<RunStatistics>();
        if (statistics != null) statistics.RecordIncoming(lostHP, cause);
        LastDamageCause = cause;
        LastDamage = damage;
        damageHold = 0.25f;
        if (Damaged != null) Damaged(raw, damage);
        if (currentHP == 0) Die();
    }

    public int Heal(int amount)
    {
        if (isDead || amount <= 0) return 0;
        int restored = Mathf.Min(amount, Mathf.Max(1, maxHP) - currentHP);
        currentHP += restored;
        return restored;
    }

    private float healthRemainder;

    // Heals fractional amounts (regeneration, vampirism) by collecting them until a whole point is reached.
    private void HealFraction(float amount)
    {
        healthRemainder += amount;
        int whole = Mathf.FloorToInt(healthRemainder);
        if (whole <= 0) return;
        healthRemainder -= whole;
        Heal(whole);
    }

    // Called for every enemy killed (Vampirism upgrade).
    public void OnEnemyKilled()
    {
        if (Stats.VampirismPerKill > 0f && !isDead) HealFraction(Stats.VampirismPerKill);
    }

    // A cursed upgrade lowers the maximum without healing: the current value is only capped.
    public void ReduceMaxHealth(float fraction)
    {
        if (isDead) return;
        maxHP = Mathf.Max(1, Mathf.RoundToInt(maxHP * (1f - Mathf.Clamp01(fraction))));
        currentHP = Mathf.Min(currentHP, maxHP);
    }

    public bool SteelWillActive => Stats.SteelWill && !isDead && currentHP < maxHP * CombatStats.SteelWillThreshold;

    private void Update()
    {
        if (Stats.RegenPerSecond > 0f && !isDead && Time.timeScale > 0f &&
            (GameSession.Instance == null || GameSession.Instance.State == GameRunState.Playing))
            HealFraction(Stats.RegenPerSecond * (SteelWillActive ? 3f : 1f) * Time.deltaTime);
        if (hpImage == null || hpDamageImage == null) return;
        float target = (float)currentHP / Mathf.Max(1, maxHP);
        hpImage.fillAmount = Mathf.Lerp(hpImage.fillAmount, target,
            1f - Mathf.Exp(-14f * Time.deltaTime));
        damageHold = Mathf.Max(0f, damageHold - Time.deltaTime);
        if (damageHold <= 0f)
            hpDamageImage.fillAmount = Mathf.Lerp(hpDamageImage.fillAmount, target,
                1f - Mathf.Exp(-4f * Time.deltaTime));
        hpDamageImage.fillAmount = Mathf.Max(hpImage.fillAmount, hpDamageImage.fillAmount);
        hpImage.color = Color.Lerp(new Color(0.95f, 0.2f, 0.18f),
            new Color(0.2f, 0.88f, 0.18f), hpImage.fillAmount);
    }

    private void StyleHealthBar()
    {
        if (hpImage == null || hpDamageImage == null) return;
        RectTransform frame = hpImage.transform.parent as RectTransform;
        if (frame == null || hpDamageImage.transform.parent != frame) return;
        frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0f);
        frame.pivot = new Vector2(0.5f, 0.5f);
        frame.anchoredPosition = new Vector2(0f, 50f);
        frame.sizeDelta = new Vector2(320f, 26f);
        Image background = frame.GetComponent<Image>();
        if (background != null)
        {
            background.color = new Color(0.16f, 0.17f, 0.16f, 0.85f);
            background.raycastTarget = false;
        }
        foreach (Image image in new[] { hpDamageImage, hpImage })
        {
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = new Vector2(5f, 3f);
            image.rectTransform.offsetMax = new Vector2(-5f, -3f);
            // Keep the rounded sprite already assigned in the scene.
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = 0;
            image.raycastTarget = false;
        }
        hpImage.color = new Color(0.2f, 0.88f, 0.18f);
        hpDamageImage.color = new Color(0.8f, 0.85f, 0.72f, 0.7f);
        HealthBarFx effects = frame.GetComponent<HealthBarFx>();
        if (effects == null) effects = frame.gameObject.AddComponent<HealthBarFx>();
        effects.Initialize(this, hpImage);
    }

    private void CreateReturnButton()
    {
        GameObject root = new GameObject("Return To Menu", typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(gameOverUI.transform, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.32f, 0.22f);
        rect.anchorMax = new Vector2(0.68f, 0.33f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = root.GetComponent<Image>();
        image.color = new Color(0.08f, 0.28f, 0.36f);
        Button button = root.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(ReturnToMenu);

        Text label = new GameObject("Label", typeof(RectTransform), typeof(LocalizedUiText)).GetComponent<Text>();
        label.transform.SetParent(root.transform, false);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(12f, 4f);
        label.rectTransform.offsetMax = new Vector2(-12f, -4f);
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = GameText.Source("gamesession.150");
        label.fontSize = 26;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 12;
        label.resizeTextMaxSize = 26;
        label.raycastTarget = false;
    }

    private void ReturnToMenu()
    {
        if (!isDead || returningToMenu) return;
        returningToMenu = true;
        Time.timeScale = 1f;
        // Reload resets enemies, pickups, upgrades and health; StartMenu opens again.
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Кооператив: поверженного поднимает напарник (CoopHero), с частью здоровья и короткой неуязвимостью.
    public event System.Action Revived;

    public void Revive(float healthShare)
    {
        if (!isDead || returningToMenu) return;
        isDead = false;
        LastDamageCause = HeroDamageCause.Unknown;
        LastDamage = 0;
        currentHP = Mathf.Clamp(Mathf.RoundToInt(maxHP * healthShare), 1, maxHP);
        GrantInvulnerability(2f);
        if (Revived != null) Revived();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        // Кооператив: пока жив напарник, герой только повержен; поражение — когда упали все.
        if (CoopMode.Active && GameSession.Instance != null && CoopMode.AnyOtherAlive(this)) return;
        if (GameSession.Instance != null)
        {
            if (hpImage != null) hpImage.fillAmount = 0f;
            if (hpDamageImage != null) hpDamageImage.fillAmount = 0f;
            if (gameOverUI != null) gameOverUI.SetActive(false);
            GameSession.Instance.Defeat(this);
            return;
        }
        if (RunStatistics.Instance != null) RunStatistics.Instance.EndRun();
        if (hpImage != null) hpImage.fillAmount = 0f;
        if (hpDamageImage != null) hpDamageImage.fillAmount = 0f;
        if (gameOverUI != null) gameOverUI.SetActive(true);
        Time.timeScale = 0f;
    }

    // Run-start scaling (run modifiers); keeps the hero at full health.
    public void ScaleMaxHealth(float factor)
    {
        if (isDead || factor <= 0f || Mathf.Approximately(factor, 1f)) return;
        maxHP = Mathf.Max(1, Mathf.RoundToInt(maxHP * factor));
        currentHP = maxHP;
    }

    public void IncreaseMaxHealth(int amount)
    {
        if (amount <= 0 || isDead) return;
        maxHP += amount;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
    }
}
