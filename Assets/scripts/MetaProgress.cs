using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable] public class MetaEntry { public string key; public int value; }

[Serializable]
public class MetaRecord
{
    public string key;
    public float bestTime;
    public int bestKills;
    public int bestLevel;
    public int runs;
    public int victories;
    public long bestDamage;
}

// Last counted run, shown on the menu statistics screen.
[Serializable]
public class MetaLastRun
{
    public int weapon = -1;
    public bool victory;
    public float seconds;
    public int kills;
    public int level;
    public int wave;
    public long damage;
    public int reward;
    public float multiplier = 1f;
    public string modifiers = "";
    public string date = "";
    public bool newBestTime, newBestKills;
    public RunReport report;
}

// Everything that survives between runs. Lists of keyed entries (not fixed fields) let later features
// (weapon/map unlocks, modifiers, settings) be added without changing the file layout; bump
// MetaProgress.CurrentVersion and add a step to Migrate only when an existing field changes meaning.
[Serializable]
public class MetaSave
{
    public int version = MetaProgress.CurrentVersion;
    public int currency;
    public long totalEarned;
    public int runs;
    public int victories;
    public List<MetaEntry> ranks = new List<MetaEntry>();
    public List<string> unlocked = new List<string>();
    public List<MetaRecord> records = new List<MetaRecord>();
    public List<string> modifiers = new List<string>();   // run modifiers chosen in the menu (RunModifiers)
    public int bestThreat = -1;   // наибольшая угроза, на которой одержана победа (-1 — побед с испытаниями не было; 10.11.2026)
    // Totals over all counted runs (added 09.11.2026; older saves start them from zero).
    public long totalKills;
    public long totalDamage;
    public double totalSeconds;
    public int totalLevels;
    public bool hasLastRun;
    public MetaLastRun lastRun = new MetaLastRun();
    // Бесконечный режим (12.11.2026; у старых сохранений — нули).
    public float bestEndlessSeconds;
    public int bestEndlessScore, bestEndlessRound, endlessRuns;
}

public struct EndlessResult
{
    public int reward;
    public bool newBestTime, newBestScore, newBestRound;
}

public class MetaUpgrade
{
    public string id, title, description;
    public UpgradeEffect effect;
    public float amountPerRank;
    public int maxRank, baseCost;
    public float costGrowth = 1.4f;

    public int Cost(int rank) { return Mathf.Max(1, Mathf.RoundToInt(baseCost * Mathf.Pow(costGrowth, rank))); }
}

public class MetaUnlock
{
    public string id, title;
    public int cost;
    // Снято с продажи: оружие не выбирается как основное (мины, турель, дроны — только карточки/проекты). Запись и ключ в meta.json сохраняются.
    public bool retired;
}

public struct RunReward
{
    public int amount;
    public float multiplier;
    public bool newBestTime, newBestKills;
    public float bestTime;
    public int bestKills;
    public int threat;            // угроза забега (сумма испытаний)
    public bool newBestThreat;    // победа на угрозе выше прежнего рекорда
    public int firstVictoryBonus; // разовый бонус за первую победу (0, если не первая)
    public int trophy;            // Детали за элит от карточки «Трофей» (уже в amount)
}

public static class MetaProgress
{
    public const int CurrentVersion = 1;
    public static readonly string CurrencyName = GameText.Source("metaprogress.170");
    public const int FirstVictoryBonus = 500;   // разовая награда за первую победу над Стражем (она же открывает «Испытания»)

    // To add a permanent upgrade, append an entry; ranks are stored by id.
    public static readonly MetaUpgrade[] Catalog =
    {
        new MetaUpgrade { id = "meta_health", title = GameText.Source("metaprogress.171"), description = GameText.Source("metaprogress.172"),
            effect = UpgradeEffect.MaxHealth, amountPerRank = 10f, maxRank = 10, baseCost = 60, costGrowth = 1.35f },
        new MetaUpgrade { id = "meta_damage", title = GameText.Source("metaprogress.173"), description = GameText.Source("metaprogress.174"),
            effect = UpgradeEffect.Damage, amountPerRank = 0.04f, maxRank = 10, baseCost = 80, costGrowth = 1.35f },
        new MetaUpgrade { id = "meta_pickup", title = GameText.Source("metaprogress.175"), description = GameText.Source("metaprogress.176"),
            effect = UpgradeEffect.Pickup, amountPerRank = 0.1f, maxRank = 5, baseCost = 50, costGrowth = 1.4f },
        new MetaUpgrade { id = "meta_speed", title = GameText.Source("metaprogress.177"), description = GameText.Source("metaprogress.178"),
            effect = UpgradeEffect.Movement, amountPerRank = 0.03f, maxRank = 5, baseCost = 100, costGrowth = 1.5f },
    };

