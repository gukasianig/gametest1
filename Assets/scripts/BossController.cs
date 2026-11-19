using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Финальные боссы (12.11.2026): в конце забега случайно выбирается один (GameSession.PrepareBoss).
public enum BossKind { Guardian, SwarmQueen, StormSniper }

public class BossController : MonoBehaviour
{
    public BossKind Kind { get; private set; }
    public string Title => Titles[(int)Kind];
    public static readonly string[] Titles = { GameText.Source("bosscontroller.069"), GameText.Source("bosscontroller.070"), GameText.Source("bosscontroller.071") };
    private static readonly string[] SkinKeys = { "Boss", "Swarm queen", "Storm sniper" };
    private static readonly int[] BaseHealth = { 5200, 4600, 4000 };
    // 12.11.2026 (просьба владельца): финальный босс сильнее в несколько раз.
    public const float HealthMultiplier = 4f, DamageMultiplier = 1.6f;
    public bool Enraged { get; private set; }
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

    public static BossController Create(Vector3 position, Transform player, BossKind kind = BossKind.Guardian)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        root.name = kind == BossKind.Guardian ? GameText.Source("bosscontroller.072") : kind == BossKind.SwarmQueen ? GameText.Source("bosscontroller.073") : GameText.Source("bosscontroller.074");
        root.tag = "Enemy";
        root.transform.position = position;
        root.transform.localScale = Vector3.one * 3.2f;
        ArenaLayout.Instance.Tint(root, new Color(0.8f, 0.34f, 0.12f));
        ModelSkins.ApplyEnemy(root, SkinKeys[(int)kind]);
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        Health health = root.AddComponent<Health>();
        health.SetHealth(Mathf.RoundToInt(BaseHealth[(int)kind] * HealthMultiplier * RunModifiers.BossHealth * GameSession.EndlessFactor));
        health.experienceReward = 0;
        BossController boss = root.AddComponent<BossController>();
        boss.Kind = kind;
        boss.slamDamage = Mathf.RoundToInt(boss.slamDamage * DamageMultiplier);
        boss.projectileDamage = Mathf.RoundToInt(boss.projectileDamage * DamageMultiplier);
        boss.dashDamage = Mathf.RoundToInt(boss.dashDamage * DamageMultiplier);
        boss.Health = health;
        boss.target = player;
        return boss;
    }

    private static BossKind lastKind = (BossKind)(-1);

    // Случайный финальный босс, не тот же, что в прошлом забеге (за одну игру).
    public static BossKind RandomKind()
    {
        int count = System.Enum.GetValues(typeof(BossKind)).Length;
        BossKind pick = (BossKind)Random.Range(0, count);
        if (pick == lastKind) pick = (BossKind)(((int)pick + 1 + Random.Range(0, count - 1)) % count);
        lastKind = pick;
        return pick;
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

    private float retargetTimer;

    private void Update()
    {
        // Кооператив: Страж выбирает ближайшего живого героя между атаками.
        if (CoopMode.Active && !attacking && (retargetTimer -= Time.deltaTime) <= 0f)
        {
            retargetTimer = 0.5f;
            Transform nearest = CoopMode.NearestAlive(transform.position);
            if (nearest != null) target = nearest;
        }
        if (!Running || attacking) return;
        Vector3 delta = target.position - transform.position;
        delta.y = 0f;
        if (Kind == BossKind.StormSniper) { KeepDistance(delta); return; }
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
            if (Kind == BossKind.SwarmQueen)
            {
                if (attack == 0) yield return QueenBrood();
                else if (attack == 1) yield return QueenAcid();
                else yield return Slam();
            }
            else if (Kind == BossKind.StormSniper)
            {
                if (attack == 1) yield return SniperBeam();
                else yield return SniperStrikes();
            }
            else if (attack == 0) yield return Slam();
            else if (attack == 1) yield return Fan();
            else yield return Dash();
            attacking = false;
            warning.enabled = false;
            Telegraphing = false;
            // Королева при половине HP злится: атакует чаще.
            if (Kind == BossKind.SwarmQueen && !Enraged && Health.CurrentHealth < Health.maxHealth / 2) Enrage();
            yield return new WaitForSeconds(attackDelay * RunModifiers.BossAttackDelay * (Enraged ? 0.6f : 1f));
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
        if (HorizontalDistance(target.position, centre) <= 4.9f) target.GetComponent<PlayerHealth>().TakeDamage(slamDamage, HeroDamageCause.BossSlam);
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
                Quaternion.Euler(0, i * 12f, 0) * direction, target, projectileDamage, cause: HeroDamageCause.BossProjectile);
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
                    target.GetComponent<PlayerHealth>().TakeDamage(dashDamage, HeroDamageCause.BossCharge);
                    hit = true;
                }
            }
            yield return null;
        }
    }

    // ---------------- Королева роя ----------------
    public const int QueenMinionLimit = 60;
    private readonly List<Health> queenMinions = new List<Health>();
    public int QueenMinionsAlive
    {
        get
        {
            queenMinions.RemoveAll(h => h == null || h.isDead);
            return queenMinions.Count;
        }
    }
    private static readonly Color Acid = new Color(0.55f, 1f, 0.3f);

    private void Enrage()
    {
        Enraged = true;
        EnemyFx.Ring(Ground(transform.position), 6f, new Color(0.8f, 0.3f, 1f), 0.6f, 0.5f);
        ScreenShake.AddAt(transform.position, 0.5f);
        GameAudio.Play(Sfx.Explosion, 0.7f);
        if (GameSession.Instance != null) GameSession.Instance.Announce(GameText.Source("bosscontroller.075"), 2.5f);
    }

    private static Vector3 Ground(Vector3 p) { return new Vector3(p.x, GroundHeight.At(p) + 0.08f, p.z); }

    // Рой: зелёные метки вокруг королевы, затем выбегают 18 мелких врагов (26 в ярости).
    private IEnumerator QueenBrood()
    {
        WaveManager waves = FindObjectOfType<WaveManager>();
        int count = Mathf.Min(Enraged ? 26 : 18, QueenMinionLimit - QueenMinionsAlive);
        if (waves == null || count <= 0) yield return Fan(); // роя достаточно — плевок
        else
        {
            var spots = new Vector3[count];
            var marks = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count + Random.Range(-0.2f, 0.2f);
                Vector3 spot = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(3.5f, 5f);
                if (ArenaLayout.Instance != null && !ArenaLayout.Instance.IsWalkable(spot, 0.5f)) spot = transform.position;
                spots[i] = Ground(spot);
                marks[i] = Ring(spots[i], 0.9f, Acid, 0.12f);
            }
            Telegraphing = true;
            yield return new WaitForSeconds(0.9f);
            while (!Running) yield return null;
            Telegraphing = false;
            for (int i = 0; i < count; i++)
            {
                Destroy(marks[i]);
                EnemyFx.Blobs(spots[i] + Vector3.up * 0.3f, Acid, 6, 3f, 0.12f, 0.4f, spots[i].y);
                if (QueenMinionsAlive >= QueenMinionLimit) break;
                GameObject minion = waves.SpawnMinionAt("Enemy_Fast", spots[i], 0.75f);
                if (minion != null && minion.TryGetComponent(out Health minionHealth)) queenMinions.Add(minionHealth);
            }
            GameAudio.Play(Sfx.Deploy, 0.8f);
        }
    }

    // Кислотный залп: веер из 7 (в ярости 9) кислотных сгустков.
    private IEnumerator QueenAcid()
    {
        if (target == null) yield break;
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        direction.Normalize();
        int side = Enraged ? 4 : 3;
        warning.enabled = true;
        warning.loop = false;
        warning.widthMultiplier = 0.16f;
        warning.positionCount = side * 4 + 3;
        Vector3 start = new Vector3(transform.position.x, 0.08f, transform.position.z);
        int n = 0;
        for (int i = -side; i <= side; i++)
        {
            warning.SetPosition(n++, start);
            warning.SetPosition(n++, start + Quaternion.Euler(0, i * 10f, 0) * direction * 18f);
        }
        warning.SetPosition(n, start);
        Telegraphing = true;
        yield return new WaitForSeconds(1f);
        while (!Running) yield return null;
        warning.enabled = false;
        Telegraphing = false;
        for (int i = -side; i <= side; i++)
            EnemyProjectile.Create(transform.position + direction * 2f, Quaternion.Euler(0, i * 10f, 0) * direction, target,
                projectileDamage, EnemyProjectile.Style.Acid, HeroDamageCause.BossProjectile);
        GameAudio.Play(Sfx.Throw, 0.8f);
    }

    // ---------------- Грозовой снайпер ----------------
    private float nextTeleport;
    private static readonly Color Storm = new Color(0.55f, 0.85f, 1f);

    // Держится в 12–18 м; если герой подошёл ближе 6 м — телепорт подальше (раз в 6 с).
    private void KeepDistance(Vector3 delta)
    {
        float distance = delta.magnitude;
        if (distance < 6f && Time.time >= nextTeleport) { StartCoroutine(Teleport()); return; }
        Vector3 goal;
        if (distance > 18f) goal = target.position;
        else if (distance < 12f) goal = transform.position - delta.normalized * 4f;
        else return;
        Vector3 desired = Vector3.MoveTowards(transform.position, new Vector3(goal.x, transform.position.y, goal.z), 2.6f * SlowEffect.MovementFactor(this) * Time.deltaTime);
        transform.position = ArenaLayout.Instance.ResolveMovement(transform.position, desired, 1.6f);
    }

    private IEnumerator Teleport()
    {
        nextTeleport = Time.time + 6f;
        attacking = true;
        Vector3 from = transform.position;
        LightningBolt.Impact(Ground(from) + Vector3.up, Storm, 1.4f);
        GameAudio.Play(Sfx.Arc, 0.8f);
        yield return new WaitForSeconds(0.35f);
        Vector3 away = (from - target.position);
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
        Vector3 spot = from;
        for (int i = 0; i < 12; i++)
        {
            Vector3 candidate = target.position + Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * away * Random.Range(13f, 16f);
            if (ArenaLayout.Instance == null || ArenaLayout.Instance.IsWalkable(candidate, 1.6f)) { spot = candidate; break; }
        }
        spot.y = from.y;
        transform.position = spot;
        LightningBolt.Create(spot + Vector3.up * 14f, Ground(spot) + Vector3.up * 0.5f, Storm, 0.6f, 0.3f, 3);
        LightningBolt.Impact(Ground(spot) + Vector3.up, Storm, 1.4f);
        ScreenShake.AddAt(spot, 0.25f);
        attacking = false;
    }

    // Удары молний: 5 кругов (один под героем), через 1,2 с — молния с неба и урон героям в круге.
    private IEnumerator SniperStrikes()
    {
        if (target == null) yield break;
        const float radius = 2.3f;
        int count = 5;
        var spots = new Vector3[count];
        var marks = new GameObject[count];
        for (int i = 0; i < count; i++)
        {
            Vector2 around = i == 0 ? Vector2.zero : Random.insideUnitCircle * 6f;
            spots[i] = Ground(target.position + new Vector3(around.x, 0f, around.y));
            marks[i] = Ring(spots[i], radius, Storm, 0.14f);
        }
        Telegraphing = true;
        yield return new WaitForSeconds(1.2f);
        while (!Running) yield return null;
        Telegraphing = false;
        for (int i = 0; i < count; i++)
        {
            Destroy(marks[i]);
            LightningBolt.Create(spots[i] + Vector3.up * 16f, spots[i] + Vector3.up * 0.2f, Storm, 0.7f, 0.3f, 3);
            LightningBolt.Impact(spots[i] + Vector3.up * 0.3f, Storm, 1.2f);
            CoopMode.DamageHeroesWithin(spots[i], radius, slamDamage, HeroDamageCause.Lightning);
        }
        GameAudio.Play(Sfx.Arc, 1f);
        ScreenShake.AddAt(target.position, 0.3f);
    }

    // Прицельный луч: 1 с следит за героем тонкой линией, 0,4 с замирает (толще), затем выстрел вдоль неё.
    private IEnumerator SniperBeam()
    {
        if (target == null) yield break;
        Vector3 direction = Vector3.forward;
        Telegraphing = true;
        float aim = 1.4f;
        while (aim > 0f)
        {
            if (Running)
            {
                aim -= Time.deltaTime;
                if (aim > 0.4f)
                {
                    direction = target.position - transform.position;
                    direction.y = 0f;
                    direction.Normalize();
                }
                Line(transform.position, transform.position + direction * 32f, aim > 0.4f ? 0.12f : 0.5f);
            }
            yield return null;
        }
        warning.enabled = false;
        Telegraphing = false;
        Vector3 start = transform.position + Vector3.up * 0.5f, end = start + direction * 32f;
        BeamFlash.Create(start, end, Storm, 0.9f, 0.25f);
        BeamFlash.Create(start, end, Color.white, 0.35f, 0.3f);
        GameAudio.Play(Sfx.Rail, 1f);
        foreach (PlayerHealth hero in CoopMode.Heroes)
            if (hero != null && !hero.IsDead && SpecialWeapons.SegmentDistance(start, end, hero.transform.position) < 1.4f)
                hero.TakeDamage(dashDamage + 10, HeroDamageCause.Beam);
    }

    private static readonly System.Collections.Generic.Dictionary<Color, Material> ringMaterials = new System.Collections.Generic.Dictionary<Color, Material>();

    private static Material RingMaterial(Color color)
    {
        if (!ringMaterials.TryGetValue(color, out Material material) || material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = color };
            ringMaterials[color] = material;
        }
        return material;
    }

    // Отдельное кольцо-предупреждение (их бывает несколько одновременно).
    private GameObject Ring(Vector3 centre, float radius, Color color, float width)
    {
        var go = new GameObject("Boss warning ring", typeof(LineRenderer));
        LineRenderer line = go.GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = 40;
        line.widthMultiplier = width;
        line.sharedMaterial = RingMaterial(color);
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        for (int i = 0; i < 40; i++)
        {
            float a = i * Mathf.PI * 2f / 40;
            Vector3 p = centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            p.y = GroundHeight.At(p) + 0.08f;
            line.SetPosition(i, p);
        }
        Destroy(go, 3f);
        return go;
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
