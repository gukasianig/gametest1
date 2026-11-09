using System.Collections.Generic;
using UnityEngine;

// The quarry prefab pairs Blender geometry with simple blockers for the existing arena pathfinder.
[RequireComponent(typeof(ArenaTerrain))]
public sealed class AcidQuarry : MonoBehaviour
{
    [SerializeField] private Bounds[] blocks = System.Array.Empty<Bounds>();
    [SerializeField] private Vector3 spillCenter;
    [SerializeField] private float spillRadius;

    private readonly List<Health> targets = new List<Health>();
    private PlayerHealth player;
    private float nextTick;

    public void RegisterObstacles(ArenaLayout arena)
    {
        foreach (Bounds block in blocks)
            arena.RegisterSolid(block);
    }

    private void OnEnable()
    {
        nextTick = Time.time + 0.5f;
    }

    private void Update()
    {
        if (spillRadius <= 0f || Time.deltaTime <= 0f || Time.time < nextTick || GameSession.Instance == null ||
            GameSession.Instance.State != GameRunState.Playing) return;

        nextTick = Time.time + 0.5f;
        if (player == null && PlayerProgression.Instance != null)
            player = PlayerProgression.Instance.GetComponent<PlayerHealth>();
        if (player != null && WeaponTargets.Flat(spillCenter, player.transform.position) <= spillRadius)
            player.TakeDamage(5);

        WeaponTargets.Collect(targets);
        foreach (Health target in targets)
            if (target != null && WeaponTargets.Flat(spillCenter, target.transform.position) <= spillRadius)
                target.TakeDamage(12);
    }
}