    // Things locked until opened. Anything not listed here is available from the start. Costs are paid in
    // currency today; a later unlock can instead be granted by code (an achievement) through Unlock(id).
    public static readonly MetaUnlock[] Unlocks =
    {
        new MetaUnlock { id = "weapon:2", title = GameText.Source("metaprogress.179"), cost = 150 },
        new MetaUnlock { id = "weapon:3", title = GameText.Source("metaprogress.180"), cost = 300 },
        new MetaUnlock { id = "weapon:4", title = GameText.Source("metaprogress.181"), cost = 300 },
        new MetaUnlock { id = "weapon:5", title = GameText.Source("metaprogress.182"), cost = 250, retired = true },
        new MetaUnlock { id = "weapon:6", title = GameText.Source("metaprogress.183"), cost = 400 },
        new MetaUnlock { id = "weapon:7", title = GameText.Source("metaprogress.184"), cost = 350 },
        new MetaUnlock { id = "weapon:8", title = GameText.Source("metaprogress.185"), cost = 450 },
        new MetaUnlock { id = "weapon:9", title = GameText.Source("metaprogress.186"), cost = 400 },
        new MetaUnlock { id = "weapon:10", title = GameText.Source("metaprogress.187"), cost = 500 },
        new MetaUnlock { id = "weapon:11", title = GameText.Source("metaprogress.188"), cost = 500, retired = true },
        new MetaUnlock { id = "weapon:12", title = GameText.Source("metaprogress.189"), cost = 550, retired = true },
    };

    private static MetaSave data;
    private static string path;
    private static bool persist;

    public static MetaSave Data { get { EnsureLoaded(); return data; } }
    public static int Currency { get { return Data.currency; } }

    private static void EnsureLoaded()
    {
        if (data != null) return;
        // Batch checks use a throw-away in-memory save so they never touch or depend on a player's file.
        persist = !Application.isBatchMode;
        path = Path.Combine(Application.persistentDataPath, "meta.json");
        data = persist ? Load(path) : new MetaSave();
    }

    // Test hooks.
    // Dev-переключатель (23.11.2026): по умолчанию выключен, оружия открываются за валюту. Включён сам только в тест-арене
    // (GameSession.TestMode); для ручной проверки можно поставить true из кода или автопроверки. UseMemory всегда сбрасывает его.
    private static bool devUnlockAllWeapons;
    public static bool DevUnlockAllWeapons
    {
        get { return devUnlockAllWeapons || GameSession.TestMode; }
        set { devUnlockAllWeapons = value; }
    }

    public static void UseMemory() { persist = false; data = new MetaSave(); devUnlockAllWeapons = false; }
    public static void UseFile(string file) { persist = true; path = file; data = Load(file); }

    public static MetaSave Load(string file)
    {
        MetaSave loaded = TryRead(file);
        if (loaded != null) return loaded;
        loaded = TryRead(file + ".bak");
        if (loaded != null) return loaded;
        try { if (File.Exists(file)) File.Copy(file, file + ".corrupt", true); } catch (Exception) { }
        return new MetaSave();
    }

    private static MetaSave TryRead(string file)
    {
        try
        {
            if (!File.Exists(file)) return null;
            MetaSave read = JsonUtility.FromJson<MetaSave>(File.ReadAllText(file));
            if (read == null || read.version < 1 || read.version > CurrentVersion) return null;
            Migrate(read);
            read.currency = Mathf.Max(0, read.currency);
            if (read.ranks == null) read.ranks = new List<MetaEntry>();
            if (read.unlocked == null) read.unlocked = new List<string>();
            if (read.records == null) read.records = new List<MetaRecord>();
            if (read.modifiers == null) read.modifiers = new List<string>();
            if (read.lastRun == null) { read.lastRun = new MetaLastRun(); read.hasLastRun = false; }
            return read;
        }
        catch (Exception) { return null; }
    }

