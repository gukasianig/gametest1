using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(AutoShoter), typeof(PlayMovement))]
public class PlayerProgression : MonoBehaviour
{
    public static PlayerProgression Instance { get; private set; }
    public ProgressionSettings settings;
    public UpgradeCatalog catalog;
    public int Level { get; private set; } = 1;
    public int Experience { get; private set; }
    public float PickupRadiusMultiplier { get; private set; } = 1f;
    public float CollectionRadius => Mathf.Max(0.1f, settings.collectionRadius) * PickupRadiusMultiplier;
    public float AttractionRadius => Mathf.Max(CollectionRadius, settings.attractionRadius * PickupRadiusMultiplier);
    public bool Choosing { get; private set; }
    public int PendingChoices { get; private set; }
    public IReadOnlyList<UpgradeDefinition> Offered => offered;
    public float DamageMultiplier { get; private set; } = 1f;
    public float AttackSpeedMultiplier { get; private set; } = 1f;
    private readonly Dictionary<string, int> ranks = new Dictionary<string, int>();
    private readonly List<UpgradeDefinition> offered = new List<UpgradeDefinition>();
    private readonly Button[] buttons = new Button[3];
    private Button rerollButton, banishButton;
    private Text choicesTitle;
    private readonly Text[] cardText = new Text[3];
    private readonly Image[] icons = new Image[3];
    private readonly UpgradeIcon[] vectorIcons = new UpgradeIcon[3];
    private readonly UpgradeCardFx[] cardFx = new UpgradeCardFx[3];
    private readonly Image[] iconBadges = new Image[3];
    private readonly Image[] accentLines = new Image[3];
    private readonly Text[] rarityLabels = new Text[3];
    private RectTransform toast;
    private CanvasGroup toastGroup;
    private Text toastText, toastDetail;
    private Image toastStripe, toastIcon;
    private UpgradeIcon toastVector;
    private const float ToastLife = 2.6f, ToastDelay = 0.28f;
    private RectTransform toastBadge, toastShine;
    private float toastTime;
    private AutoShoter shooter;
    private PlayMovement movement;
    private OrbitManager orbits;
    private Canvas canvas;
    private RectTransform choices;
    private Text status, levelText;
    private Image fill;
    private float shownExperience, targetExperience;
    private int hudLevel;
    private bool ownsSettings;
    private PlayerHealth ownHealth;

    // Свой набор бонусов у каждого героя (кооператив, этап 2). Игрок 1 — он же CombatStats.Primary.
    public HeroStats Stats { get; } = new HeroStats();
    // Игрок 2 в кооперативе: свой опыт и карточки, но без верхней полосы опыта (уровень — на его панели, CoopHero).
    public bool Secondary => CoopMode.IndexOf(this) > 0;
    public int ExperienceRequired => settings.ExperienceForLevel(Level);

    private void Awake()
    {
        if (!Secondary) { Instance = this; CombatStats.RunTrophyParts = 0; }
        Stats.Reset();
        CombatStats.Register(Stats);
        shooter = GetComponent<AutoShoter>();
        movement = GetComponent<PlayMovement>();
        orbits = GetComponent<OrbitManager>();
        ownHealth = GetComponent<PlayerHealth>();
        if (settings == null) { settings = ScriptableObject.CreateInstance<ProgressionSettings>(); ownsSettings = true; }
    }

    private void Start()
    {
        if (GetComponent<HeroCardEffects>() == null) gameObject.AddComponent<HeroCardEffects>();
        BuildUI();
        RefreshUI();
    }

