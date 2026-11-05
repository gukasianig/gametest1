using System.Collections.Generic;
using UnityEngine;

// Behaviour of the non-bullet weapons (WeaponKind other than Standard). AutoShoter picks the target by the
// weapon's TargetRule and calls Fire() every fireRate seconds; everything here is built from primitives at
// runtime, and all numbers come from the WeaponData entry so they can be tuned in the Inspector.
public class SpecialWeapons : MonoBehaviour
{
    private AutoShoter shooter;
    private WeaponData weapon;
    private readonly List<GameObject> placed = new List<GameObject>();
    private GameObject flame;
    private float flameUntil;
    private static readonly List<Health> scratch = new List<Health>();
    private static readonly List<Health> blastBuffer = new List<Health>();

    public WeaponData Weapon { get { return weapon; } }
    public AutoShoter Shooter { get { return shooter; } }

    public static bool Running
    {
        get
        {
            return Time.timeScale > 0f && (GameSession.Instance == null || GameSession.Instance.State == GameRunState.Playing);
        }
    }

    // Called when a weapon is selected: removes everything the previous weapon left and sets up drones.
    public void Configure(AutoShoter owner, WeaponData data)
    {
        shooter = owner;
        weapon = data;
        Clear();
        if (data.kind == WeaponKind.Drone)
            for (int i = 0; i < Mathf.Max(1, data.maxActive); i++) WeaponDrone.Create(this, i);
    }

    public void Clear()
    {
        foreach (GameObject item in placed) if (item != null) Destroy(item);
        placed.Clear();
        if (flame != null) Destroy(flame);
        flame = null;
    }

    public void Register(GameObject item) { placed.Add(item); }

    public int CountAlive<T>() where T : Component
    {
        placed.RemoveAll(item => item == null);
        int count = 0;
        foreach (GameObject item in placed) if (item.GetComponent<T>() != null) count++;
        return count;
    }

    private void OnDestroy() { Clear(); }

    private void Update()
    {
        if (flame != null && flame.activeSelf && Time.time > flameUntil) flame.SetActive(false);
    }

    // Beams, projectiles and blasts start at the hero's centre (like target selection), not at the muzzle,
    // so lines to far targets are not skewed by the muzzle offset.
    private Vector3 Origin()
    {
        Vector3 point = transform.position;
        point.y = 0.9f;
        return point;
    }

    private static Vector3 FlatDirection(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        direction.y = 0f;
        return direction.sqrMagnitude < 0.0001f ? Vector3.forward : direction.normalized;
    }

    public static void Damage(Health health, int amount)
    {
        if (health != null && !health.isDead && amount > 0) health.TakeDamage(amount);
    }

