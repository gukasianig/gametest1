using System.Collections.Generic;
using UnityEngine;

// Saved numeric IDs stay stable: pistol branches 5–7, rifle branches 8–10; other weapons retain elements.
public enum WeaponElement { None, Fire, Frost, Lightning, Poison, Revolver, Dual, Sticky, Assault, Minigun, Heavy }

// All keys include the weapon index, matching the existing weapon:N unlock keys.
public static class WeaponElements
{
    public const int MaxRank = 3;
    public const int PistolWeapon = 0;
    public const int RifleWeapon = 1;
    public static readonly string[] Names = { GameText.Source("weaponelements.419"), GameText.Source("weaponelements.420"), GameText.Source("weaponelements.421"), GameText.Source("weaponelements.422"), GameText.Source("weaponelements.423"),
        GameText.Source("weaponelements.433"), GameText.Source("weaponelements.434"), GameText.Source("weaponelements.435"),
        GameText.Source("rifle.assault.name"), GameText.Source("rifle.minigun.name"), GameText.Source("rifle.heavy.name") };
    public static readonly string[] Icons = { "", "el_fire", "el_frost", "el_lightning", "el_poison", "el_revolver", "el_dual", "el_sticky", "el_assault", "el_minigun", "el_heavy" };
    public static readonly Color[] Colors = { Color.white, new Color(1f, .42f, .2f),
        new Color(.35f, .8f, 1f), new Color(1f, .84f, .3f), new Color(.55f, .95f, .24f),
        new Color(1f, .72f, .28f), new Color(.45f, .85f, 1f), new Color(1f, .45f, .35f),
        new Color(.35f, .95f, .8f), new Color(1f, .75f, .3f), new Color(1f, .4f, .3f) };

    public static bool IsBranch(WeaponElement element) { return element >= WeaponElement.Revolver && element <= WeaponElement.Heavy; }
    public static bool IsRifleBranch(WeaponElement element) { return element >= WeaponElement.Assault && element <= WeaponElement.Heavy; }
    public static bool HasBranches(int weapon) { return weapon == PistolWeapon || weapon == RifleWeapon; }
    public static WeaponElement FirstBranch(int weapon) { return weapon == PistolWeapon ? WeaponElement.Revolver : WeaponElement.Assault; }
    public static WeaponElement LastBranch(int weapon) { return weapon == PistolWeapon ? WeaponElement.Sticky : WeaponElement.Heavy; }
    public static string RifleModelKey(WeaponElement element) { return IsRifleBranch(element) ? "Rifle_" + element : "Weapon1"; }

    // Pistol and rifle each have three exclusive branches; other weapons retain four elements.
    public static bool Valid(int weapon, WeaponElement element)
    {
        if (weapon < 0 || weapon >= 13) return false;
        if (weapon == PistolWeapon) return element >= WeaponElement.Revolver && element <= WeaponElement.Sticky;
        if (weapon == RifleWeapon) return IsRifleBranch(element);
        return element >= WeaponElement.Fire && element <= WeaponElement.Poison;
    }

    public static string Key(int weapon, WeaponElement element) { return "element:" + weapon + ":" + (int)element; }
    public static int Rank(int weapon, WeaponElement element)
    {
        return Valid(weapon, element) ? Mathf.Clamp(MetaProgress.Rank(Key(weapon, element)), 0, MaxRank) : 0;
    }
    public static int Cost(int rank) { return 120 + Mathf.Clamp(rank, 0, MaxRank) * 100; }
    public static WeaponElement Equipped(int weapon)
    {
        WeaponElement element = (WeaponElement)MetaProgress.Rank("element:selected:" + weapon);
        return MetaProgress.IsWeaponUnlocked(weapon) && Rank(weapon, element) > 0 ? element : WeaponElement.None;
    }

    private static void Set(string key, int value)
    {
        MetaEntry entry = MetaProgress.Data.ranks.Find(item => item.key == key);
        if (entry == null) MetaProgress.Data.ranks.Add(new MetaEntry { key = key, value = value });
        else entry.value = value;
    }