    private void Update()
    {
        if (toastTime > 0f)
        {
            toastTime = Mathf.Max(0f, toastTime - Time.unscaledDeltaTime);
            // Въезд снизу с небольшим перелётом, затем затухание за последние 0,4 с.
            float age = ToastLife - toastTime;
            float k = Mathf.Clamp01(age / 0.3f);
            float back = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);
            toast.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(-40f, 0f, back));
            toast.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, back);
            toastGroup.alpha = Mathf.Min(Mathf.Clamp01(age / 0.15f), toastTime / 0.4f);
            AnimateToast(age);
            if (toastTime == 0f) toast.gameObject.SetActive(false);
        }
        if (Choosing && coopWindow && !chestMode && Time.timeScale > 0f) ReadChoiceKeys();
        else if (Choosing && !coopWindow) ReadPausedChoiceKeys();
        if (Time.timeScale <= 0f || Choosing) return;
        // Поверженный в кооперативе герой кристаллы не собирает (их забирает напарник).
        if (!(CoopMode.Active && ownHealth != null && ownHealth.IsDead)) ExperiencePickup.CollectNearby(this, settings);
        if (PendingChoices > 0) ShowChoices();
    }

    public void AddExperience(int amount) { AddExperience(amount, true); }

    public void AddExperience(int amount, bool allowChoices)
    {
        if (amount <= 0) return;
        long total = (long)Experience + amount;
        int before = Level;
        while (total >= settings.ExperienceForLevel(Level))
        {
            total -= settings.ExperienceForLevel(Level);
            Level++;
            if (allowChoices) PendingChoices++;
        }
        Experience = (int)total;
        if (Level > before && Time.timeScale > 0f) LevelUpFx();
        RefreshUI();
        if (allowChoices && !Choosing && PendingChoices > 0 && Time.timeScale > 0f) ShowChoices();
    }

    // Новый уровень: кольцо у ног, вспышка и искры вверх (в кооперативе — цвет игрока).
    private void LevelUpFx()
    {
        Vector3 feet = transform.position;
        feet.y = GroundHeight.At(feet) + 0.05f;
        Color color = CoopMode.Active ? CoopMode.Colors[Mathf.Clamp(CoopMode.IndexOf(this), 0, 1)] : new Color(0.35f, 0.9f, 1f);
        EnemyFx.Ring(feet, 2.4f, color, 0.55f, 0.3f);
        EnemyFx.LightFlash(feet + Vector3.up * 1.6f, color, 7f, 3f, 0.45f);
        GameAudio.Play(Sfx.LevelUp, 0.85f, 0f);
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            EnemyFx.Puff(feet + new Vector3(Mathf.Cos(a) * 0.6f, 0.4f, Mathf.Sin(a) * 0.6f), color, 0.16f, 0.7f, Vector3.up * 2.2f);
        }
    }

    public int Rank(string id) { return ranks.TryGetValue(id, out int rank) ? rank : 0; }

    private float Bonus(UpgradeEffect effect)
    {
        // Permanent workshop bonuses count toward the same caps as in-run upgrades.
        float sum = effect == UpgradeEffect.MaxHealth ? 0f : MetaProgress.Bonus(effect);
        if (catalog != null)
            foreach (UpgradeDefinition definition in catalog.entries)
                if (definition.effect == effect) sum += definition.amount * Rank(definition.id);
        foreach (UpgradeDefinition definition in ChestUpgrades)
            if (definition.effect == effect) sum += definition.amount * Rank(definition.id);
        foreach (UpgradeDefinition definition in SynergyUpgrades)
            if (definition.effect == effect) sum += definition.amount * Rank(definition.id);
        return sum;
    }

    private bool metaApplied;

    // Called once when a run begins: applies permanent bonuses (health directly, the rest via ApplyStats).
    public void ApplyMetaBonuses()
    {
        if (!metaApplied)
        {
            metaApplied = true;
            int health = Mathf.RoundToInt(MetaProgress.Bonus(UpgradeEffect.MaxHealth));
            if (health > 0) GetComponent<PlayerHealth>().IncreaseMaxHealth(health);
            if (WorkshopProjects.HasRelic(3)) GetComponent<PlayerHealth>().ScaleMaxHealth(.9f);
            GetComponent<PlayerHealth>().ScaleMaxHealth(RunModifiers.HeroHealth);
        }
        ApplyStats();
    }

    private float Cap(UpgradeEffect effect)
    {
        switch (effect)
        {
            case UpgradeEffect.Damage: return settings.maxDamageMultiplier;
            case UpgradeEffect.AttackSpeed: return settings.maxAttackSpeedMultiplier;
            case UpgradeEffect.Movement: return settings.maxMovementMultiplier;
            case UpgradeEffect.Pickup: return settings.maxPickupRadiusMultiplier;
            case UpgradeEffect.GrenadeDamage: case UpgradeEffect.OrbitDamage: return 5f;
            case UpgradeEffect.GrenadeRadius: return 2.5f;
            default: return float.PositiveInfinity;
        }
    }

    public bool Available(UpgradeDefinition definition, bool ignoreLevel = false)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.id) || banished.Contains(definition.id) || definition.amount <= 0f ||
            (!ignoreLevel && Level < definition.minimumLevel) || Rank(definition.id) >= definition.maxRanks) return false;
        if ((definition.effect == UpgradeEffect.GrenadeDamage || definition.effect == UpgradeEffect.GrenadeRadius) && shooter.grenadePrefab == null) return false;
        if ((definition.effect == UpgradeEffect.OrbitDamage || definition.effect == UpgradeEffect.OrbitCount) &&
            (orbits == null || orbits.orbitPrefab == null)) return false;
        if (definition.effect == UpgradeEffect.OrbitDamage && orbits.orbitCount == 0) return false;
        // Crit power only makes sense once there is a chance to crit.
        if (definition.effect == UpgradeEffect.CritPower && Bonus(UpgradeEffect.CritChance) <= 0f) return false;
        // Piercing and ricochet change ordinary bullets only (drone, turret and mine cards work with any weapon).
        WeaponKind kind = shooter.baseWeapon != null ? shooter.baseWeapon.kind : WeaponKind.Standard;
        if ((definition.effect == UpgradeEffect.Pierce || definition.effect == UpgradeEffect.Ricochet) && kind != WeaponKind.Standard) return false;
        // Дрон, турель и мины — улучшения боя для любого оружия; карточки урона нужны только после первого юнита.
        if (definition.effect == UpgradeEffect.DroneDamage && Bonus(UpgradeEffect.DroneCount) <= 0f) return false;
        // «Радиус взрыва» усиливает гранаты — только после карточки «Гранаты» (19.11.2026: раньше выпадал без них и сам их включал).
        if (definition.effect == UpgradeEffect.GrenadeRadius && Bonus(UpgradeEffect.GrenadeDamage) <= 0f) return false;
        if (definition.effect == UpgradeEffect.TurretDamage && Bonus(UpgradeEffect.TurretCount) <= 0f) return false;
        if ((definition.effect == UpgradeEffect.ExtraPellets || definition.effect == UpgradeEffect.ExplosiveBullets ||
            definition.effect == UpgradeEffect.WeaponRange || definition.effect == UpgradeEffect.SplitShot ||
            definition.effect == UpgradeEffect.ChargedShot || definition.effect == UpgradeEffect.CritPierce) && kind != WeaponKind.Standard) return false;
        // «Азарт» бессмыслен в испытании «Без аптечек».
        if (definition.effect == UpgradeEffect.Gamble && RunModifiers.NoHealthKits) return false;
        bool hasOrbits = orbits != null && orbits.orbitPrefab != null && orbits.orbitCount > 0;
        if (definition.effect == UpgradeEffect.OrbitSpeed && !hasOrbits) return false;
        if (definition.effect == UpgradeEffect.OrbitCrit && (!hasOrbits || Bonus(UpgradeEffect.CritChance) <= 0f)) return false;
        // Every upgrade added after the crit card has no multiplier cap; its rank limit is enough.
        if ((int)definition.effect >= (int)UpgradeEffect.CritChance) return true;
        return definition.effect == UpgradeEffect.MaxHealth || definition.effect == UpgradeEffect.OrbitCount ||
            definition.effect == UpgradeEffect.CritChance || definition.effect == UpgradeEffect.CritPower ||
            definition.effect == UpgradeEffect.Pierce || definition.effect == UpgradeEffect.Ricochet ||
            definition.effect == UpgradeEffect.DroneCount || definition.effect == UpgradeEffect.DroneDamage ||
            definition.effect == UpgradeEffect.TurretCount || definition.effect == UpgradeEffect.TurretDamage || definition.effect == UpgradeEffect.MineCount ||
            1f + Bonus(definition.effect) < Mathf.Max(1f, Cap(definition.effect)) - 0.0001f;
    }

    public void RollOffers()
    {
        offered.Clear();
        if (catalog == null) return;
        List<UpgradeDefinition> candidates = Candidates();
        // Кооператив: карточки из открытого окна напарника — только если иначе не набрать три.
        HashSet<string> partner = PartnerOffers();
        var reserve = new List<UpgradeDefinition>();
        if (partner.Count > 0)
            for (int i = candidates.Count - 1; i >= 0; i--)
                if (partner.Contains(candidates[i].id)) { reserve.Add(candidates[i]); candidates.RemoveAt(i); }
        PickWeighted(candidates);
        PickWeighted(reserve);
        // Запоминаем показанное: следующие два окна реже повторяют те же карточки.
        foreach (UpgradeDefinition definition in offered) recentOffers.Add(definition.id);
        while (recentOffers.Count > 6) recentOffers.RemoveAt(0);
    }

    // Все доступные сейчас карточки (каталог + готовые синергии), без повторов id.
    private List<UpgradeDefinition> Candidates()
    {
        var candidates = new List<UpgradeDefinition>();
        var ids = new HashSet<string>();
        if (catalog == null) return candidates;
        foreach (UpgradeDefinition definition in catalog.entries)
            if (Available(definition) && ids.Add(definition.id)) candidates.Add(definition);
        // Синергии попадают в выбор, только когда у героя уже есть обе части связки.
        foreach (UpgradeDefinition definition in SynergyUpgrades)
            if (SynergyReady(definition) && Available(definition) && ids.Add(definition.id)) candidates.Add(definition);
        return candidates;
    }

    private readonly List<string> recentOffers = new List<string>();

    // Баланс 19.11.2026: удача повышает шанс редких/особых карточек; уже взятые и недавно показанные выпадают реже.
    private float OfferWeight(UpgradeDefinition definition)
    {
        float weight = catalog.Weight(definition);
        float luck = Mathf.Max(0f, Stats.DropLuck);
        if (definition.rarity == UpgradeRarity.Rare) weight *= 1f + luck;
        else if (definition.rarity == UpgradeRarity.Special) weight *= 1f + 1.5f * luck;
        weight /= 1f + 0.12f * Rank(definition.id);
        if (recentOffers.Contains(definition.id)) weight *= 0.3f;
        return Mathf.Max(0.01f, weight);
    }

    private void PickWeighted(List<UpgradeDefinition> candidates)
    {
        while (offered.Count < 3 && candidates.Count > 0)
        {
            int pick = WeightedIndex(candidates);
            offered.Add(candidates[pick]);
            candidates.RemoveAt(pick);
        }
    }

    private int WeightedIndex(List<UpgradeDefinition> candidates)
    {
        float sum = 0f;
        foreach (UpgradeDefinition definition in candidates) sum += OfferWeight(definition);
        float roll = Random.value * sum;
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= OfferWeight(candidates[i]);
            if (roll < 0f) return i;
        }
        return candidates.Count - 1;
    }

    private void ShowChoices()
    {
        // Never re-roll while cards are already on screen: Update() calls this right after
        // ExperiencePickup.CollectNearby has opened the window in the same frame, which used to
        // replace the offered list while the old cards stayed drawn (clicks picked another upgrade).
        if (Choosing) return;
        // Кооператив (этап 3): игра не встаёт на паузу, у каждого своё окно на своей стороне экрана и свои клавиши.
        bool coop = CoopMode.Active;
        if (coop && GameSession.Instance != null && GameSession.Instance.State != GameRunState.Playing) return;
        RollOffers();
        if (offered.Count == 0) { PendingChoices = 0; return; }
        if (!coop && GameSession.Instance != null && !GameSession.Instance.EnterUpgrade()) return;
        Choosing = true;
        coopWindow = coop;
        if (!coop) Time.timeScale = 0f;
        ApplyChoiceLayout(coop);
        choicesTitle.text = LevelTitle;
        choicesTitle.color = coop ? CoopMode.Colors[Mathf.Clamp(CoopMode.IndexOf(this), 0, 1)] : Color.white;
        choices.gameObject.SetActive(true);
        RefreshCards();
    }

    // Окно без паузы (кооператив): выбор клавишами своего игрока, бой продолжается.
    private bool coopWindow;
    public bool CoopWindow => Choosing && coopWindow;

    private string LevelTitle => coopWindow
        ? GameText.Source("coopmode.114") + (CoopMode.IndexOf(this) + 1) + GameText.Source("playerprogression.194") + Level + "\n<size=16>" + CoopMode.ChoiceHint(CoopMode.IndexOf(this)) + "</size>"
        : GameText.Source("playerprogression.195");

    // Полноэкранное окно (одиночная игра, сундук) или компактное на своей стороне (кооператив: И1 слева, И2 справа).
    private void ApplyChoiceLayout(bool coop)
    {
        if (coop)
        {
            bool right = Secondary;
            choices.anchorMin = new Vector2(right ? 0.64f : 0.01f, 0.14f);
            choices.anchorMax = new Vector2(right ? 0.99f : 0.36f, 0.86f);
        }
        else
        {
            choices.anchorMin = Vector2.zero;
            choices.anchorMax = Vector2.one;
        }
        choices.offsetMin = choices.offsetMax = Vector2.zero;
        Color back = GameUi.Background;
        choices.GetComponent<Image>().color = coop ? new Color(back.r, back.g, back.b, 0.72f) : back;
        float left = coop ? 0.04f : 0.24f, right2 = coop ? 0.96f : 0.76f;
        foreach (Button button in buttons)
        {
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(left, rect.anchorMin.y);
            rect.anchorMax = new Vector2(right2, rect.anchorMax.y);
        }
        // Одиночное окно: «Перебросить» слева, «Запретить» справа; в кооперативе запрета нет (первый этап D06).
        RectTransform reroll = (RectTransform)rerollButton.transform;
        reroll.anchorMin = new Vector2(coop ? 0.12f : 0.24f, reroll.anchorMin.y);
        reroll.anchorMax = new Vector2(coop ? 0.88f : 0.495f, reroll.anchorMax.y);
        // Метка редкости с клавишей: в низкой карточке кооператива строке нужно больше высоты (иначе шрифт 12 не влезает).
        foreach (Text label in rarityLabels)
        {
            label.rectTransform.anchorMin = coop ? new Vector2(0.4f, 0.66f) : new Vector2(0.68f, 0.78f);
            label.rectTransform.anchorMax = coop ? new Vector2(0.97f, 0.99f) : new Vector2(0.96f, 0.96f);
        }
        choicesTitle.supportRichText = true;
        choicesTitle.fontSize = choicesTitle.resizeTextMaxSize = coop ? 26 : 36;
    }

    // Клавиши окна без паузы: И1 — 1/2/3 и R, И2 — Num1/2/3 и Num4 или геймпад X/Y/B и LB (CoopMode).
    private void ReadChoiceKeys()
    {
        int player = CoopMode.IndexOf(this);
        int card = CoopMode.CardPressed(player);
        if (card >= 0) SelectOfferedUpgrade(card);
        else if (CoopMode.RerollPressed(player)) Reroll();
    }

    // Окно с паузой (одиночная игра, сундук): 1/2/3 (и цифры на Num) выбирают карточку, R — переброс (не в сундуке).
    // Первые 0,25 с после появления или обновления карточек клавиши не действуют: нажатие из боя не выберет карточку вслепую.
    private const float ChoiceKeyGuard = 0.25f;
    private float choiceKeysReadyAt;

    private void ReadPausedChoiceKeys()
    {
        if (Time.unscaledTime < choiceKeysReadyAt || !ChoiceStateOk) return;
        int card = Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1) ? 0
            : Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2) ? 1
            : Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3) ? 2 : -1;
        if (card >= 0) SelectOfferedUpgrade(card);
        else if (!chestMode && Input.GetKeyDown(KeyCode.R)) Reroll();
        else if (!chestMode && Input.GetKeyDown(KeyCode.B)) ToggleBanishMode();
    }

    // Карточки в открытом окне напарника (кооператив): одновременно у игроков разные карточки.
    private HashSet<string> PartnerOffers()
    {
        var ids = new HashSet<string>();
        if (!CoopMode.Active) return ids;
        foreach (PlayerHealth hero in CoopMode.Heroes)
        {
            PlayerProgression other = hero != null ? hero.GetComponent<PlayerProgression>() : null;
            if (other == null || other == this || !other.Choosing) continue;
            foreach (UpgradeDefinition definition in other.Offered) if (definition != null) ids.Add(definition.id);
        }
        return ids;
    }

    public bool SelectOfferedUpgrade(int index)
    {
        if (!Choosing || index < 0 || index >= offered.Count || !ChoiceStateOk) return false;
        if (BanishMode) return BanishOffered(index);
        UpgradeDefinition definition = offered[index];
        if (!Available(definition)) return false;
        string gained = Preview(definition);   // «было → станет» до применения — для уведомления
        ranks[definition.id] = Rank(definition.id) + 1;
        if (definition.effect == UpgradeEffect.MaxHealth) GetComponent<PlayerHealth>().IncreaseMaxHealth(Mathf.RoundToInt(definition.amount));
        if (definition.effect == UpgradeEffect.OrbitCount) orbits.UpgradeOrbitCount(Mathf.RoundToInt(definition.amount));
        if (definition.effect == UpgradeEffect.Evolution) shooter.Evolve();
        if (definition.effect == UpgradeEffect.CursedGlass) GetComponent<PlayerHealth>().ReduceMaxHealth(0.3f);
        if (chestMode && definition.effect == UpgradeEffect.MaxHealth) GetComponent<PlayerHealth>().Heal(GetComponent<PlayerHealth>().maxHP);
        if (definition.effect == UpgradeEffect.ChestXp)
            for (int level = 0; level < Mathf.RoundToInt(definition.amount); level++)
                AddExperience(Mathf.Max(1, settings.ExperienceForLevel(Level) - Experience), true);
        ApplyStats();
        Color rarity = UpgradeCatalog.RarityColor(definition.rarity);
        toastText.text = "<b>" + definition.title + GameText.Source("playerprogression.196") + Rank(definition.id);
        toastText.color = rarity;
        toastDetail.text = gained;
        toastStripe.color = rarity;
        toastIcon.sprite = CardSprite(definition);
        toastIcon.enabled = toastIcon.sprite != null;
        toastVector.enabled = true;
        toastVector.ShowGlyph = toastIcon.sprite == null;
        toastVector.Effect = definition.effect;
        toastVector.color = rarity;
        // Уведомление выезжает чуть позже: выбранная карточка успевает долететь до него (UpgradeCardFx.Leave).
        toastTime = ToastLife + ToastDelay;
        toastGroup.alpha = 0f;
        toast.gameObject.SetActive(true);
        for (int i = 0; i < buttons.Length; i++)
            if (buttons[i] != null && buttons[i].gameObject.activeInHierarchy && cardFx[i] != null)
                cardFx[i].Leave(i == index, (RectTransform)canvas.transform, toastBadge);
        if (chestMode)
        {
            // Награда сундука не тратит обычные выборы уровня: закрываем окно и возвращаемся в бой.
            CloseChoices();
            if (GameSession.Instance != null) GameSession.Instance.LeaveUpgrade();
            else Time.timeScale = 1f;
            return true;
        }
        PendingChoices--;
        if (PendingChoices > 0)
        {
            RollOffers();
            if (offered.Count > 0) { RefreshCards(); return true; }
            PendingChoices = 0;
        }
        bool paused = !coopWindow;
        CloseChoices();
        if (!paused) return true;   // окно без паузы: бой и так идёт
        if (GameSession.Instance != null) GameSession.Instance.LeaveUpgrade();
        else Time.timeScale = 1f;
        return true;
    }

    // Окно с паузой выбирают в состоянии Upgrade; окно без паузы (кооператив) — в бою, а также пока открыт сундук напарника.
    private bool ChoiceStateOk
    {
        get
        {
            if (GameSession.Instance == null) return true;
            GameRunState state = GameSession.Instance.State;
            return coopWindow ? state == GameRunState.Playing || state == GameRunState.Upgrade : state == GameRunState.Upgrade;
        }
    }

    // --- Награда сундука (событие «Защити сундук»): три особых улучшения на выбор, сильнее обычных карточек.
    private bool chestMode;
    public bool ChestChoosing { get { return Choosing && chestMode; } }

    // --- Синергии (10.11.2026): карточка «Особое», берётся один раз, появляется только при обеих частях связки.
    public static readonly UpgradeDefinition[] SynergyUpgrades =
    {
        new UpgradeDefinition { id = "syn_steel_will", title = GameText.Source("playerprogression.197"), effect = UpgradeEffect.SteelWill, amount = 1f,
            rarity = UpgradeRarity.Special, maxRanks = 1, weight = 20 },
        // 19.11.2026: связки новых карточек.
        new UpgradeDefinition { id = "syn_crit_pierce", title = GameText.Source("cards.critpierce.title"), effect = UpgradeEffect.CritPierce, amount = 1f,
            rarity = UpgradeRarity.Special, maxRanks = 1, weight = 20 },
        new UpgradeDefinition { id = "syn_burning_shot", title = GameText.Source("cards.burningshot.title"), effect = UpgradeEffect.BurningShot, amount = 1f,
            rarity = UpgradeRarity.Special, maxRanks = 1, weight = 20 },
        new UpgradeDefinition { id = "syn_blast_dash", title = GameText.Source("cards.blastdash.title"), effect = UpgradeEffect.BlastDash, amount = 1f,
            rarity = UpgradeRarity.Special, maxRanks = 1, weight = 20 },
    };

    public bool SynergyReady(UpgradeDefinition definition)
    {
        switch (definition.effect)
        {
            case UpgradeEffect.SteelWill: return Bonus(UpgradeEffect.Armor) > 0f && Bonus(UpgradeEffect.Regeneration) > 0f;
            case UpgradeEffect.CritPierce: return Bonus(UpgradeEffect.CritChance) > 0f && Bonus(UpgradeEffect.Pierce) > 0f;
            case UpgradeEffect.BurningShot: return Bonus(UpgradeEffect.ExtraPellets) > 0f && Bonus(UpgradeEffect.BurnRounds) > 0f;
            case UpgradeEffect.BlastDash:
                return (Bonus(UpgradeEffect.DashCooldown) > 0f || Bonus(UpgradeEffect.DashDistance) > 0f || Bonus(UpgradeEffect.DashShield) > 0f) &&
                    (Bonus(UpgradeEffect.ExplosiveBullets) > 0f || Bonus(UpgradeEffect.GrenadeRadius) > 0f);
            default: return false;
        }
    }

    public static readonly UpgradeDefinition[] ChestUpgrades =
    {
        Chest("chest_rage", GameText.Source("playerprogression.198"), UpgradeEffect.Damage, 0.4f),
        Chest("chest_haste", GameText.Source("playerprogression.199"), UpgradeEffect.AttackSpeed, 0.35f),
        Chest("chest_sniper", GameText.Source("playerprogression.200"), UpgradeEffect.CritChance, 0.25f),
        Chest("chest_plate", GameText.Source("playerprogression.201"), UpgradeEffect.Armor, 0.2f),
        Chest("chest_heart", GameText.Source("playerprogression.202"), UpgradeEffect.MaxHealth, 40f),
        Chest("chest_vein", GameText.Source("playerprogression.203"), UpgradeEffect.ChestXp, 2f),
        Chest("chest_spring", GameText.Source("playerprogression.204"), UpgradeEffect.Regeneration, 2f),
        Chest("chest_boots", GameText.Source("playerprogression.205"), UpgradeEffect.Movement, 0.3f),
        Chest("chest_leech", GameText.Source("playerprogression.206"), UpgradeEffect.Vampirism, 2f),
    };

    private static UpgradeDefinition Chest(string id, string title, UpgradeEffect effect, float amount)
    {
        return new UpgradeDefinition { id = id, title = title, effect = effect, amount = amount, rarity = UpgradeRarity.Special, maxRanks = 3 };
    }

    // --- Эволюция оружия (11.11.2026): карточка «ЭВОЛЮЦИЯ: …» только в награде сундука, когда готова (AutoShoter.Evolutions).
    public static readonly UpgradeDefinition[] EvolutionUpgrades = BuildEvolutions();

    private static UpgradeDefinition[] BuildEvolutions()
    {
        var cards = new UpgradeDefinition[AutoShoter.Evolutions.Length];
        for (int i = 0; i < cards.Length; i++) cards[i] = Evolution(i);
        return cards;
    }

    private static UpgradeDefinition Evolution(int weapon)
    {
        return new UpgradeDefinition { id = "evo_" + weapon, title = GameText.Source("playerprogression.207") + AutoShoter.Evolutions[weapon].title,
            effect = UpgradeEffect.Evolution, amount = 1f, rarity = UpgradeRarity.Special, maxRanks = 1 };
    }

    public const int EvolutionDamageRanks = 5;

    // Сколько рангов взято у карточек с этим эффектом в этом забеге.
    public int EffectRanks(UpgradeEffect effect)
    {
        int sum = 0;
        if (catalog != null)
            foreach (UpgradeDefinition definition in catalog.entries)
                if (definition != null && definition.effect == effect) sum += Rank(definition.id);
        return sum;
    }

    // Название карточки каталога с этим эффектом (подсказки рецепта эволюции).
    public string EffectTitle(UpgradeEffect effect)
    {
        if (catalog != null)
            foreach (UpgradeDefinition definition in catalog.entries)
                if (definition != null && definition.effect == effect) return definition.title;
        return effect.ToString();
    }

    // Карточка входит в рецепт эволюции текущего оружия и её ранг ещё не добран (21.11.2026).
    public bool FeedsEvolution(UpgradeDefinition definition)
    {
        if (definition == null || shooter == null || !shooter.CanEvolve) return false;
        AutoShoter.EvolutionData evo = AutoShoter.Evolutions[shooter.EvolutionSlot];
        return definition.effect == UpgradeEffect.Damage && EffectRanks(UpgradeEffect.Damage) < EvolutionDamageRanks
            || definition.effect == evo.partner && EffectRanks(evo.partner) < evo.partnerRanks;
    }

    // Эволюция сохраняет модель оружия; остальные карточки используют отдельный арт с векторным fallback.
    private static Sprite CardSprite(UpgradeDefinition definition)
    {
        if (definition.effect == UpgradeEffect.Evolution && definition.icon != null) return definition.icon;
        return UiArtwork.Upgrade(definition.effect);
    }

    // Карточка эволюции текущего оружия, если условия выполнены (иначе null).
    public UpgradeDefinition ReadyEvolution()
    {
        if (shooter == null || !shooter.CanEvolve) return null;
        AutoShoter.EvolutionData evo = AutoShoter.Evolutions[shooter.EvolutionSlot];
        if (EffectRanks(UpgradeEffect.Damage) < EvolutionDamageRanks || EffectRanks(evo.partner) < evo.partnerRanks) return null;
        UpgradeDefinition card = EvolutionUpgrades[shooter.EvolutionSlot];
        if (card.icon == null) card.icon = Resources.Load<Sprite>("UI/Weapons/" + ModelSkins.WeaponKey(shooter.baseWeapon, shooter.WeaponIndex));
        return card;
    }

    // Открывает окно выбора из трёх случайных особых улучшений. false — окно открыть нельзя (уже выбирают или нет карточек).
    public bool OfferChestReward()
    {
        if (Choosing || choices == null) return false;
        var pool = new List<UpgradeDefinition>();
        foreach (UpgradeDefinition definition in ChestUpgrades) if (Available(definition)) pool.Add(definition);
        if (pool.Count == 0) return false;
        offered.Clear();
        while (offered.Count < 3 && pool.Count > 0)
        {
            int pick = Random.Range(0, pool.Count);
            offered.Add(pool[pick]);
            pool.RemoveAt(pick);
        }
        // Готовая эволюция занимает первое место в награде сундука.
        UpgradeDefinition evolution = ReadyEvolution();
        if (evolution != null)
        {
            if (offered.Count >= 3) offered[0] = evolution;
            else offered.Insert(0, evolution);
        }
        if (GameSession.Instance != null && !GameSession.Instance.EnterUpgrade()) { offered.Clear(); return false; }
        chestMode = true;
        Choosing = true;
        coopWindow = false;
        Time.timeScale = 0f;
        ApplyChoiceLayout(false);
        choicesTitle.text = GameText.Source("playerprogression.208");
        choicesTitle.color = Color.white;
        choices.gameObject.SetActive(true);
        RefreshCards();
        return true;
    }

    // Тест-арена: выдать улучшение без окна выбора (минимальный уровень карточки не важен). false — недоступно.
    public bool TestGrant(UpgradeDefinition definition)
    {
        if (!Available(definition, true)) return false;
        ranks[definition.id] = Rank(definition.id) + 1;
        if (definition.effect == UpgradeEffect.MaxHealth) GetComponent<PlayerHealth>().IncreaseMaxHealth(Mathf.RoundToInt(definition.amount));
        if (definition.effect == UpgradeEffect.OrbitCount) orbits.UpgradeOrbitCount(Mathf.RoundToInt(definition.amount));
        if (definition.effect == UpgradeEffect.Evolution) shooter.Evolve();
        if (definition.effect == UpgradeEffect.CursedGlass) GetComponent<PlayerHealth>().ReduceMaxHealth(0.3f);
        if (definition.effect == UpgradeEffect.ChestXp)
            for (int level = 0; level < Mathf.RoundToInt(definition.amount); level++)
                AddExperience(Mathf.Max(1, settings.ExperienceForLevel(Level) - Experience), true);
        ApplyStats();
        return true;
    }

    // Тест-арена: повысить уровень (с обычным окном выбора карточек).
    public void TestLevelUp()
    {
        AddExperience(Mathf.Max(1, settings.ExperienceForLevel(Level) - Experience), true);
    }

    private void Choose(int index) { SelectOfferedUpgrade(index); }

    // Writes every upgrade that lives in CombatStats (before the weapon recalculates its own numbers).
    private void ApplyCombatBonuses()
    {
        float glass = Bonus(UpgradeEffect.CursedGlass), haste = Bonus(UpgradeEffect.CursedHaste), greed = Bonus(UpgradeEffect.CursedGreed);
        float timeGreed = Bonus(UpgradeEffect.TimeGreed);
        Stats.Gamble = Bonus(UpgradeEffect.Gamble) > 0f;
        // «Азарт»: +100% удачи, но аптечки становятся магнитами (DropPickup).
        float luck = Bonus(UpgradeEffect.Luck) + (Stats.Gamble ? 1f : 0f);
        // Удача добавляет и шанс крита: +10% удачи → +1% крита.
        Stats.Set(Bonus(UpgradeEffect.CritChance) + 0.1f * luck + (WorkshopProjects.HasRelic(4) ? .08f * WorkshopProjects.RelicPower : 0f), Bonus(UpgradeEffect.CritPower), Mathf.RoundToInt(Bonus(UpgradeEffect.Pierce)),
            Mathf.RoundToInt(Bonus(UpgradeEffect.Ricochet)));
        bool drone = Bonus(UpgradeEffect.DroneCount) > 0f;
        Stats.Armor = Mathf.Min(0.6f, Bonus(UpgradeEffect.Armor) + (WorkshopProjects.HasRelic(1) ? .06f * WorkshopProjects.RelicPower : 0f) + (drone && WorkshopProjects.DroneMode == 1 ? .03f * WorkshopProjects.Rank("drones") : 0f));
        Stats.RegenPerSecond = Bonus(UpgradeEffect.Regeneration) + (WorkshopProjects.HasRelic(2) ? .5f * WorkshopProjects.RelicPower : 0f);
        Stats.SteelWill = Bonus(UpgradeEffect.SteelWill) > 0f;
        Stats.VampirismPerKill = Bonus(UpgradeEffect.Vampirism);
        Stats.DashCooldownReduction = Bonus(UpgradeEffect.DashCooldown);
        Stats.DashDistanceBonus = Bonus(UpgradeEffect.DashDistance);
        Stats.ExtraPellets = Mathf.RoundToInt(Bonus(UpgradeEffect.ExtraPellets));
        Stats.ExplosionRadius = 1.2f * Bonus(UpgradeEffect.ExplosiveBullets);
        Stats.EliteDamage = Bonus(UpgradeEffect.EliteDamage);
        Stats.RangeMultiplier = 1f + Bonus(UpgradeEffect.WeaponRange);
        Stats.OrbitSpeedBonus = Bonus(UpgradeEffect.OrbitSpeed);
        Stats.OrbitCritShare = Bonus(UpgradeEffect.OrbitCrit);
        Stats.PickupSpeedBonus = Bonus(UpgradeEffect.PickupSpeed);
        Stats.DropLuck = luck;
        Stats.Dodge = Mathf.Min(0.25f, Bonus(UpgradeEffect.Dodge));
        Stats.ProjectileGuard = Mathf.Min(0.4f, Bonus(UpgradeEffect.ProjectileGuard));
        Stats.DashInvulnerabilityBonus = Bonus(UpgradeEffect.DashShield);
        Stats.TrophyParts = Mathf.RoundToInt(Bonus(UpgradeEffect.Trophy));
        Stats.BurnRank = Mathf.RoundToInt(Bonus(UpgradeEffect.BurnRounds));
        Stats.PoisonRank = Mathf.RoundToInt(Bonus(UpgradeEffect.PoisonRounds));
        Stats.FrostRank = Mathf.RoundToInt(Bonus(UpgradeEffect.FrostRounds));
        Stats.ShockRank = Mathf.RoundToInt(Bonus(UpgradeEffect.ShockRounds));
        Stats.SplitShot = HeroCardEffects.SplitFragments(Mathf.RoundToInt(Bonus(UpgradeEffect.SplitShot)));
        Stats.Retaliate = Mathf.RoundToInt(Bonus(UpgradeEffect.Retaliate));
        Stats.PulseAura = Mathf.RoundToInt(Bonus(UpgradeEffect.PulseAura));
        Stats.ChargedShot = Mathf.RoundToInt(Bonus(UpgradeEffect.ChargedShot));
        Stats.LastChance = Bonus(UpgradeEffect.LastChance) > 0f;
        Stats.CritPierce = Bonus(UpgradeEffect.CritPierce) > 0f;
        Stats.BurningShot = Bonus(UpgradeEffect.BurningShot) > 0f;
        Stats.BlastDash = Bonus(UpgradeEffect.BlastDash) > 0f;
        // Cursed upgrades: a large bonus with a price (see CursedText).
        Stats.ExperienceGain = Bonus(UpgradeEffect.ExperienceGain) + 0.5f * greed + 0.3f * timeGreed - (WorkshopProjects.HasRelic(2) ? .05f : 0f);
        Stats.EnemySpeedCurse = 1f + 0.15f * haste + 0.15f * timeGreed;
        Stats.EnemyHealthCurse = 1f + 0.25f * greed;
        cursedDamage = 1.0f * glass;
        cursedAttackSpeed = 0.4f * haste;
    }

    private float cursedDamage, cursedAttackSpeed;

    private void ApplyStats()
    {
        ApplyCombatBonuses();
        DamageMultiplier = Mathf.Min(Mathf.Max(1f, Cap(UpgradeEffect.Damage)), 1f + Bonus(UpgradeEffect.Damage) + cursedDamage + .05f * WorkshopProjects.Mastery(shooter.WeaponIndex));
        AttackSpeedMultiplier = Mathf.Min(Mathf.Max(1f, Cap(UpgradeEffect.AttackSpeed)), 1f + Bonus(UpgradeEffect.AttackSpeed) + cursedAttackSpeed);
        if (WorkshopProjects.HasRelic(4)) AttackSpeedMultiplier *= .95f;
        shooter.SetProgressionMultipliers(DamageMultiplier, AttackSpeedMultiplier);
        shooter.SetGrenadeUnlocked(Bonus(UpgradeEffect.GrenadeDamage) > 0f || Bonus(UpgradeEffect.GrenadeRadius) > 0f);
        shooter.SetSecondaryMultipliers(Mathf.Min(5f, 1f + Bonus(UpgradeEffect.GrenadeDamage)),
            Mathf.Min(2.5f, 1f + Bonus(UpgradeEffect.GrenadeRadius)));
        movement.SpeedMultiplier = Mathf.Min(Mathf.Max(1f, Cap(UpgradeEffect.Movement)), 1f + Bonus(UpgradeEffect.Movement));
        if (WorkshopProjects.HasRelic(1)) movement.SpeedMultiplier *= .95f;
        PickupRadiusMultiplier = Mathf.Min(Mathf.Max(1f, Cap(UpgradeEffect.Pickup)), 1f + Bonus(UpgradeEffect.Pickup));
        if (Bonus(UpgradeEffect.DroneCount) > 0f && WorkshopProjects.DroneMode == 2) PickupRadiusMultiplier += .15f * WorkshopProjects.Rank("drones");
        if (orbits != null) orbits.SetDamageMultiplier(Mathf.Min(5f, 1f + Bonus(UpgradeEffect.OrbitDamage)));
        if (orbits != null) orbits.SetSpeedBonus(Stats.OrbitSpeedBonus);
        shooter.SetSupportUnits(Mathf.RoundToInt(Bonus(UpgradeEffect.DroneCount)), Mathf.RoundToInt(Bonus(UpgradeEffect.TurretCount)),
            Mathf.RoundToInt(Bonus(UpgradeEffect.MineCount)), 1f + Bonus(UpgradeEffect.DroneDamage) + (WorkshopProjects.DroneMode == 0 ? .1f * WorkshopProjects.Rank("drones") : 0f) + (WorkshopProjects.HasRelic(3) ? .2f * WorkshopProjects.RelicPower : 0f), 1f + Bonus(UpgradeEffect.TurretDamage));
    }

    public void CloseChoices()
    {
        BanishMode = false;
        foreach (UpgradeCardFx fx in cardFx) if (fx != null) fx.banishHighlight = false;
        chestMode = false;
        coopWindow = false;
        if (choicesTitle != null) choicesTitle.text = GameText.Source("playerprogression.195");
        Choosing = false;
        if (choices != null) choices.gameObject.SetActive(false);
    }

    private string Preview(UpgradeDefinition definition)
    {
        // Только «что даст» (было → станет); ранг показывает RankLine отдельной мелкой строкой.
        const string rankText = "";
        if (definition.effect == UpgradeEffect.MaxHealth)
            return rankText + GetComponent<PlayerHealth>().maxHP + " → " + (GetComponent<PlayerHealth>().maxHP + Mathf.RoundToInt(definition.amount)) + " HP";
        if (definition.effect == UpgradeEffect.OrbitCount)
            return rankText + GameText.Source("playerprogression.209") + orbits.orbitCount + " → " + (orbits.orbitCount + Mathf.RoundToInt(definition.amount));
        if (definition.effect == UpgradeEffect.CritChance)
            return rankText + GameText.Source("playerprogression.210") + Mathf.RoundToInt(Bonus(definition.effect) * 100f) + "% → " +
                Mathf.RoundToInt((Bonus(definition.effect) + definition.amount) * 100f) + "%";
        if (definition.effect == UpgradeEffect.CritPower)
            return rankText + GameText.Source("playerprogression.211") + (CombatStats.BaseCritMultiplier + Bonus(definition.effect)).ToString("0.##") + " → ×" +
                (CombatStats.BaseCritMultiplier + Bonus(definition.effect) + definition.amount).ToString("0.##");
        string extra = NewPreview(definition);
        if (extra != null) return rankText + extra;
        if (definition.effect == UpgradeEffect.Ricochet)
            return rankText + GameText.Source("playerprogression.212") + Mathf.RoundToInt(Bonus(definition.effect)) + " → " +
                Mathf.RoundToInt(Bonus(definition.effect) + definition.amount) + GameText.Source("playerprogression.213");
        if (definition.effect == UpgradeEffect.DroneCount || definition.effect == UpgradeEffect.TurretCount || definition.effect == UpgradeEffect.MineCount)
        {
            // Сапёрные мины: 1-й ранг даёт 3 мины, каждый следующий +1.
            int now = Mathf.RoundToInt(Bonus(definition.effect));
            int next = now + Mathf.RoundToInt(definition.amount);
            if (definition.effect == UpgradeEffect.MineCount) { now = SpecialWeapons.SupportMines(now); next = SpecialWeapons.SupportMines(next); }
            return rankText + (definition.effect == UpgradeEffect.DroneCount ? GameText.Source("playerprogression.214") : definition.effect == UpgradeEffect.TurretCount ? GameText.Source("playerprogression.215") : GameText.Source("playerprogression.216")) + now + " → " + next;
        }
        if (definition.effect == UpgradeEffect.Pierce)
            return rankText + GameText.Source("playerprogression.217") + Mathf.RoundToInt(Bonus(definition.effect)) + " → " +
                Mathf.RoundToInt(Bonus(definition.effect) + definition.amount);
        float before = Mathf.Min(Mathf.Max(1f, Cap(definition.effect)), 1f + Bonus(definition.effect));
        float after = Mathf.Min(Mathf.Max(1f, Cap(definition.effect)), before + definition.amount);
        // Множитель показываем как прибавку с подписью: «Урон: +20% → +40%» вместо «×1,2 → ×1,4».
        return rankText + EffectLabel(definition.effect) + ": +" + Mathf.RoundToInt((before - 1f) * 100f) + "% → +" +
            Mathf.RoundToInt((after - 1f) * 100f) + "%";
    }

    private static string EffectLabel(UpgradeEffect effect)
    {
        switch (effect)
        {
            case UpgradeEffect.Damage: return GameText.Source("playerprogression.218");
            case UpgradeEffect.AttackSpeed: return GameText.Source("playerprogression.219");
            case UpgradeEffect.Movement: return GameText.Source("playerprogression.220");
            case UpgradeEffect.Pickup: return GameText.Source("playerprogression.221");
            case UpgradeEffect.GrenadeDamage: return GameText.Source("playerprogression.222");
            case UpgradeEffect.GrenadeRadius: return GameText.Source("playerprogression.223");
            case UpgradeEffect.OrbitDamage: return GameText.Source("playerprogression.224");
            case UpgradeEffect.MineCount: return GameText.Source("metaprogress.182");
            case UpgradeEffect.DroneDamage: return GameText.Source("playerprogression.225");
            case UpgradeEffect.TurretDamage: return GameText.Source("playerprogression.226");
            default: return GameText.Source("playerprogression.227");
        }
    }

    // Ранг точками: ●●●○○ и подпись «новое» / «ранг 2 → 3».
    private string RankLine(UpgradeDefinition definition)
    {
        int rank = Rank(definition.id);
        int max = Mathf.Max(1, definition.maxRanks);
        string pips = "";
        if (max <= 10)
            for (int i = 0; i < max; i++)
                pips += i < rank ? "<color=#5FD9B8>●</color>" : i == rank ? "<color=#FFFFFF>●</color>" : "○";
        string caption = rank == 0 ? GameText.Source("playerprogression.228") : GameText.Source("playerprogression.229") + rank + " → " + (rank + 1) + GameText.Source("playerprogression.230") + max;
        return (pips.Length > 0 ? pips + "   " : "") + caption;
    }

    private static string Pair(string label, float from, float to, string suffix, string format = "0.##")
    {
        return label + ": " + from.ToString(format) + suffix + " → " + to.ToString(format) + suffix;
    }

    // Card text for the upgrades without an ordinary multiplier (null = not one of them).
    private string NewPreview(UpgradeDefinition definition)
    {
        float now = Bonus(definition.effect), next = now + definition.amount;
        switch (definition.effect)
        {
            case UpgradeEffect.Armor: return Pair(GameText.Source("playerprogression.231"), now * 100f, Mathf.Min(0.6f, next) * 100f, "%", "0");
            case UpgradeEffect.Regeneration: return Pair(GameText.Source("playerprogression.232"), now, next, GameText.Source("playerprogression.233"));
            case UpgradeEffect.Vampirism: return Pair(GameText.Source("playerprogression.234"), now, next, "");
            case UpgradeEffect.DashCooldown: return Pair(GameText.Source("playerprogression.235"), Mathf.Max(1f, movement.dashCooldown - now), Mathf.Max(1f, movement.dashCooldown - next), GameText.Source("arenaevents.006"));
            case UpgradeEffect.DashDistance: return Pair(GameText.Source("playerprogression.236"), movement.dashDistance + now, movement.dashDistance + next, GameText.Source("arenaevents.040"));
            case UpgradeEffect.ExtraPellets: return Pair(GameText.Source("playerprogression.237"), now, next, "", "0");
            case UpgradeEffect.ExplosiveBullets: return Pair(GameText.Source("playerprogression.238"), now * 1.2f, next * 1.2f, GameText.Source("arenaevents.040"));
            case UpgradeEffect.EliteDamage: return Pair(GameText.Source("playerprogression.239"), now * 100f, next * 100f, "%", "0");
            case UpgradeEffect.WeaponRange: return Pair(GameText.Source("playerprogression.240"), now * 100f, next * 100f, "%", "0");
            case UpgradeEffect.OrbitSpeed: return Pair(GameText.Source("playerprogression.241"), now * 100f, next * 100f, "%", "0");
            case UpgradeEffect.OrbitCrit: return Pair(GameText.Source("playerprogression.242"), now * 100f, Mathf.Min(1f, next) * 100f, "%", "0");
            case UpgradeEffect.PickupSpeed: return Pair(GameText.Source("playerprogression.243"), now * 100f, next * 100f, "%", "0");
            case UpgradeEffect.Luck: return Pair(GameText.Source("playerprogression.244"), now * 100f, next * 100f, "%", "0");
            case UpgradeEffect.ExperienceGain: return Pair(GameText.Source("playerprogression.245"), now * 100f, next * 100f, "%", "0");
            case UpgradeEffect.Evolution: return AutoShoter.Evolutions[Mathf.Max(0, shooter.EvolutionSlot)].text;
            case UpgradeEffect.SteelWill: return GameText.Source("playerprogression.246");
            case UpgradeEffect.ChestXp: return GameText.Source("playerprogression.247") + Mathf.RoundToInt(definition.amount) + GameText.Source("playerprogression.248");
            case UpgradeEffect.CursedGlass: return GameText.Source("playerprogression.249");
            case UpgradeEffect.CursedHaste: return GameText.Source("playerprogression.250");
            case UpgradeEffect.CursedGreed: return GameText.Source("playerprogression.251");
            case UpgradeEffect.Dodge: return Pair(GameText.Source("cards.dodge.label"), now * 100f, Mathf.Min(0.25f, next) * 100f, "%", "0");
            case UpgradeEffect.ProjectileGuard: return Pair(GameText.Source("cards.projectileguard.label"), now * 100f, Mathf.Min(0.4f, next) * 100f, "%", "0");
            case UpgradeEffect.DashShield: return Pair(GameText.Source("cards.dashshield.label"), movement.dashInvulnerability + now, movement.dashInvulnerability + next, GameText.Source("arenaevents.006"));
            case UpgradeEffect.Trophy: return Pair(GameText.Source("cards.trophy.label"), now, next, "", "0");
            case UpgradeEffect.BurnRounds: return Pair(GameText.Source("cards.burn.label"), HeroCardEffects.BurnShare(Mathf.RoundToInt(now)) * 100f, HeroCardEffects.BurnShare(Mathf.RoundToInt(next)) * 100f, "%", "0");
            case UpgradeEffect.PoisonRounds: return Pair(GameText.Source("cards.poison.label"), HeroCardEffects.PoisonShare(Mathf.RoundToInt(now)) * 100f, HeroCardEffects.PoisonShare(Mathf.RoundToInt(next)) * 100f, "%", "0.#");
            case UpgradeEffect.FrostRounds: return Pair(GameText.Source("cards.frost.label"), HeroCardEffects.FrostSlow(Mathf.RoundToInt(now)) * 100f, HeroCardEffects.FrostSlow(Mathf.RoundToInt(next)) * 100f, "%", "0");
            case UpgradeEffect.ShockRounds: return Pair(GameText.Source("cards.shock.label"), now, next, "", "0");
            case UpgradeEffect.SplitShot: return Pair(GameText.Source("cards.split.label"), HeroCardEffects.SplitFragments(Mathf.RoundToInt(now)), HeroCardEffects.SplitFragments(Mathf.RoundToInt(next)), "", "0");
            case UpgradeEffect.Retaliate: return Pair(GameText.Source("cards.retaliate.label"), HeroCardEffects.RetaliateShare(Mathf.RoundToInt(now)) * 100f, HeroCardEffects.RetaliateShare(Mathf.RoundToInt(next)) * 100f, "%", "0");
            case UpgradeEffect.PulseAura: return Pair(GameText.Source("cards.aura.label"), HeroCardEffects.AuraShare(Mathf.RoundToInt(now)) * 100f, HeroCardEffects.AuraShare(Mathf.RoundToInt(next)) * 100f, "%", "0");
            case UpgradeEffect.ChargedShot: return Pair(GameText.Source("cards.charged.label"), HeroCardEffects.ChargedEvery(Mathf.RoundToInt(now)), HeroCardEffects.ChargedEvery(Mathf.RoundToInt(next)), "", "0");
            case UpgradeEffect.LastChance: return GameText.Source("cards.lastchance.text");
            case UpgradeEffect.Gamble: return GameText.Source("cards.gamble.text");
            case UpgradeEffect.TimeGreed: return GameText.Source("cards.timegreed.text");
            case UpgradeEffect.CritPierce: return GameText.Source("cards.critpierce.text");
            case UpgradeEffect.BurningShot: return GameText.Source("cards.burningshot.text");
            case UpgradeEffect.BlastDash: return GameText.Source("cards.blastdash.text");
            default: return null;
        }
    }

    // Level-up screen: swap the three offered cards for new ones (a limited number per run).
    public int RerollsLeft { get; private set; } = 2;

    public bool Reroll()
    {
        if (!Choosing || chestMode || RerollsLeft <= 0 || !ChoiceStateOk) return false;
        RollOffers();
        if (offered.Count == 0) return false;
        RerollsLeft--;
        SetBanishMode(false);
        RefreshCards();
        return true;
    }

    // --- Запрет карточки (D06, 21.11.2026): 2 заряда на забег у каждого героя (объект героя создаётся заново в каждом забеге).
    // Запрещённый id больше не выпадает до конца забега. Только обычное окно уровня: не сундук и не кооператив.
    private readonly HashSet<string> banished = new HashSet<string>();
    public int BanishesLeft { get; private set; } = 2;
    public bool BanishMode { get; private set; }
    public bool IsBanished(string id) { return id != null && banished.Contains(id); }
    private bool BanishAllowed => Choosing && !chestMode && !coopWindow;
    private static readonly Color BanishColor = new Color(1f, 0.55f, 0.2f);

    public bool ToggleBanishMode()
    {
        if (!BanishAllowed || !ChoiceStateOk || (!BanishMode && BanishesLeft <= 0)) return false;
        SetBanishMode(!BanishMode);
        choiceKeysReadyAt = Time.unscaledTime + ChoiceKeyGuard;
        return true;
    }

    private void SetBanishMode(bool on)
    {
        BanishMode = on && BanishAllowed;
        if (choicesTitle != null && Choosing && !chestMode)
        {
            choicesTitle.text = BanishMode ? GameText.Source("playerprogression.banish.hint") : LevelTitle;
            choicesTitle.color = BanishMode ? BanishColor
                : coopWindow ? CoopMode.Colors[Mathf.Clamp(CoopMode.IndexOf(this), 0, 1)] : Color.white;
        }
        foreach (UpgradeCardFx fx in cardFx) if (fx != null) fx.banishHighlight = BanishMode;
        RefreshBanishButton();
    }

    private bool BanishOffered(int index)
    {
        if (!BanishAllowed || BanishesLeft <= 0 || index < 0 || index >= offered.Count) { SetBanishMode(false); return false; }
        banished.Add(offered[index].id);
        BanishesLeft--;
        offered.RemoveAt(index);
        // Замена из пула без карточек, уже лежащих на столе; если замен нет — слот просто пропадает.
        List<UpgradeDefinition> pool = Candidates();
        pool.RemoveAll(definition => offered.Exists(other => other.id == definition.id));
        if (pool.Count > 0) offered.Insert(index, pool[WeightedIndex(pool)]);
        SetBanishMode(false);
        if (offered.Count == 0)
        {
            // Карточек не осталось вовсе: закрываем окно, чтобы забег не завис на пустом выборе.
            PendingChoices = 0;
            CloseChoices();
            if (GameSession.Instance != null) GameSession.Instance.LeaveUpgrade();
            else Time.timeScale = 1f;
            return true;
        }
        RefreshCards();
        return true;
    }

    private void RefreshBanishButton()
    {
        if (banishButton == null) return;
        bool show = Choosing && !chestMode && !coopWindow;
        banishButton.gameObject.SetActive(show);
        if (!show) return;
        banishButton.interactable = BanishesLeft > 0 || BanishMode;
        banishButton.GetComponent<Image>().color = BanishMode ? new Color(0.62f, 0.2f, 0.06f) : new Color(0.3f, 0.12f, 0.08f);
        banishButton.GetComponentInChildren<Text>().text = BanishMode
            ? GameText.Source("playerprogression.banish.cancel")
            : GameText.Source("playerprogression.banish.button") + BanishesLeft + ")";
    }

    private void RefreshCards()
    {
        choiceKeysReadyAt = Time.unscaledTime + ChoiceKeyGuard;
        if (rerollButton != null)
        {
            rerollButton.gameObject.SetActive(!chestMode);
            rerollButton.interactable = RerollsLeft > 0;
            rerollButton.GetComponentInChildren<Text>().text = coopWindow
                ? GameText.Source("playerprogression.252") + CoopMode.RerollKey(CoopMode.IndexOf(this)) + "]  (" + RerollsLeft + ")"
                : GameText.Source("playerprogression.253") + RerollsLeft + ")";
        }
        foreach (UpgradeCardFx fx in cardFx) if (fx != null) fx.banishHighlight = BanishMode;
        RefreshBanishButton();
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].gameObject.SetActive(i < offered.Count);
            if (i >= offered.Count) continue;
            UpgradeDefinition definition = offered[i];
            Color color = UpgradeCatalog.RarityColor(definition.rarity);
            buttons[i].GetComponent<Image>().color = new Color(0.035f + color.r * 0.06f, 0.045f + color.g * 0.06f, 0.065f + color.b * 0.06f);
            string hex = ColorUtility.ToHtmlStringRGB(Color.Lerp(color, Color.white, 0.35f));
            // Компактное окно кооператива — мельче шрифт, чтобы строки не переносились за край карточки.
            int titleSize = coopWindow ? 19 : 22, previewSize = coopWindow ? 15 : 17, rankSize = coopWindow ? 12 : 13;
            // Карточка из рецепта эволюции текущего оружия — золотая пометка над названием.
            string evoTag = FeedsEvolution(definition)
                ? "<size=" + rankSize + "><color=#FFC740><b>★ " + GameText.Source("playerprogression.207") +
                  AutoShoter.Evolutions[shooter.EvolutionSlot].title + "</b></color></size>\n"
                : "";
            cardText[i].text = evoTag + "<size=" + titleSize + "><b>" + definition.title + "</b></size>\n<size=" + previewSize + "><color=#" + hex + ">" +
                Preview(definition) + "</color></size>\n<size=" + rankSize + "><color=#8796A8>" + RankLine(definition) + "</color></size>";
            string key = "[" + (coopWindow ? CoopMode.CardKey(CoopMode.IndexOf(this), i) : (i + 1).ToString()) + "]   ";
            rarityLabels[i].text = key + UpgradeCatalog.RarityName(definition.rarity).ToUpperInvariant();
            rarityLabels[i].color = color;
            if (definition.effect == UpgradeEffect.Evolution) { rarityLabels[i].text = key + GameText.Source("playerprogression.254"); rarityLabels[i].color = new Color(1f, 0.78f, 0.25f); }
            icons[i].sprite = CardSprite(definition);
            icons[i].enabled = icons[i].sprite != null;
            vectorIcons[i].enabled = true;
            vectorIcons[i].ShowGlyph = icons[i].sprite == null;
            vectorIcons[i].Effect = definition.effect;
            vectorIcons[i].color = color;
            iconBadges[i].color = new Color(0.02f + color.r * 0.035f, 0.03f + color.g * 0.035f, 0.045f + color.b * 0.035f, 1f);
            accentLines[i].color = new Color(color.r, color.g, color.b, 0.7f);
            Outline ring = iconBadges[i].GetComponent<Outline>();
            if (ring != null) ring.effectColor = new Color(color.r, color.g, color.b, 0.85f);
            cardFx[i].Present(color, i, definition.rarity, definition.effect == UpgradeEffect.Evolution);
        }
    }

    private void RefreshUI()
    {
        if (status == null) return;
        int required = settings.ExperienceForLevel(Level);
        status.text = Experience + " / " + required;
        targetExperience = Mathf.Clamp01((float)Experience / Mathf.Max(1, required));
        // A level starts a new scale: never animate backwards through the previous level's XP.
        if (hudLevel != Level)
        {
            hudLevel = Level;
            shownExperience = targetExperience;
            DrawExperience();
        }
        if (levelText != null) levelText.text = Level.ToString();
    }

    // Уведомление о карточке: значок «впрыгивает» с покачиванием, по плашке проходит блик, полоса редкости тает со временем.
    private void AnimateToast(float age)
    {
        if (toastBadge == null) return;
        float t = Mathf.Max(0f, age);
        float scale = t < 0.12f ? Mathf.Lerp(0.3f, 1.25f, t / 0.12f) : 1f + 0.25f * Mathf.Exp(-(t - 0.12f) * 9f) * Mathf.Cos((t - 0.12f) * 22f);
        toastBadge.localScale = Vector3.one * scale;
        toastBadge.localRotation = Quaternion.Euler(0f, 0f, 12f * Mathf.Exp(-t * 6f) * Mathf.Sin(t * 26f));
        float sweep = Mathf.Clamp01((t - 0.15f) / 0.55f);
        toastShine.anchorMin = new Vector2(Mathf.Lerp(-0.1f, 1.1f, sweep), -0.3f);
        toastShine.anchorMax = new Vector2(toastShine.anchorMin.x, 1.3f);
        toastShine.gameObject.SetActive(sweep > 0f && sweep < 1f);
        RectTransform stripe = toastStripe.rectTransform;
        stripe.anchorMax = new Vector2(Mathf.Lerp(0.02f, 0.98f, Mathf.Clamp01(toastTime / ToastLife)), stripe.anchorMax.y);
    }

    private void LateUpdate()
    {
        if (fill == null || shownExperience == targetExperience) return;
        shownExperience = Mathf.MoveTowards(shownExperience, targetExperience, Time.unscaledDeltaTime * 2.5f);
        DrawExperience();
    }

    private void DrawExperience()
    {
        fill.rectTransform.anchorMax = new Vector2(shownExperience, 1f);
        fill.gameObject.SetActive(shownExperience > 0f);
    }

    private void BuildUI()
    {
        canvas = GameUi.Canvas("Experience HUD", 80);
        // Segmented scale: one level diamond, separate captions and 18 visual divisions (not XP units).
        RectTransform bar = GameUi.Box(canvas.transform, "Experience", new Vector2(0.22f, 0.925f), new Vector2(0.78f, 0.985f), Color.clear);
        RectTransform body = GameUi.Box(bar, "Experience body", Vector2.zero, Vector2.one, Color.clear);
        body.offsetMin = new Vector2(64f, 0f);
        RectTransform track = GameUi.Box(body, "Experience track", new Vector2(0f, 0.12f), new Vector2(1f, 0.43f), new Color(0.07f, 0.13f, 0.15f, 1f));
        track.gameObject.AddComponent<RectMask2D>();
        fill = GameUi.Box(track, "Fill", Vector2.zero, Vector2.one, new Color(0.1f, 0.62f, 0.85f)).GetComponent<Image>();
        GameUi.Box(fill.transform, "Shine", new Vector2(0f, 0.65f), Vector2.one, new Color(1f, 1f, 1f, 0.16f));
        for (int i = 1; i < 18; i++)
        {
            float x = i / 18f;
            RectTransform divider = GameUi.Box(track, "Division " + i, new Vector2(x, 0f), new Vector2(x, 1f), new Color(0.02f, 0.05f, 0.06f, 1f));
            divider.sizeDelta = new Vector2(3f, 0f);
        }
        Text caption = GameUi.Readable(GameUi.Label(body, GameText.Source("hud.experience"), new Vector2(0f, 0.5f), new Vector2(0.45f, 1f), 13), 1f);
        caption.alignment = TextAnchor.MiddleLeft;
        caption.rectTransform.offsetMin = caption.rectTransform.offsetMax = Vector2.zero;
        status = GameUi.Readable(GameUi.Label(body, "", new Vector2(0.45f, 0.5f), Vector2.one, 13), 1f);
        status.alignment = TextAnchor.MiddleRight;
        status.rectTransform.offsetMin = status.rectTransform.offsetMax = Vector2.zero;
        RectTransform levelBadge = GameUi.Box(bar, "Level Badge", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Color.clear);
        levelBadge.sizeDelta = new Vector2(44f, 44f);
        levelBadge.anchoredPosition = new Vector2(22f, 0f);
        RectTransform diamond = GameUi.Box(levelBadge, "Level Diamond", new Vector2(0.14f, 0.14f), new Vector2(0.86f, 0.86f), new Color(0.13f, 0.48f, 0.6f));
        diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
        GameUi.Box(diamond, "Inset", new Vector2(0.035f, 0.035f), new Vector2(0.965f, 0.965f), new Color(0.025f, 0.09f, 0.12f));
        levelText = GameUi.Readable(GameUi.Label(levelBadge, "", Vector2.zero, Vector2.one, 22), 1f);
        levelText.rectTransform.offsetMin = levelText.rectTransform.offsetMax = Vector2.zero;
        levelText.fontStyle = FontStyle.Bold;
        choices = GameUi.Box(canvas.transform, "Level Up", Vector2.zero, Vector2.one, GameUi.Background, true);
        choicesTitle = GameUi.Label(choices, GameText.Source("playerprogression.195"), new Vector2(0.1f, 0.77f), new Vector2(0.9f, 0.96f), 36);
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            float top = 0.7f - i * 0.18f;
            buttons[i] = GameUi.Button(choices, "", new Vector2(0.24f, top - 0.155f), new Vector2(0.76f, top), () => SelectOfferedUpgrade(index));
            cardText[i] = buttons[i].GetComponentInChildren<Text>();
            cardText[i].rectTransform.anchorMin = new Vector2(0.215f, 0.08f);
            cardText[i].rectTransform.anchorMax = new Vector2(0.96f, 0.92f);
            cardText[i].alignment = TextAnchor.MiddleLeft;
            cardText[i].fontSize = 23;
            cardText[i].resizeTextMaxSize = 23;
            // Размеры строк заданы тегами <size>; автоподбор их игнорирует, поэтому выключен.
            cardText[i].resizeTextForBestFit = false;
            cardText[i].horizontalOverflow = HorizontalWrapMode.Wrap;
            cardText[i].verticalOverflow = VerticalWrapMode.Overflow;
            cardText[i].lineSpacing = 1.05f;
            RectTransform badge = GameUi.Box(buttons[i].transform, "Icon Badge", new Vector2(0.025f, 0.1f), new Vector2(0.19f, 0.9f), GameUi.Background);
            iconBadges[i] = badge.GetComponent<Image>();
            UiArtwork.Round(iconBadges[i]);
            Outline badgeRing = badge.gameObject.AddComponent<Outline>();
            badgeRing.effectDistance = new Vector2(2f, -2f);
            icons[i] = GameUi.Box(badge, "Upgrade Artwork", new Vector2(0.1f, 0.08f), new Vector2(0.9f, 0.92f), Color.white).GetComponent<Image>();
            icons[i].preserveAspect = true;
            icons[i].enabled = false;
            GameObject iconObject = new GameObject("Vector Icon", typeof(RectTransform), typeof(UpgradeIcon));
            iconObject.transform.SetParent(badge, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.06f, 0.06f);
            iconRect.anchorMax = new Vector2(0.94f, 0.94f);
            iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
            vectorIcons[i] = iconObject.GetComponent<UpgradeIcon>();
            vectorIcons[i].raycastTarget = false;
            vectorIcons[i].ShowPlusBadge = true;
            accentLines[i] = GameUi.Box(buttons[i].transform, "Rarity stripe", new Vector2(0.025f, 0.978f),
                new Vector2(0.975f, 0.99f), GameUi.Accent).GetComponent<Image>();
            rarityLabels[i] = GameUi.Label(buttons[i].transform, "", new Vector2(0.68f, 0.78f), new Vector2(0.96f, 0.96f), 12);
            rarityLabels[i].alignment = TextAnchor.UpperRight;
            cardFx[i] = buttons[i].gameObject.AddComponent<UpgradeCardFx>();
            cardFx[i].icon = icons[i].transform;
        }
        rerollButton = GameUi.Button(choices, GameText.Source("playerprogression.257"), new Vector2(0.24f, 0.02f), new Vector2(0.495f, 0.085f), () => Reroll());
        banishButton = GameUi.Button(choices, GameText.Source("playerprogression.banish.title"), new Vector2(0.505f, 0.02f), new Vector2(0.76f, 0.085f), () => ToggleBanishMode());
        banishButton.gameObject.SetActive(false);
        choices.gameObject.SetActive(false);
        // Уведомление о полученном улучшении: значок, название с рангом и что изменилось.
        toast = GameUi.Box(canvas.transform, "Upgrade Feedback", new Vector2(0.285f, 0.19f), new Vector2(0.715f, 0.335f),
            new Color(0.02f, 0.035f, 0.05f, 0.92f));
        UiArtwork.Round(toast.GetComponent<Image>());
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.blocksRaycasts = false;
        toastStripe = GameUi.Box(toast, "Stripe", new Vector2(0.02f, 0.06f), new Vector2(0.98f, 0.12f), GameUi.Accent).GetComponent<Image>();
        toast.gameObject.AddComponent<RectMask2D>();
        toastShine = GameUi.Box(toast, "Shine", new Vector2(0f, -0.3f), new Vector2(0f, 1.3f), new Color(1f, 1f, 1f, 0.1f));
        toastShine.sizeDelta = new Vector2(60f, 0f);
        toastShine.localRotation = Quaternion.Euler(0f, 0f, -20f);
        toastShine.GetComponent<Image>().raycastTarget = false;
        toastBadge = GameUi.Box(toast, "Badge", new Vector2(0.02f, 0.18f), new Vector2(0.15f, 0.92f), GameUi.Background);
        UiArtwork.Round(toastBadge.GetComponent<Image>());
        toastIcon = GameUi.Box(toastBadge, "Icon", new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), Color.white).GetComponent<Image>();
        toastIcon.preserveAspect = true;
        GameObject vector = new GameObject("Vector Icon", typeof(RectTransform), typeof(UpgradeIcon));
        vector.transform.SetParent(toastBadge, false);
        RectTransform vectorRect = vector.GetComponent<RectTransform>();
        vectorRect.anchorMin = new Vector2(0.12f, 0.12f);
        vectorRect.anchorMax = new Vector2(0.88f, 0.88f);
        vectorRect.offsetMin = vectorRect.offsetMax = Vector2.zero;
        toastVector = vector.GetComponent<UpgradeIcon>();
        toastVector.raycastTarget = false;
        toastVector.ShowPlusBadge = true;
        Text toastCaption = GameUi.Label(toast, GameText.Source("playerprogression.258"), new Vector2(0.17f, 0.72f), new Vector2(0.97f, 0.97f), 15, new Color(0.75f, 0.82f, 0.88f));
        toastCaption.alignment = TextAnchor.MiddleLeft;
        toastCaption.resizeTextForBestFit = false;
        toastCaption.verticalOverflow = VerticalWrapMode.Overflow;
        toastText = GameUi.Readable(GameUi.Label(toast, "", new Vector2(0.17f, 0.45f), new Vector2(0.97f, 0.76f), 26, GameUi.Accent), 1f);
        toastText.alignment = TextAnchor.MiddleLeft;
        toastDetail = GameUi.Label(toast, "", new Vector2(0.17f, 0.15f), new Vector2(0.97f, 0.46f), 21, new Color(0.93f, 0.95f, 0.97f));
        toastDetail.alignment = TextAnchor.MiddleLeft;
        toast.gameObject.SetActive(false);
        if (Secondary)
        {
            // Игрок 2: уровень и опыт — на его панели (CoopHero); уведомление о карточке — выше уведомления игрока 1.
            bar.gameObject.SetActive(false);
            levelBadge.gameObject.SetActive(false);
            toast.anchorMin = new Vector2(0.285f, 0.35f);
            toast.anchorMax = new Vector2(0.715f, 0.495f);
            toastCaption.text = GameText.Source("playerprogression.259");
        }
    }

    private void OnDestroy()
    {
        CombatStats.Unregister(Stats);
        if (Instance == this) { Instance = null; ExperiencePickup.Clear(); CombatStats.Reset(); }
        if (canvas != null) Destroy(canvas.gameObject);
        if (ownsSettings) Destroy(settings);
    }
}
