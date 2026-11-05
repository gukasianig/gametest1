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
    private readonly Text[] cardText = new Text[3];
    private readonly Image[] icons = new Image[3];
    private readonly UpgradeIcon[] vectorIcons = new UpgradeIcon[3];
    private readonly UpgradeCardFx[] cardFx = new UpgradeCardFx[3];
    private readonly Image[] iconBadges = new Image[3];
    private readonly Image[] accentLines = new Image[3];
    private readonly Text[] rarityLabels = new Text[3];
    private RectTransform toast;
    private CanvasGroup toastGroup;
    private Text toastText;
    private float toastTime;
    private AutoShoter shooter;
    private PlayMovement movement;
    private OrbitManager orbits;
    private Canvas canvas;
    private RectTransform choices;
    private Text status;
    private Image fill;
    private bool ownsSettings;

    private void Awake()
    {
        Instance = this;
        shooter = GetComponent<AutoShoter>();
        movement = GetComponent<PlayMovement>();
        orbits = GetComponent<OrbitManager>();
        if (settings == null) { settings = ScriptableObject.CreateInstance<ProgressionSettings>(); ownsSettings = true; }
    }

    private void Start() { BuildUI(); RefreshUI(); }

    private void Update()
    {
        if (toastTime > 0f)
        {
            toastTime = Mathf.Max(0f, toastTime - Time.unscaledDeltaTime);
            toastGroup.alpha = Mathf.Min(1f, toastTime / 0.3f);
            toast.localScale = Vector3.one * (1f + Mathf.Max(0f, toastTime - 1.2f) * 0.06f);
            if (toastTime == 0f) toast.gameObject.SetActive(false);
        }
        if (Time.timeScale <= 0f || Choosing) return;
        ExperiencePickup.CollectNearby(this, settings);
        if (PendingChoices > 0) ShowChoices();
    }

    public void AddExperience(int amount) { AddExperience(amount, true); }

    public void AddExperience(int amount, bool allowChoices)
    {
        if (amount <= 0) return;
        long total = (long)Experience + amount;
        while (total >= settings.ExperienceForLevel(Level))
        {
            total -= settings.ExperienceForLevel(Level);
            Level++;
            if (allowChoices) PendingChoices++;
        }
        Experience = (int)total;
        RefreshUI();
        if (allowChoices && !Choosing && PendingChoices > 0 && Time.timeScale > 0f) ShowChoices();
    }

    public int Rank(string id) { return ranks.TryGetValue(id, out int rank) ? rank : 0; }

    private float Bonus(UpgradeEffect effect)
    {
        // Permanent workshop bonuses count toward the same caps as in-run upgrades.
        float sum = effect == UpgradeEffect.MaxHealth ? 0f : MetaProgress.Bonus(effect);
        if (catalog == null) return sum;
        foreach (UpgradeDefinition definition in catalog.entries)
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

    public bool Available(UpgradeDefinition definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.id) || definition.amount <= 0f ||
            Level < definition.minimumLevel || Rank(definition.id) >= definition.maxRanks) return false;
        if ((definition.effect == UpgradeEffect.GrenadeDamage || definition.effect == UpgradeEffect.GrenadeRadius) && shooter.grenadePrefab == null) return false;
        if ((definition.effect == UpgradeEffect.OrbitDamage || definition.effect == UpgradeEffect.OrbitCount) &&
            (orbits == null || orbits.orbitPrefab == null)) return false;
        if (definition.effect == UpgradeEffect.OrbitDamage && orbits.orbitCount == 0) return false;
        return definition.effect == UpgradeEffect.MaxHealth || definition.effect == UpgradeEffect.OrbitCount ||
            1f + Bonus(definition.effect) < Mathf.Max(1f, Cap(definition.effect)) - 0.0001f;
    }

    public void RollOffers()
    {
        offered.Clear();
        if (catalog == null) return;
        var candidates = new List<UpgradeDefinition>();
        var ids = new HashSet<string>();
        foreach (UpgradeDefinition definition in catalog.entries)
            if (Available(definition) && ids.Add(definition.id)) candidates.Add(definition);
        while (offered.Count < 3 && candidates.Count > 0)
        {
            int sum = 0;
            foreach (UpgradeDefinition definition in candidates) sum += catalog.Weight(definition);
            int roll = Random.Range(0, sum);
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= catalog.Weight(candidates[i]);
                if (roll >= 0) continue;
                offered.Add(candidates[i]);
                candidates.RemoveAt(i);
                break;
            }
        }
    }

    private void ShowChoices()
    {
        // Never re-roll while cards are already on screen: Update() calls this right after
        // ExperiencePickup.CollectNearby has opened the window in the same frame, which used to
        // replace the offered list while the old cards stayed drawn (clicks picked another upgrade).
        if (Choosing) return;
        RollOffers();
        if (offered.Count == 0) { PendingChoices = 0; return; }
        if (GameSession.Instance != null && !GameSession.Instance.EnterUpgrade()) return;
        Choosing = true;
        Time.timeScale = 0f;
        choices.gameObject.SetActive(true);
        RefreshCards();
    }

    public bool SelectOfferedUpgrade(int index)
    {
        if (!Choosing || index < 0 || index >= offered.Count ||
            (GameSession.Instance != null && GameSession.Instance.State != GameRunState.Upgrade)) return false;
        UpgradeDefinition definition = offered[index];
        if (!Available(definition)) return false;
        ranks[definition.id] = Rank(definition.id) + 1;
        if (definition.effect == UpgradeEffect.MaxHealth) GetComponent<PlayerHealth>().IncreaseMaxHealth(Mathf.RoundToInt(definition.amount));
        if (definition.effect == UpgradeEffect.OrbitCount) orbits.UpgradeOrbitCount(Mathf.RoundToInt(definition.amount));
        ApplyStats();
        toastText.text = definition.title + "  —  улучшено";
        toastText.color = UpgradeCatalog.RarityColor(definition.rarity);
        toastTime = 1.6f;
        toastGroup.alpha = 1f;
        toast.gameObject.SetActive(true);
        PendingChoices--;
        if (PendingChoices > 0)
        {
            RollOffers();
            if (offered.Count > 0) { RefreshCards(); return true; }
            PendingChoices = 0;
        }
        CloseChoices();
        if (GameSession.Instance != null) GameSession.Instance.LeaveUpgrade();
        else Time.timeScale = 1f;
        return true;
    }

    private void Choose(int index) { SelectOfferedUpgrade(index); }

    private void ApplyStats()
    {
        DamageMultiplier = Mathf.Min(Mathf.Max(1f, Cap(UpgradeEffect.Damage)), 1f + Bonus(UpgradeEffect.Damage));
        AttackSpeedMultiplier = Mathf.Min(Mathf.Max(1f, Cap(UpgradeEffect.AttackSpeed)), 1f + Bonus(UpgradeEffect.AttackSpeed));
        shooter.SetProgressionMultipliers(DamageMultiplier, AttackSpeedMultiplier);
        shooter.SetSecondaryMultipliers(Mathf.Min(5f, 1f + Bonus(UpgradeEffect.GrenadeDamage)),
            Mathf.Min(2.5f, 1f + Bonus(UpgradeEffect.GrenadeRadius)));
        movement.SpeedMultiplier = Mathf.Min(Mathf.Max(1f, Cap(UpgradeEffect.Movement)), 1f + Bonus(UpgradeEffect.Movement));
        PickupRadiusMultiplier = Mathf.Min(Mathf.Max(1f, Cap(UpgradeEffect.Pickup)), 1f + Bonus(UpgradeEffect.Pickup));
        if (orbits != null) orbits.SetDamageMultiplier(Mathf.Min(5f, 1f + Bonus(UpgradeEffect.OrbitDamage)));
    }

    public void CloseChoices()
    {
        Choosing = false;
        if (choices != null) choices.gameObject.SetActive(false);
    }

    private string Preview(UpgradeDefinition definition)
    {
        int rank = Rank(definition.id);
        string rankText = rank == 0
            ? "Новое улучшение  •  станет ранг 1 из " + definition.maxRanks + "\n"
            : "Сейчас ранг " + rank + "  •  станет ранг " + (rank + 1) + " из " + definition.maxRanks + "\n";
        if (definition.effect == UpgradeEffect.MaxHealth)
            return rankText + GetComponent<PlayerHealth>().maxHP + " → " + (GetComponent<PlayerHealth>().maxHP + Mathf.RoundToInt(definition.amount)) + " HP";
        if (definition.effect == UpgradeEffect.OrbitCount)
            return rankText + "Спутники: " + orbits.orbitCount + " → " + (orbits.orbitCount + Mathf.RoundToInt(definition.amount));
        float before = Mathf.Min(Mathf.Max(1f, Cap(definition.effect)), 1f + Bonus(definition.effect));
        float after = Mathf.Min(Mathf.Max(1f, Cap(definition.effect)), before + definition.amount);
        return rankText +
            "×" + before.ToString("0.##") + " → ×" + after.ToString("0.##");
    }

    private void RefreshCards()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].gameObject.SetActive(i < offered.Count);
            if (i >= offered.Count) continue;
            UpgradeDefinition definition = offered[i];
            Color color = UpgradeCatalog.RarityColor(definition.rarity);
            buttons[i].GetComponent<Image>().color = new Color(0.035f + color.r * 0.06f, 0.045f + color.g * 0.06f, 0.065f + color.b * 0.06f);
            cardText[i].text = "<b>" + definition.title + "</b>\n<color=#ABB9CB>" + Preview(definition) + "</color>";
            rarityLabels[i].text = UpgradeCatalog.RarityName(definition.rarity).ToUpperInvariant();
            rarityLabels[i].color = color;
            icons[i].sprite = definition.icon != null ? definition.icon : UiArtwork.Upgrade(definition.effect);
            icons[i].enabled = icons[i].sprite != null;
            vectorIcons[i].enabled = icons[i].sprite == null;
            vectorIcons[i].Effect = definition.effect;
            vectorIcons[i].color = color;
            iconBadges[i].color = new Color(0.02f + color.r * 0.035f, 0.03f + color.g * 0.035f, 0.045f + color.b * 0.035f, 1f);
            accentLines[i].color = new Color(color.r, color.g, color.b, 0.7f);
            cardFx[i].Present(color, i);
        }
    }

    private void RefreshUI()
    {
        if (status == null) return;
        int required = settings.ExperienceForLevel(Level);
        status.text = "Уровень " + Level + "   •   Опыт " + Experience + " / " + required;
        fill.rectTransform.anchorMax = new Vector2((float)Experience / required, 1f);
    }

    private void BuildUI()
    {
        canvas = GameUi.Canvas("Experience HUD", 80);
        RectTransform bar = GameUi.Box(canvas.transform, "Experience", new Vector2(0.2f, 0.91f), new Vector2(0.8f, 0.97f), GameUi.Background);
        fill = GameUi.Box(bar, "Fill", Vector2.zero, Vector2.one, new Color(0.05f, 0.4f, 0.55f)).GetComponent<Image>();
        status = GameUi.Label(bar, "", Vector2.zero, Vector2.one, 23);
        choices = GameUi.Box(canvas.transform, "Level Up", Vector2.zero, Vector2.one, GameUi.Background, true);
        GameUi.Label(choices, "НОВЫЙ УРОВЕНЬ\nВыбери улучшение", new Vector2(0.1f, 0.77f), new Vector2(0.9f, 0.96f), 36);
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            float top = 0.73f - i * 0.22f;
            buttons[i] = GameUi.Button(choices, "", new Vector2(0.12f, top - 0.19f), new Vector2(0.88f, top), () => SelectOfferedUpgrade(index));
            cardText[i] = buttons[i].GetComponentInChildren<Text>();
            cardText[i].rectTransform.anchorMin = new Vector2(0.23f, 0.08f);
            cardText[i].rectTransform.anchorMax = new Vector2(0.96f, 0.92f);
            cardText[i].alignment = TextAnchor.MiddleLeft;
            cardText[i].fontSize = 23;
            cardText[i].resizeTextMaxSize = 23;
            RectTransform badge = GameUi.Box(buttons[i].transform, "Icon Badge", new Vector2(0.025f, 0.12f), new Vector2(0.205f, 0.88f), GameUi.Background);
            iconBadges[i] = badge.GetComponent<Image>();
            UiArtwork.Round(iconBadges[i]);
            icons[i] = GameUi.Box(badge, "Upgrade Artwork", new Vector2(0.1f, 0.08f), new Vector2(0.9f, 0.92f), Color.white).GetComponent<Image>();
            icons[i].preserveAspect = true;
            icons[i].enabled = false;
            GameObject iconObject = new GameObject("Vector Icon", typeof(RectTransform), typeof(UpgradeIcon));
            iconObject.transform.SetParent(badge, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.12f, 0.12f);
            iconRect.anchorMax = new Vector2(0.88f, 0.88f);
            iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
            vectorIcons[i] = iconObject.GetComponent<UpgradeIcon>();
            vectorIcons[i].raycastTarget = false;
            accentLines[i] = GameUi.Box(buttons[i].transform, "Rarity stripe", new Vector2(0.025f, 0.978f),
                new Vector2(0.975f, 0.99f), GameUi.Accent).GetComponent<Image>();
            rarityLabels[i] = GameUi.Label(buttons[i].transform, "", new Vector2(0.68f, 0.78f), new Vector2(0.96f, 0.96f), 12);
            rarityLabels[i].alignment = TextAnchor.UpperRight;
            cardFx[i] = buttons[i].gameObject.AddComponent<UpgradeCardFx>();
            cardFx[i].icon = icons[i].transform;
        }
        choices.gameObject.SetActive(false);
        toast = GameUi.Box(canvas.transform, "Upgrade Feedback", new Vector2(0.23f, 0.035f), new Vector2(0.77f, 0.105f), GameUi.Background);
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.blocksRaycasts = false;
        toastText = GameUi.Label(toast, "", Vector2.zero, Vector2.one, 23, GameUi.Accent);
        toast.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) { Instance = null; ExperiencePickup.Clear(); }
        if (canvas != null) Destroy(canvas.gameObject);
        if (ownsSettings) Destroy(settings);
    }
}