    public static float SegmentDistance(Vector3 a, Vector3 b, Vector3 point)
    {
        a.y = b.y = point.y = 0f;
        Vector3 delta = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(point - a, delta) / Mathf.Max(0.00001f, delta.sqrMagnitude));
        return Vector3.Distance(point, a + delta * t);
    }

    public static GameObject Prim(PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color)
    {
        GameObject item = GameObject.CreatePrimitive(type);
        item.name = name;
        item.transform.position = position;
        item.transform.localScale = scale;
        Collider collider = item.GetComponent<Collider>();
        if (collider != null) { collider.enabled = false; Destroy(collider); }
        if (ArenaLayout.Instance != null) ArenaLayout.Instance.Tint(item, color);
        return item;
    }

    // Damage everything within the radius and show a short expanding sphere.
    public static void Blast(Vector3 center, float radius, int damage, Color color)
    {
        WeaponTargets.Collect(blastBuffer);
        foreach (Health health in blastBuffer)
            if (WeaponTargets.Flat(center, health.transform.position) <= radius + WeaponTargets.BodyRadius(health) * 0.5f)
                Damage(health, damage);
        ExpandingSphere.Create(center, radius, color, 0.3f);
    }

    public void Fire(int damage, Transform target)
    {
        if (weapon == null || shooter == null || target == null || !Running) return;
        Vector3 origin = Origin();
        switch (weapon.kind)
        {
            case WeaponKind.Rail: FireRail(origin, damage, target); break;
            case WeaponKind.Boomerang:
                if (CountAlive<WeaponSaw>() < Mathf.Max(1, weapon.maxActive))
                    WeaponSaw.Create(this, origin, FlatDirection(origin, target.position), damage,
                        Mathf.Min(weapon.range, WeaponTargets.Flat(origin, target.position) + 3f));
                break;
            case WeaponKind.Mines:
                if (CountAlive<WeaponMine>() < Mathf.Max(1, weapon.maxActive)) WeaponMine.Create(this, transform.position, damage);
                break;
            case WeaponKind.Rocket:
                WeaponProjectile.CreateRocket(this, origin, FlatDirection(origin, target.position), damage);
                break;
            case WeaponKind.Ice:
                WeaponProjectile.CreateIce(this, origin, FlatDirection(origin, target.position), damage);
                break;
            case WeaponKind.Arc: FireArc(origin, damage, target); break;
            case WeaponKind.Flame: FireFlame(origin, damage, target); break;
            case WeaponKind.Turret:
                if (CountAlive<WeaponTurret>() < Mathf.Max(1, weapon.maxActive))
                {
                    Vector3 spot = transform.position + FlatDirection(transform.position, target.position) * 1.5f;
                    if (ArenaLayout.Instance != null && !ArenaLayout.Instance.IsWalkable(spot, 0.5f)) spot = transform.position;
                    WeaponTurret.Create(this, spot);
                }
                break;
            default: break; // drones act on their own
        }
    }

    private void FireRail(Vector3 origin, int damage, Transform target)
    {
        Vector3 direction = FlatDirection(origin, target.position);
        float length = Mathf.Max(2f, weapon.range);
        float clipped = length;
        if (ArenaLayout.Instance != null)
            for (float d = 1f; d <= length; d += 1f)
                if (!ArenaLayout.Instance.PathClear(origin, origin + direction * d, 0.05f)) { clipped = Mathf.Max(0.5f, d - 1f); break; }
        Vector3 end = origin + direction * clipped;
        WeaponTargets.Collect(scratch);
        foreach (Health health in scratch)
            if (SegmentDistance(origin, end, health.transform.position) <= weapon.beamWidth + WeaponTargets.BodyRadius(health))
                Damage(health, damage);
        BeamFlash.Create(origin, end, new Color(0.55f, 0.9f, 1f), 0.35f, 0.15f);
    }

    private void FireArc(Vector3 origin, int damage, Transform target)
    {
        var hit = new List<Health>();
        Health current = target.GetComponent<Health>();
        Vector3 from = origin;
        float strength = damage;
        for (int jump = 0; jump <= weapon.chainJumps && current != null; jump++)
        {
            Vector3 point = current.transform.position + Vector3.up * 0.2f;
            point.y = 0.9f;
            Damage(current, Mathf.Max(1, Mathf.RoundToInt(strength)));
            hit.Add(current);
            BeamFlash.Create(from, point, new Color(0.65f, 0.8f, 1f), 0.14f, 0.2f);
            from = point;
            strength *= weapon.chainFalloff;
            current = NearestUnhit(current.transform.position, hit, weapon.chainRadius);
        }
    }

    private static Health NearestUnhit(Vector3 from, List<Health> hit, float radius)
    {
        WeaponTargets.Collect(scratch);
        Health best = null;
        float bestDistance = radius;
        foreach (Health health in scratch)
        {
            if (hit.Contains(health)) continue;
            float distance = WeaponTargets.Flat(from, health.transform.position);
            if (distance <= bestDistance) { bestDistance = distance; best = health; }
        }
        return best;
    }

    private void FireFlame(Vector3 origin, int damage, Transform target)
    {
        Vector3 direction = FlatDirection(origin, target.position);
        float half = weapon.coneAngle * 0.5f;
        WeaponTargets.Collect(scratch);
        foreach (Health health in scratch)
        {
            Vector3 offset = health.transform.position - origin;
            offset.y = 0f;
            float distance = offset.magnitude;
            float radius = WeaponTargets.BodyRadius(health);
            if (distance > weapon.range + radius) continue;
            // Wide bodies count when their edge is inside the cone.
            float slack = distance > 0.01f ? Mathf.Atan2(radius, distance) * Mathf.Rad2Deg : 180f;
            if (distance > 0.8f && Vector3.Angle(direction, offset) > half + slack) continue;
            Damage(health, damage);
            BurnEffect.Apply(health, weapon.burnPerSecond, weapon.effectDuration);
        }
        if (flame == null) flame = Prim(PrimitiveType.Sphere, "Flame", origin, Vector3.one, new Color(1f, 0.5f, 0.1f));
        float width = Mathf.Clamp(Mathf.Tan(half * Mathf.Deg2Rad) * weapon.range * 0.9f, 0.6f, 5f);
        flame.transform.position = origin + direction * weapon.range * 0.5f;
        flame.transform.rotation = Quaternion.LookRotation(direction);
        flame.transform.localScale = new Vector3(width, 0.5f, weapon.range);
        flame.SetActive(true);
        flameUntil = Time.time + 0.2f;
    }
}

