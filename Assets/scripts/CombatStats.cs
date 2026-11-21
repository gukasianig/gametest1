using System.Collections.Generic;
using UnityEngine;

// Bonuses that are not simple multipliers: crits, piercing, armor, regeneration, drops and the "cursed" penalties.
// 11.11.2026 (кооператив, этап 2): у каждого героя свой набор — HeroStats в его PlayerProgression.
// PlayerProgression writes them from the chosen upgrades (ApplyStats); weapons, bullets and pickups read their owner's set.
// Every value is neutral (0 or 1) outside a run.
public sealed class HeroStats
{
    public const float BaseCritMultiplier = 1.5f;
    public const float SteelWillThreshold = 0.4f;
    public const float ExplosionDamageShare = 0.4f;

    public float CritChance { get; private set; }
    public float CritMultiplier { get; private set; } = BaseCritMultiplier;
    public int Pierce { get; private set; }
    public int Ricochet { get; private set; }

    // Survival.
    public float Armor;                 // share of incoming damage removed (0..0.6)
    public float RegenPerSecond;        // HP per second
    public bool SteelWill;              // синергия «Стальная воля»: при HP < 40% реген ×3 и броня +15% (10.11.2026)
    public float VampirismPerKill;      // HP per enemy kill
    public float DashCooldownReduction; // seconds
    public float DashDistanceBonus;     // metres

    // Combat.
    public int ExtraPellets;            // extra bullets per shot (ordinary weapons)
    public float ExplosionRadius;       // metres, 0 = no explosive bullets
    public float EliteDamage;           // bonus damage against elites and the boss
    public float RangeMultiplier = 1f;  // ordinary weapons
    public float OrbitSpeedBonus;       // +share of orbit rotation speed
    public float OrbitCritShare;        // share of the hero's crit chance used by orbiting satellites

    // Progress.
    public float PickupSpeedBonus;      // +share of crystal pull speed
    public float DropLuck;              // luck: rare/special card weight, drops, small chest, +crit (19.11.2026)
    public float ExperienceGain;        // +share of experience from crystals

    // Cursed upgrade penalties (act on the whole arena: CombatStats takes the strongest of all heroes).
    public float EnemySpeedCurse = 1f;
    public float EnemyHealthCurse = 1f;

    // Карточки 19.11.2026 (CardEffects.cs): защита, стихии, особые выстрелы, импульсы.
    public float Dodge;                 // шанс избежать удара (0..0.25)
    public float ProjectileGuard;       // доля урона снарядов, которая не проходит (0..0.4)
    public float DashInvulnerabilityBonus; // секунды к неуязвимости рывка
    public int TrophyParts;             // Деталей за каждую элиту
    public int BurnRank, PoisonRank, FrostRank, ShockRank; // стихийные карточки
    public int SplitShot;               // осколков после первого попадания (0 = нет)
    public int Retaliate;               // ранг ответного импульса
    public int PulseAura;               // ранг импульсной ауры
    public int ChargedShot;             // ранг заряженного выстрела
    public bool LastChance, CritPierce, BurningShot, BlastDash, Gamble;

    public void Set(float critChance, float critBonus, int pierce, int ricochet = 0)
    {
        CritChance = Mathf.Clamp01(critChance);
        CritMultiplier = BaseCritMultiplier + Mathf.Max(0f, critBonus);
        Pierce = Mathf.Max(0, pierce);
        Ricochet = Mathf.Max(0, ricochet);
    }

    public void Reset()
    {
        Set(0f, 0f, 0, 0);
        Armor = RegenPerSecond = VampirismPerKill = DashCooldownReduction = DashDistanceBonus = 0f;
        SteelWill = false;
        ExtraPellets = 0;
        ExplosionRadius = EliteDamage = OrbitSpeedBonus = OrbitCritShare = 0f;
        RangeMultiplier = 1f;
        PickupSpeedBonus = DropLuck = ExperienceGain = 0f;
        EnemySpeedCurse = EnemyHealthCurse = 1f;
        Dodge = ProjectileGuard = DashInvulnerabilityBonus = 0f;
        TrophyParts = BurnRank = PoisonRank = FrostRank = ShockRank = SplitShot = Retaliate = PulseAura = ChargedShot = 0;
        LastChance = CritPierce = BurningShot = BlastDash = Gamble = false;
    }

    // Rolls one hit: returns the (possibly increased) damage. A crit always deals at least 1 more than a normal hit.
    public int Roll(int damage, out bool crit) { return Roll(damage, CritChance, out crit); }

    public int Roll(int damage, float chance, out bool crit)
    {
        crit = damage > 0 && chance > 0f && Random.value < chance;
        if (!crit) return damage;
        return Mathf.Max(damage + 1, Mathf.RoundToInt(damage * CritMultiplier));
    }

