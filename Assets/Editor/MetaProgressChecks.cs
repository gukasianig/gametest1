using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Optional check of the between-run progression layer.
// Batch: -batchmode -executeMethod MetaProgressChecks.RunBatch (without -quit). Report: Logs/meta-progress-results.txt.
// Part 1 is pure logic (rewards, purchases, records, save/restore); part 2 runs in Play (workshop UI, bonuses applied, reward on results).
[InitializeOnLoad]
public static class MetaProgressChecks
{
    private const string Request = "Logs/meta-progress.request";
    private const string Report = "Logs/meta-progress-results.txt";
    private static IEnumerator routine;
    private static readonly System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();

    static MetaProgressChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Request)) EditorApplication.delayCall += Begin;
            if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Tick;
        };
    }

    public static void RunBatch()
    {
        File.WriteAllText(Request, "batch");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.isPlaying = true;
    }

    private static void Begin()
    {
        File.Delete(Request);
        lines.Clear();
        routine = Checks();
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try { if (!routine.MoveNext()) Finish(true, "Meta progression checks complete."); }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Pass(string text) { lines.Add("PASS: " + text); File.WriteAllLines(Report, lines); }
    private static void Require(bool condition, string text) { if (!condition) throw new Exception(text); }

    private static void Finish(bool success, string text)
    {
        EditorApplication.update -= Tick;
        lines.Add((success ? "PASS: " : "FAIL: ") + text);
        File.WriteAllLines(Report, lines);
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static IEnumerator Checks()
    {
        yield return null;
        LogicChecks();
        MetaProgress.UseMemory();
        MetaProgress.Data.currency = 100000;
        string[] bought = { "meta_health", "meta_health", "meta_health", "meta_damage", "meta_damage", "meta_speed" };
        foreach (string id in bought) Require(MetaProgress.Buy(id), "Could not buy " + id);
        int currencyAfterSetup = MetaProgress.Currency;

        GameObject player = PlayerProgression.Instance.gameObject;
        StartMenu menu = player.GetComponent<StartMenu>();
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        AutoShoter shooter = player.GetComponent<AutoShoter>();
        PlayMovement movement = player.GetComponent<PlayMovement>();
        PlayerProgression progression = player.GetComponent<PlayerProgression>();
        int baseHealth = health.maxHP;
        int baseDamage = shooter.weapons[1].damage;

        menu.ShowWorkshop();
        Transform row = FindDeep(menu.transform.root, "Node meta_pickup");
        Require(row != null, "Workshop rows were not built.");
        // Rows live in the menu canvas, which is not under the player.
        Button node = row.GetComponent<Button>();
        Require(node != null, "Pickup node missing.");
        node.onClick.Invoke();
        Button buy = FindDeep(menu.transform.root, "Tree purchase").GetComponent<Button>();
        Require(buy != null && buy.interactable, "Pickup upgrade should be affordable.");
        int cost = MetaProgress.Find("meta_pickup").Cost(0);
        buy.onClick.Invoke();
        Require(MetaProgress.Rank("meta_pickup") == 1 && MetaProgress.Currency == currencyAfterSetup - cost, "Workshop purchase failed.");
        Require(buy.GetComponentInChildren<Text>().text.Contains((MetaProgress.Find("meta_pickup").Cost(1)).ToString()), "Workshop label not refreshed.");
        Pass("Workshop UI: rows built, purchase through the button spends currency and refreshes the label.");

        // Locked weapons: cannot start, cannot be bought without funds, can be bought once with funds.
        Require(MetaProgress.IsWeaponUnlocked(0) && MetaProgress.IsWeaponUnlocked(1) &&
            !MetaProgress.IsWeaponUnlocked(2) && !MetaProgress.IsWeaponUnlocked(3), "Default weapon availability.");
        menu.SendMessage("BeginGame", 2);
        Require(GameSession.Instance.State == GameRunState.Menu, "Locked weapon started a run.");
        Transform card = FindDeep(menu.transform.root, "Weapon Card 2");
        Transform shade = FindDeep(menu.transform.root, "Locked");
        Require(card != null && shade != null, "Weapon cards/locks were not built.");
        int richCurrency = MetaProgress.Currency;
        MetaProgress.Data.currency = 100;
        card.GetComponent<Button>().onClick.Invoke();
        Require(!MetaProgress.IsWeaponUnlocked(2) && MetaProgress.Currency == 100, "Weapon opened without enough currency.");
        MetaProgress.Data.currency = richCurrency;
        card.GetComponent<Button>().onClick.Invoke();
        Require(MetaProgress.IsWeaponUnlocked(2) && MetaProgress.Currency == richCurrency - 150, "Weapon purchase failed.");
        Require(GameSession.Instance.State == GameRunState.Menu, "Buying a weapon must not start the run.");
        Require(!card.Find("Locked").gameObject.activeSelf, "Lock overlay not hidden after purchase.");
        Pass("Weapon unlocks: locked card cannot start, needs funds, purchase opens it once, overlay hides.");

        // Run modifiers chosen through the menu buttons.
        menu.ShowModifiers();
        foreach (string id in new[] { "mod_fast", "mod_thick", "mod_fragile", "mod_short" })
        {
            Transform modifierRow = FindDeep(menu.transform.root, "Row " + id);
            Require(modifierRow != null, "Modifier row missing: " + id);
            modifierRow.GetComponentInChildren<Button>().onClick.Invoke();
            Require(RunModifiers.IsActive(id) && modifierRow.GetComponentInChildren<Button>().GetComponentInChildren<Text>().text == "Включено",
                "Modifier not enabled through the button: " + id);
        }
        float multiplier = 1f + 0.15f + 0.15f + 0.25f + 0.1f;
        Require(Mathf.Approximately(RunModifiers.RewardMultiplier, multiplier), "Reward multiplier: " + RunModifiers.RewardMultiplier);
        Pass("Modifier screen: four modifiers enabled through buttons, reward multiplier x" + multiplier.ToString("0.##") + ".");

        // Start with the freshly unlocked weapon.
        baseDamage = shooter.weapons[2].damage;
        menu.SendMessage("BeginGame", 2);
        Require(GameSession.Instance.State == GameRunState.Playing, "Unlocked weapon did not start the run.");
        Require(health.maxHP == Mathf.RoundToInt((baseHealth + 30) * 0.6f) && health.CurrentHP == health.maxHP,
            "Permanent health + fragile hero not applied: " + health.maxHP);
        Require(Mathf.Approximately(GameSession.Instance.survivalSeconds, 300f), "Short finale not applied: " + GameSession.Instance.survivalSeconds);
        WaveManager spawner = UnityEngine.Object.FindObjectOfType<WaveManager>();
        double enemyDeadline = EditorApplication.timeSinceStartup + 20;
        EnemyAI firstEnemy = null;
        while (firstEnemy == null && EditorApplication.timeSinceStartup < enemyDeadline)
        {
            firstEnemy = UnityEngine.Object.FindObjectOfType<EnemyAI>();
            yield return null;
        }
        Require(firstEnemy != null, "No enemy spawned for modifier check.");
        string prefabName = firstEnemy.name.Replace("(Clone)", "").Trim();
        EnemyType enemyType = Array.Find(spawner.enemies, e => e.prefab != null && e.prefab.name == prefabName);
        Require(enemyType != null, "Cannot match spawned enemy to its type: " + prefabName);
        Require(Mathf.Approximately(firstEnemy.speed, enemyType.prefab.GetComponent<EnemyAI>().speed * 1.25f), "Fast enemies not applied.");
        Require(firstEnemy.GetComponent<Health>().maxHealth >= Mathf.RoundToInt(enemyType.prefab.GetComponent<Health>().maxHealth * 1.5f), "Thick enemies not applied.");
        Pass("Modifiers applied in a run: hero HP x0.6 over the permanent bonus, finale at 300 s, enemy speed x1.25, enemy health x1.5.");
        Require(health.maxHP == Mathf.RoundToInt((baseHealth + 30) * 0.6f), "Permanent health not applied: " + health.maxHP);
        Require(shooter.CurrentDamage == Mathf.Max(1, Mathf.RoundToInt(baseDamage * 1.08f)), "Permanent damage not applied: " + shooter.CurrentDamage);
        Require(Mathf.Approximately(movement.SpeedMultiplier, 1.03f), "Permanent speed not applied.");
        Require(Mathf.Approximately(progression.PickupRadiusMultiplier, 1.1f), "Permanent pickup not applied.");
        Pass("Permanent bonuses applied at run start: +30 HP, +8% damage, +3% speed, +10% pickup.");

        int before = MetaProgress.Currency;
        GameSession.Instance.Defeat();
        yield return null;
        MetaRecord record = MetaProgress.Record("all");
        Require(MetaProgress.Data.runs == 1 && record != null && record.runs == 1, "Run was not recorded.");
        int expected = Mathf.RoundToInt(MetaProgress.Reward(false, GameSession.Instance.Elapsed, RunStatistics.Instance.Kills, progression.Level) * multiplier);
        Require(MetaProgress.Currency - before == expected && expected > 0, "Reward must include the modifier multiplier: " + (MetaProgress.Currency - before) + " vs " + expected);
        Pass("Run end records the result and pays currency with the modifier multiplier (+" + (MetaProgress.Currency - before) + ").");
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true))
        {
            Transform found = Search(canvas.transform, name);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform Search(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = Search(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static void LogicChecks()
    {
        string file = Path.GetFullPath("Logs/meta-test.json");
        foreach (string suffix in new[] { "", ".bak", ".tmp", ".corrupt" }) if (File.Exists(file + suffix)) File.Delete(file + suffix);
        MetaProgress.UseFile(file);
        Require(MetaProgress.Currency == 0 && MetaProgress.Data.runs == 0, "Fresh save is not empty.");
        Require(MetaProgress.Reward(false, 120f, 50, 3) == 50 + 24 + 9, "Reward formula (defeat).");
        Require(MetaProgress.Reward(true, 600f, 300, 10) == 300 + 120 + 30 + 250, "Reward formula (victory).");

        RunReward first = MetaProgress.RecordRun(1, false, 120f, 50, 3);
        Require(first.amount == 83 && MetaProgress.Currency == 83 && first.newBestTime && first.newBestKills, "First run.");
        RunReward worse = MetaProgress.RecordRun(1, false, 60f, 10, 2);
        Require(!worse.newBestTime && !worse.newBestKills && worse.bestTime == 120f && worse.bestKills == 50, "Worse run must keep records.");
        RunReward other = MetaProgress.RecordRun(2, true, 600f, 80, 5);
        Require(other.newBestTime && MetaProgress.Record("weapon:2").victories == 1 && MetaProgress.Record("weapon:1").runs == 2, "Per-weapon records.");
        Require(MetaProgress.Data.runs == 3 && MetaProgress.Data.victories == 1, "Run counters.");

        int money = MetaProgress.Currency;
        MetaUpgrade pickup = MetaProgress.Find("meta_pickup");
        Require(MetaProgress.CanBuy(pickup) && MetaProgress.Buy("meta_pickup"), "Purchase with enough currency.");
        Require(MetaProgress.Currency == money - pickup.Cost(0) && MetaProgress.Rank("meta_pickup") == 1, "Purchase cost/rank.");
        MetaProgress.Data.currency = 0;
        Require(!MetaProgress.Buy("meta_pickup") && MetaProgress.Rank("meta_pickup") == 1, "Cannot buy without currency.");
        MetaProgress.Data.currency = 1000000;
        for (int i = 0; i < 20; i++) MetaProgress.Buy("meta_pickup");
        Require(MetaProgress.Rank("meta_pickup") == pickup.maxRank, "Rank limit not enforced: " + MetaProgress.Rank("meta_pickup"));
        Require(!MetaProgress.Buy("unknown_id"), "Unknown id must not be purchasable.");
        Require(Mathf.Approximately(MetaProgress.Bonus(UpgradeEffect.Pickup), pickup.maxRank * pickup.amountPerRank), "Bonus sum.");
        MetaProgress.Unlock("map:forge");
        MetaProgress.Unlock("map:forge");
        Require(MetaProgress.IsUnlocked("map:forge") && MetaProgress.Data.unlocked.Count == 1, "Unlock list.");
        Require(MetaProgress.IsWeaponUnlocked(0) && MetaProgress.IsWeaponUnlocked(1) && !MetaProgress.IsWeaponUnlocked(2), "Weapon defaults.");
        MetaProgress.Data.currency = 149;
        Require(!MetaProgress.TryUnlock("weapon:2") && !MetaProgress.IsWeaponUnlocked(2) && MetaProgress.Currency == 149, "Unlock with too little currency.");
        MetaProgress.Data.currency = 150;
        Require(MetaProgress.TryUnlock("weapon:2") && MetaProgress.IsWeaponUnlocked(2) && MetaProgress.Currency == 0, "Unlock purchase.");
        Require(!MetaProgress.TryUnlock("weapon:2") && !MetaProgress.TryUnlock("weapon:99"), "Double/unknown unlock must fail.");
        MetaProgress.Data.currency = 1000000;
        MetaProgress.Save();
        Pass("Rewards, per-weapon/global records, purchases, rank limit, unlock list.");

        int savedCurrency = MetaProgress.Currency;
        MetaProgress.UseFile(file);
        Require(MetaProgress.Currency == savedCurrency && MetaProgress.Rank("meta_pickup") == pickup.maxRank &&
            MetaProgress.Record("all").bestKills == 80 && MetaProgress.IsUnlocked("map:forge") &&
            MetaProgress.IsWeaponUnlocked(2) && !MetaProgress.IsWeaponUnlocked(3), "Save/load round trip.");
        MetaProgress.RecordRun(1, false, 30f, 1, 1); // second save creates the .bak of the previous state
        Require(File.Exists(file + ".bak"), "Backup was not created.");
        File.WriteAllText(file, "{ not json");
        MetaProgress.UseFile(file);
        Require(MetaProgress.Currency == savedCurrency, "Damaged save must recover from backup.");
        File.WriteAllText(file, "garbage");
        File.WriteAllText(file + ".bak", "garbage");
        MetaProgress.UseFile(file);
        Require(MetaProgress.Currency == 0 && MetaProgress.Data.runs == 0 && File.Exists(file + ".corrupt"), "Unreadable save must start fresh and keep the bad file.");
        File.WriteAllText(file, "{\"version\":999,\"currency\":5}");
        File.Delete(file + ".bak");
        MetaProgress.UseFile(file);
        Require(MetaProgress.Currency == 0, "Newer save version must not be misread.");
        foreach (string suffix in new[] { "", ".bak", ".tmp", ".corrupt" }) if (File.Exists(file + suffix)) File.Delete(file + suffix);
        Pass("Persistence: round trip, atomic backup, recovery from damaged file, unreadable/newer-version handling.");

        // Run modifiers: toggling, multiplier, conflicts-free combination, persistence and effects on settings.
        MetaProgress.UseFile(file);
        Require(RunModifiers.ActiveCount == 0 && Mathf.Approximately(RunModifiers.RewardMultiplier, 1f), "Fresh save has modifiers.");
        Require(RunModifiers.Toggle("mod_fast") && RunModifiers.IsActive("mod_fast") && Mathf.Approximately(RunModifiers.RewardMultiplier, 1.15f), "Toggle on.");
        Require(!RunModifiers.Toggle("mod_fast") && !RunModifiers.IsActive("mod_fast"), "Toggle off.");
        Require(!RunModifiers.Toggle("no_such_modifier") && RunModifiers.ActiveCount == 0, "Unknown modifier must do nothing.");
        foreach (RunModifier modifier in RunModifiers.Catalog) RunModifiers.Toggle(modifier.id);
        Require(RunModifiers.ActiveCount == RunModifiers.Catalog.Length && Mathf.Approximately(RunModifiers.RewardMultiplier, 1.9f), "All five: x1.9.");
        Require(Mathf.Approximately(RunModifiers.EnemySpeed, 1.25f) && Mathf.Approximately(RunModifiers.EnemyHealth, 1.5f) &&
            Mathf.Approximately(RunModifiers.HeroHealth, 0.6f) && Mathf.Approximately(RunModifiers.SurvivalSeconds(600f), 300f) &&
            Mathf.Approximately(RunModifiers.SurvivalSeconds(120f), 120f), "Modifier effect getters.");
        WaveSettings waves = new WaveSettings();
        Require(waves.AliveLimitForWave(1) == 9 && Mathf.Approximately(waves.SpawnIntervalForWave(1), 0.75f * 0.75f), "Horde effect on alive limit/interval.");
        RunReward modified = MetaProgress.RecordRun(1, true, 600f, 300, 10);
        Require(modified.amount == Mathf.RoundToInt(700f * 1.9f) && Mathf.Approximately(modified.multiplier, 1.9f), "Reward with multiplier: " + modified.amount);
        MetaProgress.UseFile(file);
        Require(RunModifiers.ActiveCount == RunModifiers.Catalog.Length, "Chosen modifiers must survive a reload.");
        RunModifiers.Clear();
        Require(RunModifiers.ActiveCount == 0 && Mathf.Approximately(waves.AliveLimitForWave(1), 6) && Mathf.Approximately(RunModifiers.RewardMultiplier, 1f), "Clear.");
        foreach (string suffix in new[] { "", ".bak", ".tmp", ".corrupt" }) if (File.Exists(file + suffix)) File.Delete(file + suffix);
        Pass("Modifiers: toggle/clear, x1.9 with all five, effect getters, horde limits, reward multiplier, persistence.");
    }
}