// Short-lived straight line (rail beam, chain lightning, turret and drone tracers).
public class BeamFlash : MonoBehaviour
{
    private static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();

    public static void Create(Vector3 from, Vector3 to, Color color, float width, float seconds)
    {
        GameObject item = new GameObject("Beam", typeof(LineRenderer));
        LineRenderer line = item.GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.widthMultiplier = width;
        if (!materials.TryGetValue(color, out Material material) || material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.color = color;
            materials[color] = material;
        }
        line.sharedMaterial = material;
        Destroy(item, seconds);
    }
}

public class ExpandingSphere : MonoBehaviour
{
    private float age, duration, radius;

    public static void Create(Vector3 center, float radius, Color color, float seconds)
    {
        GameObject item = SpecialWeapons.Prim(PrimitiveType.Sphere, "Blast", new Vector3(center.x, 0.8f, center.z), Vector3.one * 0.2f, color);
        ExpandingSphere sphere = item.AddComponent<ExpandingSphere>();
        sphere.duration = Mathf.Max(0.05f, seconds);
        sphere.radius = radius;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        transform.localScale = Vector3.one * Mathf.Lerp(0.2f, radius * 2f, t);
        if (age >= duration) Destroy(gameObject);
    }
}

public class BurnEffect : MonoBehaviour
{
    private Health health;
    private float damagePerSecond, until, tick;

    public static void Apply(Health target, float damagePerSecond, float duration)
    {
        if (target == null || target.isDead) return;
        BurnEffect burn = target.GetComponent<BurnEffect>();
        if (burn == null) { burn = target.gameObject.AddComponent<BurnEffect>(); burn.health = target; }
        burn.damagePerSecond = Mathf.Max(burn.damagePerSecond, damagePerSecond);
        burn.until = Time.time + duration;
    }

    private void Update()
    {
        if (health == null || health.isDead || Time.time >= until) { Destroy(this); return; }
        if (Time.timeScale <= 0f) return;
        tick += Time.deltaTime;
        if (tick < 0.5f) return;
        tick -= 0.5f;
        SpecialWeapons.Damage(health, Mathf.Max(1, Mathf.RoundToInt(damagePerSecond * 0.5f)));
    }
}

public class SlowEffect : MonoBehaviour
{
    private EnemyAI ai;
    private float original, until;
    private bool restored;

    public static void Apply(Health target, float factor, float duration)
    {
        if (target == null) return;
        EnemyAI enemy = target.GetComponent<EnemyAI>();
        if (enemy == null) return; // the boss is not slowed
        SlowEffect slow = enemy.GetComponent<SlowEffect>();
        if (slow == null)
        {
            slow = enemy.gameObject.AddComponent<SlowEffect>();
            slow.ai = enemy;
            slow.original = enemy.speed;
            enemy.speed = slow.original * Mathf.Clamp(factor, 0.05f, 1f);
        }
        slow.until = Time.time + duration;
    }

