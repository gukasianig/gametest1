using System;
using System.Collections.Generic;
using UnityEngine;

public enum UpgradeEffect { Damage, AttackSpeed, Movement, Pickup, GrenadeDamage, OrbitDamage, GrenadeRadius, MaxHealth, OrbitCount }
public enum UpgradeRarity { Common, Rare, Special }

[Serializable]
public class UpgradeDefinition
{
    public string id;
    public string title;
    public Sprite icon;
    public string symbol;
    public UpgradeEffect effect;
    public UpgradeRarity rarity;
    [Min(0.01f)] public float amount = 0.2f;
    [Min(1)] public int maxRanks = 10;
    [Min(1)] public int minimumLevel = 1;
    [Min(1)] public int weight = 10;
}

[CreateAssetMenu(menuName = "Arena/Upgrade Catalog")]
public class UpgradeCatalog : ScriptableObject
{
    public List<UpgradeDefinition> entries = new List<UpgradeDefinition>();
    [Min(1)] public int commonWeight = 70;
    [Min(1)] public int rareWeight = 25;
    [Min(1)] public int specialWeight = 5;

    public int Weight(UpgradeDefinition definition)
    {
        int rarity = definition.rarity == UpgradeRarity.Common ? commonWeight :
            definition.rarity == UpgradeRarity.Rare ? rareWeight : specialWeight;
        return Mathf.Max(1, rarity) * Mathf.Max(1, definition.weight);
    }

    public static string RarityName(UpgradeRarity rarity)
    {
        return rarity == UpgradeRarity.Common ? "Обычное" : rarity == UpgradeRarity.Rare ? "Редкое" : "Особое";
    }

    public static Color RarityColor(UpgradeRarity rarity)
    {
        return rarity == UpgradeRarity.Common ? new Color(0.35f, 0.8f, 0.65f) :
            rarity == UpgradeRarity.Rare ? new Color(0.35f, 0.65f, 1f) : new Color(0.85f, 0.45f, 1f);
    }
}
