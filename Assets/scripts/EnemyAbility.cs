using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Values are stored in prefabs by number; 5 was a removed role, keep the explicit numbers.
public enum EnemyRole { Shooter, Charger, Bomber, Healer, Splitter, Summoner = 6, Sniper = 7 }

[RequireComponent(typeof(EnemyAI), typeof(Health))]
public class EnemyAbility : MonoBehaviour
{
    public EnemyRole role;
    // Splitter: the fragment prefab. Summoner: the minion prefab.
    public GameObject splitPrefab;
    public const int SummonerMinionLimit = 4;
    private readonly List<Health> minions = new List<Health>();
    public int LiveMinions { get { minions.RemoveAll(m => m == null || m.isDead); return minions.Count; } }
    private EnemyAI ai;
    private Health health;
    private LineRenderer warning;
    private Material warningMaterial;
    private bool acting;
    private float nextAction;
    public bool Telegraphing => warning != null && warning.enabled;
    public bool Ready => warning != null;
    public static Color RoleColor(EnemyRole value)
    {
        switch (value)
        {
            case EnemyRole.Shooter: return new Color(0.3f, 0.65f, 1f);
            case EnemyRole.Charger: return new Color(1f, 0.64f, 0.12f);
            case EnemyRole.Bomber: return new Color(1f, 0.2f, 0.3f);
            case EnemyRole.Healer: return new Color(0.2f, 0.95f, 0.55f);
            case EnemyRole.Summoner: return new Color(1f, 0.35f, 0.75f);
            case EnemyRole.Sniper: return new Color(0.95f, 0.95f, 0.3f);
            default: return new Color(0.75f, 0.4f, 1f);
        }
    }


    private void Awake()
    {
        ai = GetComponent<EnemyAI>();
        health = GetComponent<Health>();
        health.Died += OnDeath;
    }

    private void Start()
    {
        ai.UsesMelee = role == EnemyRole.Splitter;
        ai.PreferredRange = role == EnemyRole.Shooter ? 9f : role == EnemyRole.Healer ? 6f :
            role == EnemyRole.Summoner ? 8f : role == EnemyRole.Sniper ? 17f : 0f;
        nextAction = Time.time + 1.5f;
        Color color = RoleColor(role);
        if (ArenaLayout.Instance != null) ArenaLayout.Instance.Tint(gameObject, color);
        GameObject line = new GameObject("Attack telegraph", typeof(LineRenderer));
        line.transform.SetParent(transform, false);
        warning = line.GetComponent<LineRenderer>();
        warning.useWorldSpace = true;
        warning.widthMultiplier = 0.12f;
        warningMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        warningMaterial.color = color;
        warning.sharedMaterial = warningMaterial;
        warning.enabled = false;
        if (role == EnemyRole.Healer)
        {
            Part(PrimitiveType.Cube, new Vector3(0, 0.8f, 0), new Vector3(0.25f, 0.65f, 0.2f));
            Part(PrimitiveType.Cube, new Vector3(0, 0.8f, 0), new Vector3(0.65f, 0.25f, 0.2f));
        }
        else if (role == EnemyRole.Shooter)
            Part(PrimitiveType.Cylinder, new Vector3(0, 0.2f, 0.65f), new Vector3(0.22f, 0.5f, 0.22f)).localRotation = Quaternion.Euler(90, 0, 0);
        else if (role == EnemyRole.Charger)
        {
            Part(PrimitiveType.Cube, new Vector3(-0.35f, 0.4f, 0.3f), new Vector3(0.18f, 0.65f, 0.25f));
            Part(PrimitiveType.Cube, new Vector3(0.35f, 0.4f, 0.3f), new Vector3(0.18f, 0.65f, 0.25f));
        }
        else if (role == EnemyRole.Bomber)
            Part(PrimitiveType.Sphere, new Vector3(0, 0.65f, 0), Vector3.one * 0.5f);
        else if (role == EnemyRole.Summoner)
        {
            Part(PrimitiveType.Cube, new Vector3(-0.25f, 0.75f, 0), new Vector3(0.12f, 0.6f, 0.12f));
            Part(PrimitiveType.Cube, new Vector3(0.25f, 0.75f, 0), new Vector3(0.12f, 0.6f, 0.12f));
            Part(PrimitiveType.Sphere, new Vector3(0, 0.75f, 0), Vector3.one * 0.35f);
        }
        else if (role == EnemyRole.Sniper)
            Part(PrimitiveType.Cylinder, new Vector3(0, 0.25f, 1.0f), new Vector3(0.1f, 0.9f, 0.1f)).localRotation = Quaternion.Euler(90, 0, 0);
        else
        {
            Part(PrimitiveType.Sphere, new Vector3(-0.4f, 0.4f, 0), Vector3.one * 0.45f);
            Part(PrimitiveType.Sphere, new Vector3(0.4f, 0.4f, 0), Vector3.one * 0.45f);
        }
    }

