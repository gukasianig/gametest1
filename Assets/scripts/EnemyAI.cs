using UnityEngine;
using System.Collections.Generic;

public class EnemyAI : MonoBehaviour
{
    private static readonly HashSet<EnemyAI> active = new HashSet<EnemyAI>();
    public static int ActiveCount => active.Count;

    public static int CountWithin(Vector3 center, float radius)
    {
        float limit = radius * radius;
        int count = 0;
        foreach (EnemyAI enemy in active)
        {
            Vector3 offset = enemy.transform.position - center;
            offset.y = 0f;
            if (offset.sqrMagnitude <= limit) count++;
        }
        return count;
    }
    public float speed = 3f;
    public float attackDistance = 1.5f;
    public int damage = 10;
    public float attackCooldown = 1f;
    public float stopDistance = 1.5f;
    public float separationRadius = 1.2f;
    public float separationStrength = 2f;
    public float attackRange = 1.5f;
    public Transform target;
    public bool UsesMelee { get; set; } = true;
    public bool MovementLocked { get; set; }
    public float PreferredRange { get; set; }
    private float lastAttackTime;
    private Vector3 waypoint;
    private float nextPathTime;
    private float bodyRadius = 0.5f;
    private Vector3 velocity;
    private float preferredSide = 1f;
    private static readonly float[] steeringAngles = { 0f, 25f, -25f, 50f, -50f, 75f, -75f, 100f, -100f };

    private void OnEnable() { active.Add(this); velocity = Vector3.zero; }
    private void OnDisable() { active.Remove(this); velocity = Vector3.zero; }

    private void Start()
    {
        Collider shape = GetComponent<Collider>();
        if (shape != null)
        {
            Vector3 extent = shape.bounds.extents;
            bodyRadius = shape is BoxCollider ? new Vector2(extent.x, extent.z).magnitude : Mathf.Max(extent.x, extent.z);
        }
        else bodyRadius = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) * 0.5f;
    }

    private void Update()
    {
        if (target == null || Time.timeScale <= 0f || MovementLocked) { velocity = Vector3.zero; return; }
        Move(Time.deltaTime);
    }

    private void Move(float deltaTime)
    {
        Vector3 position = transform.position;
        Vector3 toPlayer = target.position - position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        Vector3 direction = Vector3.zero;
        if (PreferredRange > 0f && distance < PreferredRange * 0.65f)
            direction = -toPlayer.normalized;
        else if (distance > Mathf.Max(stopDistance, PreferredRange))
        {
            if (ArenaLayout.Instance != null)
            {
                if (Time.time >= nextPathTime || (waypoint - position).sqrMagnitude < 0.3f)
                {
                    waypoint = ArenaLayout.Instance.NextWaypoint(position, target.position, bodyRadius);
                    nextPathTime = Time.time + 0.6f;
                }
                direction = waypoint - position;
                direction.y = 0f;
                direction.Normalize();
            }
            else direction = toPlayer.normalized;
        }

        Vector3 separation = Vector3.zero;
        foreach (EnemyAI other in active)
        {
            if (other == this) continue;
            Vector3 away = position - other.transform.position;
            away.y = 0f;
            float gap = away.magnitude;
            float personalSpace = Mathf.Max(separationRadius, bodyRadius + other.bodyRadius + 0.12f);
            if (gap >= personalSpace) continue;
            // Coincident spawn points also need a deterministic escape direction.
            if (gap < 0.001f) away = GetInstanceID() < other.GetInstanceID() ? Vector3.right : Vector3.left;
            else away /= gap;
            separation += away * (1f - gap / personalSpace);
        }
        // Repulsion must not reverse pursuit, as the previous inverse-distance sum did.
        Vector3 desired = Vector3.ClampMagnitude(direction + Vector3.ClampMagnitude(separation * separationStrength, 0.65f), 1f) * speed;
        Vector3 chosen = AvoidCrowd(position, desired, deltaTime);
        Vector3 next = position + chosen * deltaTime;
        if (ArenaLayout.Instance != null) next = ArenaLayout.Instance.ResolveMovement(position, next, bodyRadius);
        transform.position = next;
        velocity = deltaTime > 0f ? (next - position) / deltaTime : Vector3.zero;
        if (velocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(velocity), 10f * deltaTime);
        if (UsesMelee && distance <= attackRange) Attack();
    }

    private Vector3 AvoidCrowd(Vector3 position, Vector3 desired, float deltaTime)
    {
        Vector3 best = Vector3.zero;
        float bestScore = Score(position, Vector3.zero, deltaTime) - 0.2f;
        Vector3 forward = desired.normalized;
        foreach (float angle in steeringAngles)
        {
            Vector3 candidate = Quaternion.AngleAxis(angle, Vector3.up) * desired;
            Vector3 next = position + candidate * deltaTime;
            if (ArenaLayout.Instance != null && !ArenaLayout.Instance.PathClear(position, next, bodyRadius)) continue;
            float side = Mathf.Sign(angle);
            float score = Vector3.Dot(candidate / Mathf.Max(0.01f, speed), forward) * 2.5f + Score(position, candidate, deltaTime);
            if (angle != 0f && side != preferredSide) score -= 0.3f;
            if (score <= bestScore) continue;
            bestScore = score;
            best = candidate;
        }
        float turn = Vector3.Cross(forward, best).y;
        if (Mathf.Abs(turn) > 0.05f) preferredSide = Mathf.Sign(turn);
        return best;
    }

    private float Score(Vector3 position, Vector3 candidate, float deltaTime)
    {
        float penalty = 0f;
        foreach (EnemyAI other in active)
        {
            if (other == this) continue;
            Vector3 offset = other.transform.position - position;
            offset.y = 0f;
            float clearance = bodyRadius + other.bodyRadius + 0.12f;
            if (offset.sqrMagnitude > Mathf.Pow(clearance + (speed + other.speed) * 0.65f, 2f)) continue;
            Vector3 relative = candidate - other.velocity;
            float time = relative.sqrMagnitude > 0.001f ?
                Mathf.Clamp(Vector3.Dot(offset, relative) / relative.sqrMagnitude, 0f, 0.65f) : 0f;
            float predictedGap = (offset - relative * time).magnitude;
            float risk = Mathf.Clamp01(1f - predictedGap / clearance);
            penalty += risk * risk * 12f;
            // Never move deeper into an existing overlap; allow movement out of it.
            float nextGap = (offset - candidate * deltaTime).magnitude;
            if (nextGap < Mathf.Min(offset.magnitude, clearance - 0.1f) - 0.001f) penalty += 30f;
        }
        return -penalty;
    }

    private void Attack()
    {
        if (Time.time - lastAttackTime < attackCooldown) return;
        lastAttackTime = Time.time;
        PlayerHealth player = target.GetComponent<PlayerHealth>();
        if (player != null) player.TakeDamage(damage);
    }
}
