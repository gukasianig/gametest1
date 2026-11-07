using UnityEngine;

// Run-wide bonuses that are not simple multipliers: crits, piercing, armor, regeneration, drops and the "cursed" penalties.
// PlayerProgression writes them from the chosen upgrades (ApplyStats); weapons, bullets and pickups read them.
// Every value is neutral (0 or 1) outside a run.
public static class CombatStats
{
    public const float BaseCritMultiplier = 1.5f;
    public static float CritChance { get; private set; }
    public static float CritMultiplier { get; private set; } = BaseCritMultiplier;
    public static int Pierce { get; private set; }
    public static int Ricochet { get; private set; }

    // Survival.
    public static float Armor;                 // share of incoming damage removed (0..0.6)
    public static float RegenPerSecond;        // HP per second
    public static float VampirismPerKill;      // HP per enemy kill
    public static float DashCooldownReduction; // seconds
    public static float DashDistanceBonus;     // metres

    // Combat.
    public static int ExtraPellets;            // extra bullets per shot (ordinary weapons)
    public static float ExplosionRadius;       // metres, 0 = no explosive bullets
    public const float ExplosionDamageShare = 0.4f;
    public static float EliteDamage;           // bonus damage against elites and the boss
    public static float RangeMultiplier = 1f;  // ordinary weapons
    public static float OrbitSpeedBonus;       // +share of orbit rotation speed
    public static float OrbitCritShare;        // share of the hero's crit chance used by orbiting satellites

    // Progress.
    public static float PickupSpeedBonus;      // +share of crystal pull speed
    public static float DropLuck;              // +share of repair kit / magnet chance
    public static float ExperienceGain;        // +share of experience from crystals

    // Cursed upgrade penalties.
    public static float EnemySpeedCurse = 1f;
    public static float EnemyHealthCurse = 1f;

    public static void Set(float critChance, float critBonus, int pierce, int ricochet = 0)
    {
        CritChance = Mathf.Clamp01(critChance);
        CritMultiplier = BaseCritMultiplier + Mathf.Max(0f, critBonus);
        Pierce = Mathf.Max(0, pierce);
        Ricochet = Mathf.Max(0, ricochet);
    }

    public static void Reset()
    {
        Set(0f, 0f, 0, 0);
        Armor = RegenPerSecond = VampirismPerKill = DashCooldownReduction = DashDistanceBonus = 0f;
        ExtraPellets = 0;
        ExplosionRadius = EliteDamage = OrbitSpeedBonus = OrbitCritShare = 0f;
        RangeMultiplier = 1f;
        PickupSpeedBonus = DropLuck = ExperienceGain = 0f;
        EnemySpeedCurse = EnemyHealthCurse = 1f;
    }

    // Rolls one hit: returns the (possibly increased) damage. A crit always deals at least 1 more than a normal hit.
    public static int Roll(int damage, out bool crit)
    {
        return Roll(damage, CritChance, out crit);
    }

    public static int Roll(int damage, float chance, out bool crit)
    {
        crit = damage > 0 && chance > 0f && Random.value < chance;
        if (!crit) return damage;
        return Mathf.Max(damage + 1, Mathf.RoundToInt(damage * CritMultiplier));
    }

    // Elites (crowned enemies) and the boss take extra damage from the "elite hunter" upgrade.
    public static float TargetMultiplier(Health target)
    {
        if (EliteDamage <= 0f || target == null) return 1f;
        bool strong = target.name.StartsWith("Elite") || target.GetComponent<BossController>() != null;
        return strong ? 1f + EliteDamage : 1f;
    }
}