    private static void Migrate(MetaSave save)
    {
        // Version 1 is current; later versions convert older layouts here, one step at a time.
        save.version = CurrentVersion;
    }

    public static void Save()
    {
        if (!persist || data == null) return;
        try
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
        catch (Exception exception) { Debug.LogWarning("Meta save failed: " + exception.Message); }
    }

    public static MetaUpgrade Find(string id)
    {
        foreach (MetaUpgrade upgrade in Catalog) if (upgrade.id == id) return upgrade;
        return null;
    }

    public static int Rank(string id)
    {
        foreach (MetaEntry entry in Data.ranks) if (entry.key == id) return entry.value;
        return 0;
    }

    public static bool CanBuy(MetaUpgrade upgrade)
    {
        return upgrade != null && Rank(upgrade.id) < upgrade.maxRank && Currency >= upgrade.Cost(Rank(upgrade.id));
    }

    public static bool Buy(string id)
    {
        MetaUpgrade upgrade = Find(id);
        if (!CanBuy(upgrade)) return false;
        int rank = Rank(id);
        Data.currency -= upgrade.Cost(rank);
        MetaEntry entry = Data.ranks.Find(e => e.key == id);
        if (entry == null) Data.ranks.Add(new MetaEntry { key = id, value = rank + 1 });
        else entry.value = rank + 1;
        Save();
        return true;
    }

    // Sum of permanent bonuses for an effect: multiplier bonus (0.08 = +8%), or raw HP for MaxHealth.
    public static float Bonus(UpgradeEffect effect)
    {
        float sum = 0f;
        foreach (MetaUpgrade upgrade in Catalog)
            if (upgrade.effect == effect) sum += upgrade.amountPerRank * Rank(upgrade.id);
        return sum;
    }

    public static string EffectText(MetaUpgrade upgrade, int rank)
    {
        float value = upgrade.amountPerRank * rank;
        return upgrade.effect == UpgradeEffect.MaxHealth ? "+" + Mathf.RoundToInt(value) + " HP" :
            "+" + Mathf.RoundToInt(value * 100f) + "%";
    }

    public static bool IsUnlocked(string id) { return Data.unlocked.Contains(id); }

    public static void Unlock(string id)
    {
        if (string.IsNullOrEmpty(id) || Data.unlocked.Contains(id)) return;
        Data.unlocked.Add(id);
        Save();
    }

    public static string WeaponKey(int weapon) { return "weapon:" + weapon; }

    public static MetaUnlock FindUnlock(string id)
    {
        foreach (MetaUnlock unlock in Unlocks) if (unlock.id == id) return unlock;
        return null;
    }

    public static bool IsWeaponUnlocked(int weapon)
    {
        if (DevUnlockAllWeapons) return true;
        MetaUnlock unlock = FindUnlock(WeaponKey(weapon));
        return unlock == null || IsUnlocked(unlock.id);
    }

    // Spends currency to open a listed unlock; the payment and the unlock are written together.
    public static bool TryUnlock(string id)
    {
        MetaUnlock unlock = FindUnlock(id);
        if (unlock == null || unlock.retired || IsUnlocked(id) || Currency < unlock.cost) return false;
        Data.currency -= unlock.cost;
        Unlock(id);
        return true;
    }

    public static MetaRecord Record(string key)
    {
        foreach (MetaRecord record in Data.records) if (record.key == key) return record;
        return null;
    }

    // Currency for a finished run; surviving to the Guardian and killing it pays the most.
    public static int Reward(bool victory, float seconds, int kills, int level)
    {
        return Mathf.Max(0, kills) + Mathf.FloorToInt(Mathf.Max(0f, seconds) / 10f) * 2 +
            Mathf.Max(1, level) * 3 + (victory ? 250 : 0);
    }

