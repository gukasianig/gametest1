using System.Collections.Generic;
using UnityEngine;

public enum WeaponElement { None, Fire, Frost, Lightning, Poison }

// All keys include the weapon index, matching the existing weapon:N unlock keys.
public static class WeaponElements
{
    public const int MaxRank = 3;
    public static readonly string[] Names = { "Без стихии", "Огонь", "Холод", "Молния", "Яд" };
    public static readonly string[] Icons = { "", "el_fire", "el_frost", "el_lightning", "el_poison" };
    public static readonly Color[] Colors = { Color.white, new Color(1f, .42f, .2f),
        new Color(.35f, .8f, 1f), new Color(1f, .84f, .3f), new Color(.55f, .95f, .24f) };

    public static bool Valid(int weapon, WeaponElement element)
    {
        return weapon >= 0 && weapon < 13 && element >= WeaponElement.Fire && element <= WeaponElement.Poison;
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
    public static readonly bool Locked = true;

    public static bool CanBuy(int weapon, WeaponElement element)
    {
        return !Locked && Valid(weapon, element) && MetaProgress.IsWeaponUnlocked(weapon) &&
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

    public static string Description(WeaponElement element, int rank)
    {
        rank = Mathf.Clamp(rank, 1, MaxRank);
        switch (element)
        {
            case WeaponElement.Fire: return "Горение: " + rank * 8 + "% урона попадания в секунду, 3 с. Обновляет длительность. На Стража вдвое слабее.";
            case WeaponElement.Frost: return "Замедление на " + (10 + rank * 10) + "% на 2 с. На Стража действует вдвое слабее.";
            case WeaponElement.Lightning: return "Разряд: до " + rank + " соседей в 5 м, 30% урона с затуханием. Не чаще раза в 0,4 с.";
            case WeaponElement.Poison: return "Яд: " + rank * 5 + "% урона попадания в секунду, 4 с, до 3 стаков. На Стража вдвое слабее.";
            default: return "Одна активная стихия для каждого оружия. Купленные варианты можно переключать бесплатно.";
        }
    }
}

// Captured at weapon selection and shared by its shots. Old projectiles retain their original loadout.
public sealed class WeaponElementAttack
{
    public readonly WeaponElement Element;
    public readonly int Rank;
    private float nextChain;

    public WeaponElementAttack(int weapon)
    {
        Element = WeaponElements.Equipped(weapon);
        Rank = WeaponElements.Rank(weapon, Element);
    }

    public void Hit(Health target, int damage)
    {
        if (target == null || target.isDead || damage <= 0 || !target.CompareTag("Enemy")) return;
        Vector3 position = target.transform.position;
        damage = Mathf.Max(1, Mathf.RoundToInt(damage * CombatStats.TargetMultiplier(target)));
        damage = CombatStats.Roll(damage, out bool crit);
        Health.NextHitCrit = crit;
        target.TakeDamage(damage);
        Health.NextHitCrit = false;
        if (Rank <= 0) return;
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
