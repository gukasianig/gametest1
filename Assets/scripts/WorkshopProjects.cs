using UnityEngine;

// Permanent project state reuses stable rank/unlock keys; version-1 saves remain readable.
public static class WorkshopProjects
{
    public static readonly string[] Ids = { "generator", "scanner", "relics", "drones", "weapons" };
    private static readonly int[,] Costs = { { 120, 200, 300 }, { 150, 250, 350 }, { 180, 280, 400 }, { 180, 280, 380 }, { 160, 260, 360 } };
    public static int Revision { get; private set; }
    public static int Count(string key) => Mathf.Max(0, MetaProgress.Rank("workshop:" + key));
    public static int Rank(string id) => Mathf.Clamp(Count("project:" + id), 0, 3);
    public static string Title(string id) => GameText.Source("projects." + id + ".title");
    public static string Reward(string id, int stage) => GameText.Source("projects." + id + ".reward" + Mathf.Clamp(stage, 0, 2));
    public static bool PulseUnlocked => Rank("generator") >= 1;
    public static bool DriveUnlocked => Rank("generator") >= 2;
    public static float CooldownFactor => Rank("generator") == 3 ? .9f : 1f;
    public static int Target => Mathf.Clamp(Count("target") - 1, -1, Ids.Length - 1);
    public static int ScannerMode => Count("scanner-mode") % 3;
    public static int DroneMode => Count("drone-mode") % 3;
    public static int Relic => Count("relic");

    // 21.11.2026: раздел проектов в Мастерской открывается после 2 завершённых забегов (не перегружать первый заход)
    // или сразу, если у игрока уже есть прогресс в проектах (куплен этап или получен компонент).
    public const int SectionRuns = 2;
    public static bool SectionUnlocked
    {
        get
        {
            if (MetaProgress.Data != null && MetaProgress.Data.runs >= SectionRuns) return true;
            foreach (string id in Ids) if (Rank(id) > 0) return true;
            return Count("core") > 0 || Count("boss") > 0 || Count("research") > 0;
        }
    }
    public static int Cost(string id)
    {
        int index = System.Array.IndexOf(Ids, id);
        return index < 0 ? int.MaxValue : Costs[index, Mathf.Min(2, Rank(id))];
    }

    private static void Set(string key, int value)
    {
        string full = "workshop:" + key;
        MetaEntry entry = MetaProgress.Data.ranks.Find(e => e.key == full);
        if (entry == null) MetaProgress.Data.ranks.Add(new MetaEntry { key = full, value = Mathf.Max(0, value) });
        else entry.value = Mathf.Max(0, value);
        Revision++;
    }

    private static string RequirementKey(string id, int stage)
    {
        if (id == "generator") return stage == 0 ? "" : stage == 1 ? "core" : "boss";
        if (id == "scanner") return stage == 0 ? "runs" : stage == 1 ? "events" : "caches";
        if (id == "relics") return stage == 0 ? "core" : stage == 1 ? "boss" : "research";
        if (id == "drones") return stage == 0 ? "events" : stage == 1 ? "caches" : "research";
        return stage == 0 ? "runs" : stage == 1 ? "kills" : "boss";
    }

    private static int Required(string key) => key == "research" ? 3 : key == "caches" ? 2 : key == "kills" ? 100 : 1;
    private static int Have(string key) => key == "runs" ? MetaProgress.Data.runs : key == "kills" ? (int)System.Math.Min(int.MaxValue, MetaProgress.Data.totalKills) : Count(key);
    public static bool RequirementsMet(string id)
    {
        string key = RequirementKey(id, Rank(id));
        return string.IsNullOrEmpty(key) || Have(key) >= Required(key);
    }

    public static string Requirement(string id)
    {
        if (Rank(id) == 3) return GameText.Source("projects.complete");
        string key = RequirementKey(id, Rank(id));
        return string.IsNullOrEmpty(key) ? GameText.Source("projects.require.details") :
            GameText.Source("projects.require." + key) + " " + Mathf.Min(Have(key), Required(key)) + " / " + Required(key);
    }