    // Бесконечный режим (12.11.2026): открыт после первой победы; свои рекорды; детали как за поражение + 50 за круг.
    public static bool EndlessUnlocked => Data.victories > 0;

    public static EndlessResult RecordEndless(int weapon, float seconds, int kills, int level, int score, int rounds, long damage, RunReport report = null, int wave = 0)
    {
        var result = new EndlessResult { reward = Mathf.RoundToInt(Reward(false, seconds, kills, level) * RunModifiers.RewardMultiplier) + 50 * Mathf.Max(0, rounds) };
        result.newBestTime = seconds > Data.bestEndlessSeconds;
        result.newBestScore = score > Data.bestEndlessScore;
        result.newBestRound = rounds > Data.bestEndlessRound;
        Data.bestEndlessSeconds = Mathf.Max(Data.bestEndlessSeconds, seconds);
        Data.bestEndlessScore = Mathf.Max(Data.bestEndlessScore, score);
        Data.bestEndlessRound = Mathf.Max(Data.bestEndlessRound, rounds);
        Data.endlessRuns++;
        Data.currency += result.reward;
        Data.totalEarned += result.reward;
        Data.runs++;
        Data.totalKills += Mathf.Max(0, kills);
        Data.totalDamage += Math.Max(0L, damage);
        Data.totalSeconds += Mathf.Max(0f, seconds);
        Data.totalLevels += Mathf.Max(0, level);
        Data.hasLastRun = true;
        Data.lastRun = new MetaLastRun { weapon = weapon, seconds = seconds, kills = kills, level = level,
            damage = damage, wave = wave, reward = result.reward, report = report,
            modifiers = RunModifiers.Summary(), date = DateTime.Now.ToString("dd.MM.yyyy HH:mm") };
        Save();
        return result;
    }

    public static RunReward RecordRun(int weapon, bool victory, float seconds, int kills, int level,
        long damage = 0, int wave = 0, RunReport report = null)
    {
        float multiplier = RunModifiers.RewardMultiplier;
        RunReward result = new RunReward { multiplier = multiplier,
            amount = Mathf.RoundToInt(Reward(victory, seconds, kills, level) * multiplier) };
        result.trophy = Mathf.Max(0, CombatStats.RunTrophyParts);
        result.amount += result.trophy;
        result.threat = RunModifiers.Threat;
        if (victory && Data.victories == 0) { result.firstVictoryBonus = FirstVictoryBonus; result.amount += FirstVictoryBonus; }
        if (victory && result.threat > 0 && result.threat > Data.bestThreat) { result.newBestThreat = true; Data.bestThreat = result.threat; }
        Data.currency += result.amount;
        Data.totalEarned += result.amount;
        Data.runs++;
        if (victory) Data.victories++;
        Data.totalKills += Mathf.Max(0, kills);
        Data.totalDamage += Math.Max(0L, damage);
        Data.totalSeconds += Mathf.Max(0f, seconds);
        Data.totalLevels += Mathf.Max(0, level);
        foreach (string key in new[] { "all", WeaponKey(weapon) })
        {
            MetaRecord record = Record(key);
            if (record == null) { record = new MetaRecord { key = key }; Data.records.Add(record); }
            if (key == "all")
            {
                result.newBestTime = seconds > record.bestTime;
                result.newBestKills = kills > record.bestKills;
            }
            record.runs++;
            if (victory) record.victories++;
            record.bestTime = Mathf.Max(record.bestTime, seconds);
            record.bestKills = Mathf.Max(record.bestKills, kills);
            record.bestLevel = Mathf.Max(record.bestLevel, level);
            record.bestDamage = Math.Max(record.bestDamage, damage);
            if (key == "all") { result.bestTime = record.bestTime; result.bestKills = record.bestKills; }
        }
        Data.hasLastRun = true;
        Data.lastRun = new MetaLastRun { weapon = weapon, victory = victory, seconds = seconds, kills = kills, level = level,
            wave = wave, damage = damage, reward = result.amount, multiplier = multiplier, modifiers = RunModifiers.Summary(), report = report,
            date = DateTime.Now.ToString("dd.MM.yyyy HH:mm"), newBestTime = result.newBestTime, newBestKills = result.newBestKills };
        Save();
        return result;
    }
}
