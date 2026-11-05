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
}

public struct RunReward
{
    public int amount;
    public float multiplier;
    public bool newBestTime, newBestKills;
    public float bestTime;
    public int bestKills;
}

public static class MetaProgress
{
    public const int CurrentVersion = 1;
    public const string CurrencyName = "Детали";

    // To add a permanent upgrade, append an entry; ranks are stored by id.
    public static readonly MetaUpgrade[] Catalog =
    {
        new MetaUpgrade { id = "meta_health", title = "Усиленный корпус", description = "Больше здоровья в начале забега",
            effect = UpgradeEffect.MaxHealth, amountPerRank = 10f, maxRank = 10, baseCost = 60, costGrowth = 1.35f },
        new MetaUpgrade { id = "meta_damage", title = "Калибровка оружия", description = "Выше урон пуль",
            effect = UpgradeEffect.Damage, amountPerRank = 0.04f, maxRank = 10, baseCost = 80, costGrowth = 1.35f },
        new MetaUpgrade { id = "meta_pickup", title = "Магнитный захват", description = "Больше радиус сбора опыта",
            effect = UpgradeEffect.Pickup, amountPerRank = 0.1f, maxRank = 5, baseCost = 50, costGrowth = 1.4f },
        new MetaUpgrade { id = "meta_speed", title = "Лёгкие приводы", description = "Выше скорость движения",
            effect = UpgradeEffect.Movement, amountPerRank = 0.03f, maxRank = 5, baseCost = 100, costGrowth = 1.5f },
    };

    // Things locked until opened. Anything not listed here is available from the start. Costs are paid in
    // currency today; a later unlock can instead be granted by code (an achievement) through Unlock(id).
    public static readonly MetaUnlock[] Unlocks =
    {
        new MetaUnlock { id = "weapon:2", title = "Дробовик", cost = 150 },
        new MetaUnlock { id = "weapon:3", title = "Тяжёлая винтовка", cost = 300 },
        new MetaUnlock { id = "weapon:4", title = "Пила-бумеранг", cost = 300 },
        new MetaUnlock { id = "weapon:5", title = "Сапёрные мины", cost = 250 },
        new MetaUnlock { id = "weapon:6", title = "Ракетница", cost = 400 },
        new MetaUnlock { id = "weapon:7", title = "Электродуга", cost = 350 },
        new MetaUnlock { id = "weapon:8", title = "Огнемёт", cost = 450 },
        new MetaUnlock { id = "weapon:9", title = "Ледяная пушка", cost = 400 },
        new MetaUnlock { id = "weapon:10", title = "Рельсовая пушка", cost = 500 },
        new MetaUnlock { id = "weapon:11", title = "Турель", cost = 500 },
        new MetaUnlock { id = "weapon:12", title = "Дроны-стрелки", cost = 550 },
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
    public static void UseMemory() { persist = false; data = new MetaSave(); }
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
        MetaUnlock unlock = FindUnlock(WeaponKey(weapon));
        return unlock == null || IsUnlocked(unlock.id);
    }

    // Spends currency to open a listed unlock; the payment and the unlock are written together.
    public static bool TryUnlock(string id)
    {
        MetaUnlock unlock = FindUnlock(id);
        if (unlock == null || IsUnlocked(id) || Currency < unlock.cost) return false;
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

    public static RunReward RecordRun(int weapon, bool victory, float seconds, int kills, int level)
    {
        float multiplier = RunModifiers.RewardMultiplier;
        RunReward result = new RunReward { multiplier = multiplier,
            amount = Mathf.RoundToInt(Reward(victory, seconds, kills, level) * multiplier) };
        Data.currency += result.amount;
        Data.totalEarned += result.amount;
        Data.runs++;
        if (victory) Data.victories++;
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
            if (key == "all") { result.bestTime = record.bestTime; result.bestKills = record.bestKills; }
        }
        Save();
        return result;
    }
}