    // ВРЕМЕННО: улучшения оружия (стихии) в мастерской закрыты ключом. Вернуть: false. Уже купленные ранги и выбранная стихия работают как раньше.
    // Pistol and rifle are open; the other weapons wait for their own branch trees.
    public static readonly bool Locked = true;
    public static bool IsLocked(int weapon) { return Locked && !HasBranches(weapon); }

    public static bool CanBuy(int weapon, WeaponElement element)
    {
        return !IsLocked(weapon) && Valid(weapon, element) && MetaProgress.IsWeaponUnlocked(weapon) &&
            Rank(weapon, element) < MaxRank && MetaProgress.Currency >= Cost(Rank(weapon, element));
    }

    public static bool Buy(int weapon, WeaponElement element)
    {
        if (!CanBuy(weapon, element)) return false;
        int rank = Rank(weapon, element);
        MetaProgress.Data.currency -= Cost(rank);
        Set(Key(weapon, element), rank + 1);
        // Buying the first module equips it; later purchases never silently replace another module.
        if (Equipped(weapon) == WeaponElement.None) Set("element:selected:" + weapon, (int)element);
        MetaProgress.Save();
        return true;
    }

    public static bool Equip(int weapon, WeaponElement element)
    {
        if (weapon < 0 || weapon >= 13 || !MetaProgress.IsWeaponUnlocked(weapon) ||
            (element != WeaponElement.None && Rank(weapon, element) == 0)) return false;
        Set("element:selected:" + weapon, (int)element);
        MetaProgress.Save();
        return true;
    }

    // Тексты рангов веток пистолета: Револьвер 1–3, Двойные 1–3, Липкий 1–3 (game-text.json).
    private static readonly string[] BranchText =
    {
        "weaponelements.436", "weaponelements.437", "weaponelements.438",
        "weaponelements.439", "weaponelements.440", "weaponelements.441",
        "weaponelements.442", "weaponelements.443", "weaponelements.444"
    };

    // ---- Числа веток пистолета (ранг 0 — ветка не надета) ----
    public const int RevolverBarrel = 6;
    public static float RevolverReload(int rank) { return 0.9f; }
    public static float RevolverDamage(int rank) { return rank >= 2 ? 2.2f : 1.4f; }
    public static float RevolverInterval(int rank) { return rank >= 2 ? 1.4f : 1f; }
    public static float RevolverRange(int rank) { return rank >= 2 ? 1.5f : 1.1f; }
    public static float DualDamage(int rank) { return 1f; }
    public const float DualInterval = 1.5f;
    public static float DualRate(int rank) { return rank >= 2 ? 1.15f : 1f; }
    public static float StickyDelay { get { return 1.5f; } }
    public static float StickyShare { get { return 0.5f; } }
    public static float StickyRadius { get { return 2.2f; } }
    public static int StickyMaxCharges(int rank) { return rank >= 3 ? 3 : 1; }

    public static float RifleDamage(WeaponElement element, int rank)
    {
        if (element == WeaponElement.Assault) return rank >= 2 ? 1.45f : 1.3f;
        if (element == WeaponElement.Minigun) return rank >= 3 ? .95f : rank >= 2 ? .9f : .85f;
        if (element == WeaponElement.Heavy) return rank >= 3 ? 4.2f : rank >= 2 ? 3.8f : 3.2f;
        return 1f;
    }
    public static float MinigunFastInterval(int rank) { return rank >= 3 ? .58f : rank >= 2 ? .65f : .72f; }

