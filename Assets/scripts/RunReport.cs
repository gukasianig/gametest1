using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RunUpgradeReport
{
    public string id;
    public int rank;
}

[Serializable]
public class RunHeroReport
{
    public int weapon;
    public WeaponElement branch;
    public int branchRank;
    public bool evolved;
    public List<RunUpgradeReport> upgrades = new List<RunUpgradeReport>();
}

[Serializable]
public class RunDamageReport
{
    public HeroDamageCause cause;
    public long amount;
}

[Serializable]
public class RunBossReport
{
    public BossKind kind;
    public float seconds;
    public bool defeated;
}

// Save stable identifiers only; display strings are resolved in the selected language.
[Serializable]
public class RunReport
{
    public List<RunHeroReport> heroes = new List<RunHeroReport>();
    public List<RunDamageReport> incoming = new List<RunDamageReport>();
    public List<RunBossReport> bosses = new List<RunBossReport>();
    public HeroDamageCause deathCause;
    public bool hasDeathCause;
    public bool endless;
    public int rounds, score;

    public long DamageTaken
    {
        get { long sum = 0; foreach (var hit in incoming) sum += hit.amount; return sum; }
    }

    public float BossSeconds
    {
        get { float sum = 0; foreach (var boss in bosses) sum += boss.seconds; return sum; }
    }

    public static IEnumerable<UpgradeDefinition> Definitions(PlayerProgression progression)
    {
        if (progression.catalog != null)
            foreach (var entry in progression.catalog.entries) if (entry != null) yield return entry;
        foreach (var entry in PlayerProgression.ChestUpgrades) yield return entry;
        foreach (var entry in PlayerProgression.SynergyUpgrades) yield return entry;
        foreach (var entry in PlayerProgression.EvolutionUpgrades) yield return entry;
    }

    public static string Duration(float seconds)
    {
        int value = Mathf.FloorToInt(Mathf.Max(0, seconds));
        return (value / 60).ToString("00") + ":" + (value % 60).ToString("00");
    }
}
