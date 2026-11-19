using UnityEngine;

// Stable weapon indices share the existing meta v1 keyed storage, independent of branch/mastery ranks.
public static class WeaponTuning
{
    public const int MaxRank = 5;
    private static readonly int[] Costs = { 60, 90, 140, 210, 320 };
    public static bool Valid(int weapon) => weapon >= 0 && weapon <= 10 && weapon != 5;
    public static string Key(int weapon) => "weapon:tuning:" + weapon;
    public static int Rank(int weapon) => Valid(weapon) ? Mathf.Clamp(MetaProgress.Rank(Key(weapon)), 0, MaxRank) : 0;
    public static float Multiplier(int weapon) => 1f + .1f * Rank(weapon);
    public static int Cost(int rank) => Costs[Mathf.Clamp(rank, 0, MaxRank - 1)];
    public static bool CanBuy(int weapon) => Valid(weapon) && MetaProgress.IsWeaponUnlocked(weapon) &&
        Rank(weapon) < MaxRank && MetaProgress.Currency >= Cost(Rank(weapon));

    public static bool Buy(int weapon)
    {
        if (!CanBuy(weapon)) return false;
        int rank = Rank(weapon);
        MetaProgress.Data.currency -= Cost(rank);
        MetaEntry entry = MetaProgress.Data.ranks.Find(item => item.key == Key(weapon));
        if (entry == null) MetaProgress.Data.ranks.Add(new MetaEntry { key = Key(weapon), value = rank + 1 });
        else entry.value = rank + 1;
        MetaProgress.Save();
        return true;
    }
}
