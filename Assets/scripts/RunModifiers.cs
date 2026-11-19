using System.Collections.Generic;
using UnityEngine;

public class RunModifier
{
    public string id, title, description;
    public float reward;               // added to the reward multiplier (0.2 = +20%)
    public int threat = 1;             // «угроза»: сумма у включённых испытаний — уровень сложности забега (рекорд в MetaSave.bestThreat)
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
        new RunModifier { id = "mod_fast", title = GameText.Source("runmodifiers.260"), description = GameText.Source("runmodifiers.261"), reward = 0.15f, threat = 1 },
        new RunModifier { id = "mod_thick", title = GameText.Source("runmodifiers.262"), description = GameText.Source("runmodifiers.263"), reward = 0.15f, threat = 2 },
        new RunModifier { id = "mod_fragile", title = GameText.Source("runmodifiers.264"), description = GameText.Source("runmodifiers.265"), reward = 0.25f, threat = 2 },
        new RunModifier { id = "mod_horde", title = GameText.Source("runmodifiers.266"), description = GameText.Source("runmodifiers.267"), reward = 0.25f, threat = 2 },
        new RunModifier { id = "mod_short", title = GameText.Source("runmodifiers.268"), description = GameText.Source("runmodifiers.269"), reward = 0.1f, threat = 1 },
        // 10.11.2026: ещё четыре испытания (выбор владельца).
        new RunModifier { id = "mod_boss", title = GameText.Source("runmodifiers.270"), description = GameText.Source("runmodifiers.271"), reward = 0.15f, threat = 2 },
        new RunModifier { id = "mod_nokits", title = GameText.Source("runmodifiers.272"), description = GameText.Source("runmodifiers.273"), reward = 0.2f, threat = 2 },
        new RunModifier { id = "mod_elite", title = GameText.Source("runmodifiers.274"), description = GameText.Source("runmodifiers.275"), reward = 0.2f, threat = 2 },
        new RunModifier { id = "mod_heavy", title = GameText.Source("runmodifiers.276"), description = GameText.Source("runmodifiers.277"), reward = 0.2f, threat = 3 },
    };

    public static RunModifier Find(string id)
    {
        foreach (RunModifier modifier in Catalog) if (modifier.id == id) return modifier;
        return null;
    }

    // До первой победы испытания закрыты: сохранённый выбор не действует.
    public static bool IsActive(string id) { return Unlocked && MetaProgress.Data.modifiers.Contains(id); }

    // Switches a modifier on or off; turning one on switches off any it conflicts with. Returns the new state.
    public static bool Toggle(string id)
    {
        RunModifier modifier = Find(id);
        if (modifier == null) return false;
        if (!Unlocked) return false;
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
    public static float BossHealth { get { return IsActive("mod_boss") ? 1.6f : 1f; } }
    public static float BossAttackDelay { get { return IsActive("mod_boss") ? 0.6f : 1f; } }
    public static bool NoHealthKits { get { return IsActive("mod_nokits"); } }
    public static float RepairHeal { get { return IsActive("mod_nokits") ? 0.5f : 1f; } }
    public static int EliteMultiplier { get { return IsActive("mod_elite") ? 2 : 1; } }
    public static float HeroDamageTaken { get { return IsActive("mod_heavy") ? 1.4f : 1f; } }

    // Угроза забега — сумма угроз включённых испытаний; открыто ли меню испытаний (после первой победы).
    public static int Threat
    {
        get
        {
            int sum = 0;
            foreach (RunModifier modifier in Catalog) if (IsActive(modifier.id)) sum += modifier.threat;
            return sum;
        }
    }

    public static int MaxThreat
    {
        get
        {
            int sum = 0;
            foreach (RunModifier modifier in Catalog) sum += modifier.threat;
            return sum;
        }
    }

    public static bool Unlocked { get { return MetaProgress.Data.victories > 0; } }

    public static float SurvivalSeconds(float baseSeconds) { return IsActive("mod_short") ? Mathf.Min(baseSeconds, 300f) : baseSeconds; }

    public static string Summary()
    {
        var names = new List<string>();
        foreach (RunModifier modifier in Catalog) if (IsActive(modifier.id)) names.Add(modifier.title);
        return names.Count == 0 ? GameText.Source("runmodifiers.278") : GameText.Source("runmodifiers.279") + Threat + ": " + string.Join(", ", names) + GameText.Source("runmodifiers.280") + RewardMultiplier.ToString("0.##");
    }
}