    private void Update()
    {
        if (ai == null || Time.time >= until) { Restore(); Destroy(this); }
    }

    private void Restore()
    {
        if (ai != null && !restored) ai.speed = original;
        restored = true;
    }

    private void OnDestroy() { Restore(); }
}

// Rockets and ice shots: a straight flying object that bursts on an enemy, a wall or after its range.
public class WeaponProjectile : MonoBehaviour
{
    private SpecialWeapons owner;
    private Vector3 direction;
    private float speed, traveled, maxTravel, age, maxAge;
    private int damage;
    private bool ice;
    private static readonly List<Health> buffer = new List<Health>();

    public static void CreateRocket(SpecialWeapons owner, Vector3 origin, Vector3 direction, int damage)
    {
        Spawn(owner, origin, direction, damage, false, new Color(1f, 0.6f, 0.1f), new Vector3(0.35f, 0.35f, 0.7f));
    }

    public static void CreateIce(SpecialWeapons owner, Vector3 origin, Vector3 direction, int damage)
    {
        Spawn(owner, origin, direction, damage, true, new Color(0.5f, 0.85f, 1f), Vector3.one * 0.4f);
    }

    private static void Spawn(SpecialWeapons owner, Vector3 origin, Vector3 direction, int damage, bool ice, Color color, Vector3 scale)
    {
        WeaponData data = owner.Weapon;
        GameObject item = SpecialWeapons.Prim(ice ? PrimitiveType.Sphere : PrimitiveType.Capsule, ice ? "Ice shot" : "Rocket", origin, scale, color);
        item.transform.rotation = Quaternion.LookRotation(direction) * (ice ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f));
        WeaponProjectile shot = item.AddComponent<WeaponProjectile>();
        shot.owner = owner;
        shot.direction = direction;
        shot.damage = damage;
        shot.ice = ice;
        shot.speed = Mathf.Max(1f, data.bulletSpeed);
        shot.maxTravel = data.range + 6f;
        shot.maxAge = Mathf.Max(0.5f, data.bulletLifeTime);
    }

    private void Update()
    {
        if (!SpecialWeapons.Running) return;
        float step = speed * Time.deltaTime;
        Vector3 start = transform.position;
        Vector3 next = start + direction * step;
        age += Time.deltaTime;
        traveled += step;
        bool wall = ArenaLayout.Instance != null && (!ArenaLayout.Instance.IsWalkable(next, 0.1f) || !ArenaLayout.Instance.PathClear(start, next, 0.1f));
        bool hit = false;
        WeaponTargets.Collect(buffer);
        foreach (Health health in buffer)
            if (WeaponTargets.Flat(next, health.transform.position) <= 0.6f + WeaponTargets.BodyRadius(health)) { hit = true; break; }
        if (wall || hit || traveled >= maxTravel || age >= maxAge) { Burst(wall ? start : next); return; }
        transform.position = next;
    }

    private void Burst(Vector3 point)
    {
        WeaponData data = owner != null ? owner.Weapon : null;
        float radius = data != null ? data.explosionRadius : 3f;
        if (!ice) SpecialWeapons.Blast(point, radius, damage, new Color(1f, 0.55f, 0.1f));
        else
        {
            WeaponTargets.Collect(buffer);
            foreach (Health health in buffer)
                if (WeaponTargets.Flat(point, health.transform.position) <= radius + WeaponTargets.BodyRadius(health) * 0.5f)
                {
                    SpecialWeapons.Damage(health, damage);
                    SlowEffect.Apply(health, data != null ? data.slowFactor : 0.5f, data != null ? data.effectDuration : 3f);
                }
            ExpandingSphere.Create(point, radius, new Color(0.55f, 0.85f, 1f), 0.3f);
        }
        Destroy(gameObject);
    }
}

// Spinning disc: flies out, then returns to the hero; hits each enemy once per leg.
public class WeaponSaw : MonoBehaviour
{
    private SpecialWeapons owner;
    private Vector3 direction;
    private float outDistance, traveled, age, radius, speed;
    private bool returning;
    private int damage;
    private readonly HashSet<Health> hit = new HashSet<Health>();
    private static readonly List<Health> buffer = new List<Health>();