    public static string Description(WeaponElement element, int rank)
    {
        rank = Mathf.Clamp(rank, 1, MaxRank);
        switch (element)
        {
            case WeaponElement.Fire: return GameText.Source("weaponelements.424") + rank * 8 + GameText.Source("weaponelements.425");
            case WeaponElement.Frost: return GameText.Source("weaponelements.426") + (10 + rank * 10) + GameText.Source("weaponelements.427");
            case WeaponElement.Lightning: return GameText.Source("weaponelements.428") + rank + GameText.Source("weaponelements.429");
            case WeaponElement.Poison: return GameText.Source("weaponelements.430") + rank * 5 + GameText.Source("weaponelements.431");
            case WeaponElement.Revolver: return GameText.Source(BranchText[rank - 1]);
            case WeaponElement.Dual: return GameText.Source(BranchText[3 + rank - 1]);
            case WeaponElement.Sticky: return GameText.Source(BranchText[6 + rank - 1]);
            case WeaponElement.Assault: return GameText.Source("rifle.assault.rank" + rank);
            case WeaponElement.Minigun: return GameText.Source("rifle.minigun.rank" + rank);
            case WeaponElement.Heavy: return GameText.Source("rifle.heavy.rank" + rank);
            default: return GameText.Source("weaponelements.432");
        }
    }
}

// Captured at weapon selection and shared by its shots. Old projectiles retain their original loadout.
public sealed class WeaponElementAttack
{
    public readonly WeaponElement Element;
    public readonly int Rank;
    private float nextChain;

    // Бонусы героя-владельца (крит, урон по элите); null — игрок 1.
    private readonly HeroStats stats;
    private HeroStats Stats => stats ?? CombatStats.Primary;

    public WeaponElementAttack(int weapon, HeroStats owner = null)
    {
        stats = owner;
        Element = WeaponElements.Equipped(weapon);
        Rank = WeaponElements.Rank(weapon, Element);
    }

    public void Hit(Health target, int damage, bool forceCrit = false)
    {
        if (target == null || target.isDead || damage <= 0 || !target.CompareTag("Enemy")) return;
        Vector3 position = target.transform.position;
        damage = Mathf.Max(1, Mathf.RoundToInt(damage * Stats.TargetMultiplier(target)));
        bool crit;
        damage = forceCrit ? Stats.Roll(damage, 1f, out crit) : Stats.Roll(damage, out crit);
        Health.NextHitCrit = crit;
        target.TakeDamage(damage);
        Health.NextHitCrit = false;
        if (Rank <= 0) return;
        if (Element == WeaponElement.Sticky)
        {
            if (!target.isDead) StickyCharge.Attach(target, damage, Rank, Stats);
            return;
        }
        if (WeaponElements.IsBranch(Element)) return;   // Револьвер и Двойные действуют в AutoShoter.Shoot
        if (Element == WeaponElement.Lightning)
        {
            if (Time.time < nextChain) return;
            nextChain = Time.time + .4f;
            Chain(target, position, damage);
            return;
        }
        if (target.isDead) return;
        if (Element == WeaponElement.Frost)
            SlowEffect.Apply(target, 1f - (.1f + .1f * Rank), 2f);
        else ElementStatus.Apply(target, Element, damage * Rank * (Element == WeaponElement.Fire ? .08f : .05f));
    }

    private void Chain(Health first, Vector3 from, int damage)
    {
        var candidates = new List<Health>();
        var hit = new HashSet<Health> { first };
        WeaponTargets.Collect(candidates);
        float strength = damage * .3f;
        for (int jump = 0; jump < Rank; jump++)
        {
            Health nearest = null;
            float distance = 5f;
            foreach (Health candidate in candidates)
            {
                if (candidate == null || candidate.isDead || hit.Contains(candidate)) continue;
                float gap = WeaponTargets.Flat(from, candidate.transform.position);
                if (gap < distance) { distance = gap; nearest = candidate; }
            }
            if (nearest == null) break;
            Vector3 to = nearest.transform.position;
            nearest.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(strength)));
            LightningBolt.Create(from + Vector3.up, to + Vector3.up, new Color(0.35f, 0.6f, 1f), .22f, .2f, 1);
            LightningBolt.Impact(to + Vector3.up, new Color(0.35f, 0.6f, 1f), .6f);
            hit.Add(nearest);
            from = to;
            strength *= .65f;
        }
    }
}
