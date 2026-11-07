using System.Collections;
using UnityEngine;

public class BossController : MonoBehaviour
{
    public Health Health { get; private set; }
    public Transform target;
    public int slamDamage = 18, projectileDamage = 12, dashDamage = 20;
    public float attackDelay = 1.5f;
    public int AttackIndex { get; private set; }
    public bool Telegraphing { get; private set; }
    private LineRenderer warning;
    private Vector3 waypoint;
    private float nextPath;
    private bool attacking;
    private Material warningMaterial;

    public static BossController Create(Vector3 position, Transform player)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        root.name = "Страж арены";
        root.tag = "Enemy";
        root.transform.position = position;
        root.transform.localScale = Vector3.one * 3.2f;
        ArenaLayout.Instance.Tint(root, new Color(0.8f, 0.34f, 0.12f));
        ModelSkins.ApplyEnemy(root, "Boss");
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        Health health = root.AddComponent<Health>();
        health.SetHealth(2600);
        health.experienceReward = 0;
        BossController boss = root.AddComponent<BossController>();
        boss.Health = health;
        boss.target = player;
        return boss;
    }

    private void Start()
    {
        Health = GetComponent<Health>();
        GameObject line = new GameObject("Attack warning", typeof(LineRenderer));
        line.transform.SetParent(transform, false);
        warning = line.GetComponent<LineRenderer>();
        warning.useWorldSpace = true;
        warning.widthMultiplier = 0.16f;
        warningMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        warningMaterial.color = new Color(1f, 0.4f, 0.1f);
        warning.sharedMaterial = warningMaterial;
        warning.enabled = false;
        StartCoroutine(AttackLoop());
    }

    private bool Running => target != null && Health != null && !Health.isDead &&
        GameSession.Instance != null && GameSession.Instance.State == GameRunState.Playing;

    private void Update()
    {
        if (!Running || attacking) return;
        Vector3 delta = target.position - transform.position;
        delta.y = 0f;
        if (delta.magnitude < 9f) return;
        if (Time.time >= nextPath || (waypoint - transform.position).sqrMagnitude < 0.5f)
        {
            waypoint = ArenaLayout.Instance.NextWaypoint(transform.position, target.position, 1.6f);
            nextPath = Time.time + 0.6f;
        }
        Vector3 desired = Vector3.MoveTowards(transform.position, new Vector3(waypoint.x, 1.6f, waypoint.z), 2.2f * SlowEffect.MovementFactor(this) * Time.deltaTime);
        transform.position = ArenaLayout.Instance.ResolveMovement(transform.position, desired, 1.6f);
    }

    private IEnumerator AttackLoop()
    {
        yield return new WaitForSeconds(3f);
        while (Health != null && !Health.isDead)
        {
            while (!Running) yield return null;
            attacking = true;
            int attack = AttackIndex++ % 3;
            if (attack == 0) yield return Slam();
            else if (attack == 1) yield return Fan();
            else yield return Dash();
            attacking = false;
            warning.enabled = false;
            Telegraphing = false;
            yield return new WaitForSeconds(attackDelay);
        }
    }

    private void Circle(Vector3 centre, float radius)
    {
        warning.enabled = true;
        warning.loop = true;
        warning.positionCount = 64;
        for (int i = 0; i < 64; i++)
        {
            float angle = i * Mathf.PI * 2f / 64;
            warning.SetPosition(i, new Vector3(centre.x + Mathf.Cos(angle) * radius, 0.08f, centre.z + Mathf.Sin(angle) * radius));
        }
        Telegraphing = true;
    }

    private void Line(Vector3 start, Vector3 end, float width)
    {
        warning.enabled = true;
        warning.loop = false;
        warning.positionCount = 2;
        start.y = end.y = 0.08f;
        warning.SetPosition(0, start);
        warning.SetPosition(1, end);
        warning.widthMultiplier = width;
        Telegraphing = true;
    }

    private IEnumerator Slam()
    {
        if (target == null) yield break;
        Vector3 centre = target.position;
        warning.widthMultiplier = 0.16f;
        Circle(centre, 4.5f);
        yield return new WaitForSeconds(1.3f);
        while (!Running) yield return null;
        Telegraphing = false;
        if (HorizontalDistance(target.position, centre) <= 4.9f) target.GetComponent<PlayerHealth>().TakeDamage(slamDamage);
        warning.widthMultiplier = 0.65f;
        yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator Fan()
    {
        if (target == null) yield break;
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        direction.Normalize();
        warning.enabled = true;
        warning.loop = false;
        warning.widthMultiplier = 0.16f;
        warning.positionCount = 15;
        for (int i = 0; i < 7; i++)
        {
            Vector3 start = new Vector3(transform.position.x, 0.08f, transform.position.z);
            warning.SetPosition(i * 2, start);
            warning.SetPosition(i * 2 + 1, start + Quaternion.Euler(0, (i - 3) * 12f, 0) * direction * 20f);
        }
        warning.SetPosition(14, new Vector3(transform.position.x, 0.08f, transform.position.z));
        Telegraphing = true;
        yield return new WaitForSeconds(1.1f);
        while (!Running) yield return null;
        warning.enabled = false;
        Telegraphing = false;
        for (int i = -3; i <= 3; i++)
            EnemyProjectile.Create(transform.position + direction * 2f,
                Quaternion.Euler(0, i * 12f, 0) * direction, target, projectileDamage);
    }

    private IEnumerator Dash()
    {
        if (target == null) yield break;
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        direction.Normalize();
        Vector3 end = transform.position + direction * 16f;
        Line(transform.position, end, 3.2f);
        yield return new WaitForSeconds(1.2f);
        while (!Running) yield return null;
        warning.enabled = false;
        Telegraphing = false;
        bool hit = false;
        float elapsed = 0f;
        while (elapsed < 1.1f)
        {
            if (Running)
            {
                elapsed += Time.deltaTime;
                Vector3 desired = transform.position + direction * 14f * Time.deltaTime;
                Vector3 allowed = ArenaLayout.Instance.ResolveMovement(transform.position, desired, 1.6f);
                if ((allowed - transform.position).sqrMagnitude < 0.00001f) break;
                transform.position = allowed;
                if (!hit && HorizontalDistance(target.position, transform.position) < 2.2f)
                {
                    target.GetComponent<PlayerHealth>().TakeDamage(dashDamage);
                    hit = true;
                }
            }
            yield return null;
        }
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void OnDestroy()
    {
        if (warningMaterial != null) Destroy(warningMaterial);
    }
}