    public static void Create(SpecialWeapons owner, Vector3 origin, Vector3 direction, int damage, float outDistance)
    {
        GameObject item = SpecialWeapons.Prim(PrimitiveType.Cylinder, "Saw", origin, new Vector3(1.4f, 0.06f, 1.4f), new Color(0.95f, 0.6f, 0.2f));
        WeaponSaw saw = item.AddComponent<WeaponSaw>();
        saw.owner = owner;
        saw.direction = direction;
        saw.damage = damage;
        saw.outDistance = Mathf.Max(2f, outDistance);
        saw.radius = Mathf.Max(0.3f, owner.Weapon.beamWidth);
        saw.speed = Mathf.Max(2f, owner.Weapon.bulletSpeed);
        owner.Register(item);
    }

    private void Update()
    {
        if (!SpecialWeapons.Running) return;
        float step = speed * Time.deltaTime;
        age += Time.deltaTime;
        transform.Rotate(0f, 900f * Time.deltaTime, 0f);
        Vector3 position = transform.position;
        if (!returning)
        {
            Vector3 next = position + direction * step;
            bool blocked = ArenaLayout.Instance != null && (!ArenaLayout.Instance.IsWalkable(next, 0.2f) || !ArenaLayout.Instance.PathClear(position, next, 0.2f));
            if (blocked || traveled + step >= outDistance) { returning = true; hit.Clear(); }
            else { transform.position = next; traveled += step; }
        }
        else
        {
            Transform hero = owner != null ? owner.transform : null;
            if (hero == null || age > 8f) { Destroy(gameObject); return; }
            Vector3 target = new Vector3(hero.position.x, position.y, hero.position.z);
            if (Vector3.Distance(position, target) <= 0.9f) { Destroy(gameObject); return; }
            transform.position = Vector3.MoveTowards(position, target, step * 1.15f);
        }
        WeaponTargets.Collect(buffer);
        foreach (Health health in buffer)
            if (!hit.Contains(health) && WeaponTargets.Flat(transform.position, health.transform.position) <= radius + WeaponTargets.BodyRadius(health))
            {
                hit.Add(health);
                SpecialWeapons.Damage(health, damage);
            }
    }
}

// Armed after a short delay; explodes when an enemy walks close.
public class WeaponMine : MonoBehaviour
{
    private SpecialWeapons owner;
    private int damage;
    private float age, armTime = 0.6f, lifetime;
    private Renderer body;
    private static readonly List<Health> buffer = new List<Health>();

    public static void Create(SpecialWeapons owner, Vector3 position, int damage)
    {
        GameObject item = SpecialWeapons.Prim(PrimitiveType.Cylinder, "Mine", new Vector3(position.x, 0.06f, position.z),
            new Vector3(0.9f, 0.06f, 0.9f), new Color(0.9f, 0.3f, 0.1f));
        WeaponMine mine = item.AddComponent<WeaponMine>();
        mine.owner = owner;
        mine.damage = damage;
        mine.lifetime = Mathf.Max(2f, owner.Weapon.lifetime);
        mine.body = item.GetComponent<Renderer>();
        owner.Register(item);
    }

    private void Update()
    {
        if (!SpecialWeapons.Running) return;
        age += Time.deltaTime;
        if (age >= lifetime) { Destroy(gameObject); return; }
        if (age < armTime) return;
        WeaponTargets.Collect(buffer);
        foreach (Health health in buffer)
            if (WeaponTargets.Flat(transform.position, health.transform.position) <= 1.6f + WeaponTargets.BodyRadius(health) * 0.5f)
            {
                SpecialWeapons.Blast(transform.position, owner.Weapon.explosionRadius, damage, new Color(1f, 0.4f, 0.1f));
                Destroy(gameObject);
                return;
            }
    }
}

