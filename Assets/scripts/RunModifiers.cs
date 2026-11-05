using System.Collections.Generic;
using UnityEngine;

public class RunModifier
{
    public string id, title, description;
    public float reward;               // added to the reward multiplier (0.2 = +20%)
    public string[] conflicts = new string[0];
}

// Optional run conditions chosen in the menu before a run. Each makes the run harder and pays more currency.
// The chosen ids are stored in the save (MetaSave.modifiers) so the choice is remembered and survives restarts.
// To add one: append to Catalog and read it where it applies (IsActive / a getter below).
public static class RunModifiers
{
    public const float MaxRewardMultiplier = 3f;

    public static readonly RunModifier[] Catalog =
    {
        new RunModifier { id = "mod_fast", title = "Быстрые враги", description = "Враги движутся на 25% быстрее", reward = 0.15f },
        new RunModifier { id = "mod_thick", title = "Толстые враги", description = "У врагов на 50% больше здоровья", reward = 0.15f },
        new RunModifier { id = "mod_fragile", title = "Хрупкий герой", description = "Здоровье героя на 40% меньше", reward = 0.25f },
        new RunModifier { id = "mod_horde", title = "Орда", description = "Врагов на арене в 1.5 раза больше и они появляются чаще", reward = 0.25f },
        new RunModifier { id = "mod_short", title = "Быстрый финал", description = "Страж приходит через 5 минут вместо 10", reward = 0.1f },
    };

    public static RunModifier Find(string id)
    {
        foreach (RunModifier modifier in Catalog) if (modifier.id == id) return modifier;
        return null;
    }

    public static bool IsActive(string id) { return MetaProgress.Data.modifiers.Contains(id); }

    // Switches a modifier on or off; turning one on switches off any it conflicts with. Returns the new state.
    public static bool Toggle(string id)
    {
        RunModifier modifier = Find(id);
        if (modifier == null) return false;
        List<string> active = MetaProgress.Data.modifiers;
        bool on = !active.Contains(id);
        if (on)
        {
            foreach (string conflict in modifier.conflicts) active.Remove(conflict);
            foreach (RunModifier other in Catalog)
                if (System.Array.IndexOf(other.conflicts, id) >= 0) active.Remove(other.id);
            active.Add(id);
        }
        else active.Remove(id);
        MetaProgress.Save();
        return on;
    }

    public static void Clear()
    {
        MetaProgress.Data.modifiers.Clear();
        MetaProgress.Save();
    }

    public static int ActiveCount
    {
        get
        {
            int count = 0;
            foreach (RunModifier modifier in Catalog) if (IsActive(modifier.id)) count++;
            return count;
        }
    }

    public static float RewardMultiplier
    {
        get
        {
            float sum = 1f;
            foreach (RunModifier modifier in Catalog) if (IsActive(modifier.id)) sum += modifier.reward;
            return Mathf.Min(MaxRewardMultiplier, sum);
        }
    }

    public static float EnemySpeed { get { return IsActive("mod_fast") ? 1.25f : 1f; } }
    public static float EnemyHealth { get { return IsActive("mod_thick") ? 1.5f : 1f; } }
    public static float HeroHealth { get { return IsActive("mod_fragile") ? 0.6f : 1f; } }
    public static float AliveLimit { get { return IsActive("mod_horde") ? 1.5f : 1f; } }
    public static float SpawnInterval { get { return IsActive("mod_horde") ? 0.75f : 1f; } }
    public static float SurvivalSeconds(float baseSeconds) { return IsActive("mod_short") ? Mathf.Min(baseSeconds, 300f) : baseSeconds; }

    public static string Summary()
    {
        var names = new List<string>();
        foreach (RunModifier modifier in Catalog) if (IsActive(modifier.id)) names.Add(modifier.title);
        return names.Count == 0 ? "Условия: обычные" : "Условия: " + string.Join(", ", names) + "  •  награда ×" + RewardMultiplier.ToString("0.##");
    }
}
