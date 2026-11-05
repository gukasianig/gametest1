using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]

public class WaveSettings 
{
    [Min(1)] public int baseBudget = 10;
    [Min(0)] public int budgetPerWave = 3;
    [Min(1)] public int maxBudget = 60;
    [Min(1)] public int maxEnemiesPerWave = 12;
    [Min(1)] public int initialAliveLimit = 6;
    [Min(0)] public int aliveLimitPerWave = 1;
    [Min(1)] public int maxAliveEnemies = 20;
    // Only enemies this close to the player count towards the alive limit, so kiting cannot freeze wave pressure.
    [Min(1f)] public float pressureRadius = 30f;
    // Hard cap on all live enemies (near or far) as a multiple of the current alive limit.
    [Min(1)] public int absoluteLimitMultiplier = 2;
    [Min(0.1f)] public float initialSpawnInterval = 0.75f;
    [Min(0.1f)] public float minimumSpawnInterval = 0.4f;
    [Min(0f)] public float intervalReductionPerWave = 0.015f;
    [Min(0f)] public float healthGrowthPerWave = 0.04f;

    public int BudgetForWave(int wave)
    {
        long budget = (long)Mathf.Max(1, baseBudget) + (long)Mathf.Max(0, wave - 1) * Mathf.Max(0, budgetPerWave);
        return (int)System.Math.Min(Mathf.Max(1, maxBudget), budget);
    }

    public int AliveLimitForWave(int wave)
    {
        long limit = (long)Mathf.Max(1, initialAliveLimit) + (long)Mathf.Max(0, wave - 1) * Mathf.Max(0, aliveLimitPerWave);
        return (int)System.Math.Min(Mathf.Max(1, maxAliveEnemies), limit);
    }

    public int AbsoluteLimitForWave(int wave)
    {
        return AliveLimitForWave(wave) * Mathf.Max(1, absoluteLimitMultiplier);
    }

    public float SpawnIntervalForWave(int wave)
    {
        return Mathf.Max(0.1f, minimumSpawnInterval,
            initialSpawnInterval - Mathf.Max(0, wave - 1) * Mathf.Max(0f, intervalReductionPerWave));
    }
    
}