// Stationary gun: shoots the nearest enemy in range for a limited time.
public class WeaponTurret : MonoBehaviour
{
    private SpecialWeapons owner;
    private float age, timer;
    private Transform barrel;

    public static void Create(SpecialWeapons owner, Vector3 position)
    {
        GameObject item = SpecialWeapons.Prim(PrimitiveType.Cylinder, "Turret", new Vector3(position.x, 0.3f, position.z),
            new Vector3(0.8f, 0.3f, 0.8f), new Color(0.35f, 0.55f, 0.4f));
        GameObject barrel = SpecialWeapons.Prim(PrimitiveType.Cube, "Turret barrel", item.transform.position + Vector3.up * 0.45f,
            new Vector3(0.18f, 0.18f, 0.9f), new Color(0.7f, 0.75f, 0.8f));
        barrel.transform.SetParent(item.transform, true);
        WeaponTurret turret = item.AddComponent<WeaponTurret>();
        turret.owner = owner;
        turret.barrel = barrel.transform;
        owner.Register(item);
    }

    private void Update()
    {
        if (!SpecialWeapons.Running) return;
        WeaponData data = owner.Weapon;
        age += Time.deltaTime;
        timer += Time.deltaTime;
        if (age >= Mathf.Max(2f, data.lifetime)) { Destroy(gameObject); return; }
        if (timer < Mathf.Max(0.05f, data.placedFireRate)) return;
        Transform target = WeaponTargets.Pick(transform.position, data.range, TargetRule.Nearest);
        if (target == null) return;
        timer = 0f;
        Vector3 from = transform.position + Vector3.up * 0.7f;
        Vector3 to = target.position;
        to.y = 0.9f;
        barrel.rotation = Quaternion.LookRotation(new Vector3(to.x - from.x, 0f, to.z - from.z));
        SpecialWeapons.Damage(target.GetComponent<Health>(), owner.Shooter.RollDamage());
        BeamFlash.Create(from, to, new Color(1f, 0.9f, 0.4f), 0.08f, 0.06f);
    }
}

// Orbiting helper gun with its own target; even drones shoot the nearest enemy, odd ones the strongest.
public class WeaponDrone : MonoBehaviour
{
    private SpecialWeapons owner;
    private int index;
    private float angle, timer;

    public static void Create(SpecialWeapons owner, int index)
    {
        GameObject item = SpecialWeapons.Prim(PrimitiveType.Sphere, "Drone", owner.transform.position + Vector3.up * 2f,
            Vector3.one * 0.5f, new Color(0.3f, 0.9f, 0.8f));
        foreach (float side in new[] { -1f, 1f })
        {
            GameObject rotor = SpecialWeapons.Prim(PrimitiveType.Cube, "Drone rotor", item.transform.position + Vector3.right * side * 0.4f,
                new Vector3(0.5f, 0.05f, 0.12f), new Color(0.8f, 0.9f, 0.95f));
            rotor.transform.SetParent(item.transform, true);
        }
        WeaponDrone drone = item.AddComponent<WeaponDrone>();
        drone.owner = owner;
        drone.index = index;
        drone.angle = index * 180f;
        owner.Register(item);
    }

    private void Update()
    {
        if (!SpecialWeapons.Running || owner == null) return;
        WeaponData data = owner.Weapon;
        angle += 80f * Time.deltaTime;
        float radians = angle * Mathf.Deg2Rad;
        transform.position = owner.transform.position + new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * 2.4f + Vector3.up * 2.2f;
        timer += Time.deltaTime;
        if (timer < Mathf.Max(0.05f, data.placedFireRate)) return;
        Transform target = WeaponTargets.Pick(transform.position, data.range, index % 2 == 0 ? TargetRule.Nearest : TargetRule.Strongest);
        if (target == null) return;
        timer = 0f;
        Vector3 to = target.position;
        to.y = 0.9f;
        SpecialWeapons.Damage(target.GetComponent<Health>(), owner.Shooter.RollDamage());
        BeamFlash.Create(transform.position, to, new Color(0.4f, 1f, 0.9f), 0.07f, 0.06f);
    }
}