    public static bool CanBuy(string id) => System.Array.IndexOf(Ids, id) >= 0 && Rank(id) < 3 && RequirementsMet(id) && MetaProgress.Currency >= Cost(id);
    public static bool Buy(string id)
    {
        if (!CanBuy(id)) return false;
        int stage = Rank(id);
        string key = RequirementKey(id, stage);
        MetaProgress.Data.currency -= Cost(id);
        if (key == "core" || key == "boss" || key == "research") Set(key, Count(key) - Required(key));
        Set("project:" + id, stage + 1);
        MetaProgress.Save();
        return true;
    }

    public static void Track(string id)
    {
        int index = System.Array.IndexOf(Ids, id);
        if (index < 0) return;
        Set("target", Target == index ? 0 : index + 1);
        MetaProgress.Save();
    }

    public static string Goal => Target < 0 ? GameText.Source("projects.goal.none") : Title(Ids[Target]) + " · " + Requirement(Ids[Target]);
    public static string RelicName(int relic) => GameText.Source("projects.relic." + relic);
    public static string Option(string id)
    {
        if (id == "scanner") return GameText.Source("projects.scanner.mode" + ScannerMode);
        if (id == "drones") return GameText.Source("projects.drones.mode" + DroneMode);
        if (id == "relics") return RelicName(Relic);
        return "";
    }

    public static string OptionDescription(string id)
    {
        if (id == "relics") return GameText.Source("projects.relic.description" + Relic);
        if (id == "drones") return GameText.Source("projects.drones.description" + DroneMode);
        if (id == "scanner") return GameText.Source("projects.scanner.description" + ScannerMode);
        return "";
    }

    public static void CycleOption(string id)
    {
        if (Rank(id) == 0) return;
        if (id == "scanner" || id == "drones") Set(id == "scanner" ? "scanner-mode" : "drone-mode", (id == "scanner" ? ScannerMode + 1 : DroneMode + 1) % 3);
        else if (id == "relics")
        {
            int next = Relic;
            do { next = (next + 1) % 5; } while (next != 0 && !MetaProgress.IsUnlocked("workshop:relic:" + next));
            Set("relic", next);
        }
        else return;
        MetaProgress.Save();
    }

    public static float RelicPower => Rank("relics") > 0 ? 1f + .25f * (Rank("relics") - 1) : 0f;
    public static bool HasRelic(int relic) => RelicPower > 0f && Relic == relic && MetaProgress.IsUnlocked("workshop:relic:" + relic);
    public static int Mastery(int weapon)
    {
        int kills = Count("weapon:" + weapon);
        return Mathf.Min(Rank("weapons"), kills >= 600 ? 3 : kills >= 300 ? 2 : kills >= 100 ? 1 : 0);
    }
    public static string MasteryText(int weapon) => GameText.Source("projects.mastery") + Mastery(weapon) + "/3 · " + Count("weapon:" + weapon) + " / " + (Mastery(weapon) == 0 ? 100 : Mastery(weapon) == 1 ? 300 : 600);

    public static void RecordKill(int weapon)
    {
        if (GameSession.TestMode || Rank("weapons") == 0) return;
        string key = "weapon:" + weapon;
        int before = Count(key);
        Set(key, Mathf.Min(600, before + 1));
        if (before < 600 && (before + 1) % 10 == 0) MetaProgress.Save();
    }

    public static void RecordBoss(int relic)
    {
        if (GameSession.TestMode) return;
        Set(relic == 1 ? "core" : "boss", Count(relic == 1 ? "core" : "boss") + 1);
        MetaProgress.Unlock("workshop:relic:" + relic);
        MetaProgress.Save();
    }
    public static void RecordEvent()
    {
        if (GameSession.TestMode) return;
        Set("events", Count("events") + 1);
        MetaProgress.Save();
    }
    public static void CollectCache(PlayerProgression hero)
    {
        if (GameSession.TestMode || hero == null) return;
        Set("caches", Count("caches") + 1);
        if (ScannerMode == 0)
        {
            int reward = 30 + 10 * Rank("scanner");
            MetaProgress.Data.currency += reward;
            MetaProgress.Data.totalEarned += reward;
        }
        else if (ScannerMode == 1)
        {
            hero.GetComponent<PlayerHealth>().Heal(25);
            hero.AddExperience(20);
        }
        else Set("research", Count("research") + 1);
        MetaProgress.Save();
    }
}