    private Transform Part(PrimitiveType shape, Vector3 position, Vector3 size)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = role + " marker";
        part.transform.SetParent(transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        part.GetComponent<Collider>().enabled = false;
        Destroy(part.GetComponent<Collider>());
        if (ArenaLayout.Instance != null) ArenaLayout.Instance.Tint(part, Color.white);
        return part.transform;
    }

    private bool Running => GameSession.Instance != null && GameSession.Instance.State == GameRunState.Playing;
    private float DistanceToTarget => ai.target == null ? float.PositiveInfinity : FlatDistance(transform.position, ai.target.position);
    private void Update()
    {
        if (!Running || ai.target == null || acting || Time.time < nextAction || health.isDead) return;
        if (role == EnemyRole.Shooter && DistanceToTarget <= 15f) StartCoroutine(Shoot());
        else if (role == EnemyRole.Charger && DistanceToTarget <= 13f) StartCoroutine(Charge());
        else if (role == EnemyRole.Bomber && DistanceToTarget <= 3.5f) StartCoroutine(Explode());
        else if (role == EnemyRole.Healer) StartCoroutine(HealAllies());
        else if (role == EnemyRole.Summoner && DistanceToTarget <= 26f && LiveMinions < SummonerMinionLimit) StartCoroutine(Summon());
        else if (role == EnemyRole.Sniper && DistanceToTarget <= 24f &&
            ArenaLayout.Instance.PathClear(transform.position, ai.target.position, 0.2f)) StartCoroutine(Snipe());
    }

