using System.Collections.Generic;
using UnityEngine;

// Enemy queries and target selection shared by the special weapons.
public static class WeaponTargets
{
    // Every living enemy (regular enemies and the boss) into the given list.
    public static void Collect(List<Health> into)
    {
        into.Clear();
        foreach (EnemyAI enemy in EnemyAI.All)
        {
            Health health = enemy.Health;
            if (health != null && !health.isDead) into.Add(health);
        }
        GameSession session = GameSession.Instance;
        if (session != null && session.Boss != null && session.Boss.Health != null && !session.Boss.Health.isDead)
            into.Add(session.Boss.Health);
    }

    public static float Flat(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // Rough horizontal radius of an enemy body, so beams and blasts do not miss large targets.
    public static float BodyRadius(Health health)
    {
        Collider collider = health.GetComponent<Collider>();
        if (collider == null) return 0.6f;
        Vector3 extents = collider.bounds.extents;
        return Mathf.Max(extents.x, extents.z);
    }

    private static readonly List<Health> buffer = new List<Health>();

    // The enemy the weapon aims at, chosen among those within range of the origin.
    public static Transform Pick(Vector3 origin, float range, TargetRule rule)
    {
        Collect(buffer);
        Health best = null;
        float bestScore = float.NegativeInfinity;
        foreach (Health health in buffer)
        {
            float distance = Flat(origin, health.transform.position);
            if (distance > range) continue;
            float score;
            switch (rule)
            {
                case TargetRule.Strongest: score = health.CurrentHealth - distance * 0.001f; break;
                case TargetRule.Weakest: score = -health.CurrentHealth - distance * 0.001f; break;
                case TargetRule.Densest:
                    int neighbours = 0;
                    foreach (Health other in buffer)
                        if (other != health && Flat(health.transform.position, other.transform.position) <= 4f) neighbours++;
                    score = neighbours - distance * 0.001f;
                    break;
                default: score = -distance; break;
            }
            if (score > bestScore) { bestScore = score; best = health; }
        }
        return best != null ? best.transform : null;
    }
}
