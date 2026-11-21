using UnityEngine;

[CreateAssetMenu(menuName = "Arena/Progression Settings")]
public class ProgressionSettings : ScriptableObject
{
    [Header("Experience required to leave a level")]
    [Min(1)] public int firstLevelExperience = 12;
    [Min(0f)] public float experienceGrowth = 10f;
    [Min(1f)] public float experienceExponent = 1.45f;
    [Tooltip("Fixed costs of the first levels (1->2, 2->3, 3->4); later levels use the formula above.")]
    public int[] earlyLevelExperience = { 6, 10, 16 };
    [Header("Pickup (metres)")]
    [Min(0.1f)] public float attractionRadius = 4f;
    [Min(0.1f)] public float collectionRadius = 0.8f;
    [Min(0.1f)] public float attractionSpeed = 9f;
    [Header("Additive bonuses relative to starting stats")]
    [Range(0.01f, 1f)] public float damagePerUpgrade = 0.2f;
    [Range(0.01f, 1f)] public float attackSpeedPerUpgrade = 0.15f;
    [Range(0.01f, 1f)] public float movementPerUpgrade = 0.1f;
    [Range(0.01f, 1f)] public float pickupRadiusPerUpgrade = 0.25f;
    [Header("Multiplier limits for future difficulty balancing")]
    [Min(1f)] public float maxDamageMultiplier = 5f;
    [Min(1f)] public float maxAttackSpeedMultiplier = 3f;
    [Min(1f)] public float maxMovementMultiplier = 2f;
    [Min(1f)] public float maxPickupRadiusMultiplier = 3f;

    public int ExperienceForLevel(int level)
    {
        // Quick start (B07, 21.11.2026): the formula stays for level 4+, so the 10-minute level is unchanged in shape.
        if (earlyLevelExperience != null && level >= 1 && level <= earlyLevelExperience.Length && earlyLevelExperience[level - 1] > 0)
            return earlyLevelExperience[level - 1];
        double cost = System.Math.Max(1, firstLevelExperience) +
            System.Math.Max(0f, experienceGrowth) * System.Math.Pow(
                System.Math.Max(0, level - 1), System.Math.Max(1f, experienceExponent));
        return (int)System.Math.Min(int.MaxValue, System.Math.Ceiling(cost));
    }
}