    private IEnumerator Wait(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (Running) elapsed += Time.deltaTime;
            yield return null;
        }
    }
    private void BeginAction() { acting = true; ai.MovementLocked = true; }
    private void EndAction(float cooldown)
    {
        warning.enabled = false;
        acting = false;
        ai.MovementLocked = false;
        nextAction = Time.time + cooldown;
    }
    private Vector3 Aim()
    {
        Vector3 direction = ai.target.position - transform.position;
        direction.y = 0f;
        return direction.normalized;
    }
    private void Line(Vector3 end)
    {
        warning.loop = false;
        warning.positionCount = 2;
        warning.SetPosition(0, new Vector3(transform.position.x, 0.08f, transform.position.z));
        warning.SetPosition(1, new Vector3(end.x, 0.08f, end.z));
        warning.enabled = true;
    }
    private void Circle(float radius)
    {
        warning.loop = true;
        warning.positionCount = 48;
        for (int i = 0; i < 48; i++)
        {
            float angle = i * Mathf.PI * 2 / 48;
            warning.SetPosition(i, new Vector3(transform.position.x + Mathf.Cos(angle) * radius, 0.08f, transform.position.z + Mathf.Sin(angle) * radius));
        }
        warning.enabled = true;
    }
    private IEnumerator Shoot()
    {
        BeginAction();
        Vector3 direction = Aim();
        Line(transform.position + direction * 15f);
        yield return Wait(0.65f);
        if (ai.target != null && ArenaLayout.Instance.PathClear(transform.position, transform.position + direction * 1.2f, 0.15f))
            EnemyProjectile.Create(transform.position + direction * 1.2f, direction, ai.target, 10);
        EndAction(2.4f);
    }
    private IEnumerator Charge()
    {
        BeginAction();
        Vector3 direction = Aim();
        Line(transform.position + direction * 8f);
        yield return Wait(0.9f);
        warning.enabled = false;
        float elapsed = 0f;
        bool hit = false;
        while (elapsed < 0.65f)
        {
            if (Running)
            {
                elapsed += Time.deltaTime;
                Vector3 before = transform.position;
                Vector3 after = ArenaLayout.Instance.ResolveMovement(before, before + direction * 12f * Time.deltaTime, 0.7f);
                transform.position = after;
                if (!hit && ai.target != null && SegmentDistance(before, after, ai.target.position) < 1.2f)
                {
                    ai.target.GetComponent<PlayerHealth>()?.TakeDamage(18);
                    hit = true;
                }
                if ((after - before).sqrMagnitude < 0.000001f) break;
            }
            yield return null;
        }
        EndAction(3.5f);
    }
    private IEnumerator Explode()
    {
        BeginAction();
        Circle(3.5f);
        yield return Wait(1.2f);
        if (DistanceToTarget <= 3.5f && ArenaLayout.Instance.PathClear(transform.position, ai.target.position, 0f))
            ai.target.GetComponent<PlayerHealth>()?.TakeDamage(25);
        health.TakeDamage(health.CurrentHealth);
    }
    private IEnumerator HealAllies()
    {
        BeginAction();
        Circle(7f);
        yield return Wait(0.7f);
        foreach (EnemyAI ally in FindObjectsOfType<EnemyAI>())
            if (ally != ai && FlatDistance(transform.position, ally.transform.position) <= 7f &&
                ArenaLayout.Instance.PathClear(transform.position, ally.transform.position, 0f))
                ally.GetComponent<Health>()?.Heal(12);
        EndAction(4f);
    }

    // Long thin warning, a pause, then one fast and heavy shot along the locked direction (sidestep to dodge).
    private IEnumerator Snipe()
    {
        BeginAction();
        Vector3 direction = Aim();
        // The barrel marker points along transform.forward: turn the sniper to the locked shot direction.
        if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        Line(transform.position + direction * 28f);
        warning.widthMultiplier = 0.07f;
        yield return Wait(1.2f);
        warning.widthMultiplier = 0.12f;
        if (ai.target != null && ArenaLayout.Instance.PathClear(transform.position, transform.position + direction * 1.2f, 0.15f))
        {
            EnemyProjectile shot = EnemyProjectile.Create(transform.position + direction * 1.2f, direction, ai.target, 22);
            shot.speed = 24f;
        }
        EndAction(4.5f);
    }

    // Circle warning, then up to two weak minions next to the summoner; at most four alive per summoner.
    private IEnumerator Summon()
    {
        BeginAction();
        Circle(2.5f);
        yield return Wait(0.8f);
        WaveManager waves = FindObjectOfType<WaveManager>();
        if (splitPrefab != null && waves != null && !waves.Suspended)
        {
            int cap = waves.settings.AbsoluteLimitForWave(waves.wave);
            for (int i = 0; i < 2 && LiveMinions < SummonerMinionLimit && EnemyAI.ActiveCount < cap; i++)
            {
                Vector3 point = transform.position + Quaternion.Euler(0, 90f + i * 180f, 0) * transform.forward * 1.8f;
                if (!ArenaLayout.Instance.IsWalkable(point, 0.4f)) continue;
                Health minion = SpawnMinion(splitPrefab, point, waves.groundHeight);
                if (minion != null) minions.Add(minion);
            }
        }
        EndAction(7f);
    }

    private Health SpawnMinion(GameObject prefab, Vector3 point, float groundHeight)
    {
        GameObject child = Instantiate(prefab, point, Quaternion.identity);
        child.name = "Summoned minion";
        child.transform.localScale *= 0.55f;
        float bottom = float.PositiveInfinity;
        foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>()) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
        child.transform.position += Vector3.up * (groundHeight - bottom);
        EnemyAI childAI = child.GetComponent<EnemyAI>();
        childAI.target = ai.target;
        childAI.speed = 4.2f;
        childAI.damage = 5;
        Health childHealth = child.GetComponent<Health>();
        childHealth.SetHealth(15);
        childHealth.experienceReward = 0;
        ArenaLayout.Instance.Tint(child, RoleColor(role));
        return childHealth;
    }

    private void OnDeath(Health dead)
    {
        if (role != EnemyRole.Splitter || splitPrefab == null || !Running) return;
        WaveManager waves = FindObjectOfType<WaveManager>();
        if (waves == null || waves.Suspended) return;
        int limit = waves.settings.AliveLimitForWave(waves.wave);
        for (int i = 0; i < 2 && EnemyAI.ActiveCount < limit; i++)
        {
            Vector3 point = transform.position + (i == 0 ? Vector3.left : Vector3.right) * 1.3f;
            if (!ArenaLayout.Instance.IsWalkable(point, 0.4f)) continue;
            GameObject child = Instantiate(splitPrefab, point, Quaternion.identity);
            child.name = "Splitter fragment";
            child.transform.localScale *= 0.55f;
            float bottom = float.PositiveInfinity;
            foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>()) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
            child.transform.position += Vector3.up * (waves.groundHeight - bottom);
            EnemyAI childAI = child.GetComponent<EnemyAI>();
            childAI.target = ai.target;
            childAI.speed = 4f;
            childAI.damage = 5;
            Health childHealth = child.GetComponent<Health>();
            childHealth.SetHealth(20);
            childHealth.experienceReward = 0;
            ArenaLayout.Instance.Tint(child, RoleColor(role));
        }
    }
    private static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }
    private static float SegmentDistance(Vector3 a, Vector3 b, Vector3 point)
    {
        a.y = b.y = point.y = 0f;
        Vector3 delta = b - a;
        return Vector3.Distance(point, a + delta * Mathf.Clamp01(Vector3.Dot(point - a, delta) / Mathf.Max(0.00001f, delta.sqrMagnitude)));
    }
    private void OnDisable()
    {
        StopAllCoroutines();
        acting = false;
        if (ai != null) ai.MovementLocked = false;
        if (warning != null) warning.enabled = false;
    }
    private void OnDestroy()
    {
        if (health != null) health.Died -= OnDeath;
        if (warningMaterial != null) Destroy(warningMaterial);
    }
}
