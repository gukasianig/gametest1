using UnityEngine;

[CreateAssetMenu(menuName = "Arena/Progression Settings")]
public class ProgressionSettings : ScriptableObject
{
    [Header("Experience required to leave a level")]
    [Min(1)] public int firstLevelExperience = 10;
    [Min(0f)] public float experienceGrowth = 8f;
    [Min(1f)] public float experienceExponent = 1.3f;
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
        double cost = System.Math.Max(1, firstLevelExperience) +
            System.Math.Max(0f, experienceGrowth) * System.Math.Pow(
                System.Math.Max(0, level - 1), System.Math.Max(1f, experienceExponent));
        return (int)System.Math.Min(int.MaxValue, System.Math.Ceiling(cost));
    }
}