    // Elites (crowned enemies) and the boss take extra damage from the "elite hunter" upgrade.
    public float TargetMultiplier(Health target)
    {
        if (EliteDamage <= 0f || target == null) return 1f;
        bool strong = target.name.StartsWith("Elite") || target.GetComponent<BossController>() != null || target.GetComponent<MiniBoss>() != null;
        return strong ? 1f + EliteDamage : 1f;
    }
}

// Прежний общий доступ: значения игрока 1 (PlayerProgression.Instance) — для одиночной игры, экранов и Editor-проверок.
// Проклятия врагов и удача дропа действуют на всю арену и берутся как наибольшие среди героев.
public static class CombatStats
{
    public const float BaseCritMultiplier = HeroStats.BaseCritMultiplier;
    public const float SteelWillThreshold = HeroStats.SteelWillThreshold;
    public const float ExplosionDamageShare = HeroStats.ExplosionDamageShare;

    private static readonly HeroStats fallback = new HeroStats();
    private static readonly List<HeroStats> all = new List<HeroStats>();

    public static HeroStats Primary => PlayerProgression.Instance != null ? PlayerProgression.Instance.Stats : fallback;

    // Набор бонусов героя (или игрока 1, если у объекта нет своего прогресса).
    public static HeroStats Of(Component hero)
    {
        PlayerProgression progression = hero != null ? hero.GetComponentInParent<PlayerProgression>() : null;
        return progression != null ? progression.Stats : Primary;
    }

    public static void Register(HeroStats stats) { if (stats != null && !all.Contains(stats)) all.Add(stats); }
    public static void Unregister(HeroStats stats) { all.Remove(stats); }

    public static float CritChance => Primary.CritChance;
    public static float CritMultiplier => Primary.CritMultiplier;
    public static int Pierce => Primary.Pierce;
    public static int Ricochet => Primary.Ricochet;
    public static float Armor { get => Primary.Armor; set => Primary.Armor = value; }
    public static float RegenPerSecond { get => Primary.RegenPerSecond; set => Primary.RegenPerSecond = value; }
    public static bool SteelWill { get => Primary.SteelWill; set => Primary.SteelWill = value; }
    public static float VampirismPerKill { get => Primary.VampirismPerKill; set => Primary.VampirismPerKill = value; }
    public static float DashCooldownReduction { get => Primary.DashCooldownReduction; set => Primary.DashCooldownReduction = value; }
    public static float DashDistanceBonus { get => Primary.DashDistanceBonus; set => Primary.DashDistanceBonus = value; }
    public static int ExtraPellets { get => Primary.ExtraPellets; set => Primary.ExtraPellets = value; }
    public static float ExplosionRadius { get => Primary.ExplosionRadius; set => Primary.ExplosionRadius = value; }
    public static float EliteDamage { get => Primary.EliteDamage; set => Primary.EliteDamage = value; }
    public static float RangeMultiplier { get => Primary.RangeMultiplier; set => Primary.RangeMultiplier = value; }
    public static float OrbitSpeedBonus { get => Primary.OrbitSpeedBonus; set => Primary.OrbitSpeedBonus = value; }
    public static float OrbitCritShare { get => Primary.OrbitCritShare; set => Primary.OrbitCritShare = value; }
    public static float PickupSpeedBonus { get => Primary.PickupSpeedBonus; set => Primary.PickupSpeedBonus = value; }
    public static float ExperienceGain { get => Primary.ExperienceGain; set => Primary.ExperienceGain = value; }

    // На всю арену: сильнейшее среди героев.
    public static float DropLuck { get { float best = Primary.DropLuck; foreach (HeroStats s in all) best = Mathf.Max(best, s.DropLuck); return best; } }
    public static float EnemySpeedCurse { get { float best = Primary.EnemySpeedCurse; foreach (HeroStats s in all) best = Mathf.Max(best, s.EnemySpeedCurse); return best; } }
    public static float EnemyHealthCurse { get { float best = Primary.EnemyHealthCurse; foreach (HeroStats s in all) best = Mathf.Max(best, s.EnemyHealthCurse); return best; } }
    // «Азарт» у любого героя: аптечки на арене превращаются в магниты.
    public static bool GambleActive { get { bool any = Primary.Gamble; foreach (HeroStats s in all) any |= s.Gamble; return any; } }
    // Детали за элит (карточка «Трофей»), копятся за забег и прибавляются к награде.
    public static int RunTrophyParts;

    public static void Set(float critChance, float critBonus, int pierce, int ricochet = 0) { Primary.Set(critChance, critBonus, pierce, ricochet); }
    public static void Reset() { Primary.Reset(); }
    public static int Roll(int damage, out bool crit) { return Primary.Roll(damage, out crit); }
    public static int Roll(int damage, float chance, out bool crit) { return Primary.Roll(damage, chance, out crit); }
    public static float TargetMultiplier(Health target) { return Primary.TargetMultiplier(target); }
}
