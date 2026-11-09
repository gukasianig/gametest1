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
    private FlameJet flame;   // струя огнемёта из частиц (FlameJet)
    private float flameRange = -1f;
    private float flameUntil;
    private static readonly List<Health> scratch = new List<Health>();
    private static readonly List<Health> blastBuffer = new List<Health>();

    public WeaponData Weapon { get { return weapon; } }
    public AutoShoter Shooter { get { return shooter; } }

    public void Hit(Health health, int amount)
    {
        if (shooter != null && shooter.ElementAttack != null) shooter.ElementAttack.Hit(health, amount);
        else Damage(health, amount);
    }

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
        SyncDrones();
    }

    // Юниты из улучшений боя (support = true): их число задаёт только улучшение. Без support — то же оружие,
    // выбранное основным (сейчас только в WeaponChecks): число берётся из weapon.maxActive.
    public bool support;
    public int Drones { get; private set; }
    public int Turrets { get; private set; }
    public int Mines { get; private set; }
    public float DroneDamage { get; private set; } = 1f;
    public float TurretDamage { get; private set; } = 1f;
    private int BaseActive { get { return weapon == null ? 0 : Mathf.Max(1, weapon.maxActive); } }
    public int DroneLimit { get { return support ? Drones : BaseActive; } }
    public int TurretLimit { get { return support ? Turrets : BaseActive; } }
    public int MineLimit { get { return support ? SupportMines(Mines) : BaseActive; } }
    // Улучшение «Сапёрные мины»: 1-й ранг — 3 мины, каждый следующий +1.
    public static int SupportMines(int ranks) { return ranks <= 0 ? 0 : 2 + ranks; }

    public void SetUnits(int drones, int turrets, int mines, float droneDamage, float turretDamage)
    {
        Drones = Mathf.Max(0, drones);
        Turrets = Mathf.Max(0, turrets);
        Mines = Mathf.Max(0, mines);
        DroneDamage = Mathf.Max(1f, droneDamage);
        TurretDamage = Mathf.Max(1f, turretDamage);
        SyncDrones();
    }

    public int SupportDamage(float multiplier)
    {
        return Mathf.Max(1, Mathf.RoundToInt(shooter.RollDamage() * multiplier));
    }

    private void SyncDrones()
    {
        if (weapon == null || weapon.kind != WeaponKind.Drone) return;
        for (int i = CountAlive<WeaponDrone>(); i < DroneLimit; i++) WeaponDrone.Create(this, i);
    }

    public void Clear()
    {
        foreach (GameObject item in placed) if (item != null) Destroy(item);
        placed.Clear();
        if (flame != null) Destroy(flame.gameObject);
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
        if (flame != null && flame.Emitting && Time.time > flameUntil) flame.Stop();
        // Огнемёт бьёт каждые ~0,15 с: звук - непрерывная петля, пока горит пламя, а не новый звук на каждый тик.
        if (flame != null && flame.Emitting && Running) GameAudio.Hold(SfxLoop.Flame, 0.6f);
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
        ModelSkins.ApplySpecial(item, name);
        return item;
    }

    // Damage everything within the radius and show a short expanding sphere.
    public static void Blast(Vector3 center, float radius, int damage, Color color, SpecialWeapons source = null)
    {
        GameAudio.Play(Sfx.Explosion, 0.75f);   // ракета и мина
        WeaponTargets.Collect(blastBuffer);
        foreach (Health health in blastBuffer)
            if (WeaponTargets.Flat(center, health.transform.position) <= radius + WeaponTargets.BodyRadius(health) * 0.5f)
            {
                if (source != null) source.Hit(health, damage);
                else Damage(health, damage);
            }
        // Ракета и мина: полноценный взрыв (вспышка, огонь, дым, искры, обломки) вместо простой сферы.
        EnemyFx.Explosion(new Vector3(center.x, GroundHeight.At(center), center.z), radius * 0.8f, Mathf.Clamp01(radius / 5f));
    }

    public void Fire(int damage, Transform target)
    {
        if (weapon == null || shooter == null || target == null || !Running) return;
        // Отдача модели в руке: лёгкая для большинства (дымок только у «огнестрельных»), у ракетницы — полная анимация ниже.
        switch (weapon.kind)
        {
            case WeaponKind.Rail: shooter.PlayWeaponRecoil(0.5f, true); break;
            case WeaponKind.Ice: shooter.PlayWeaponRecoil(0.3f, false); break;
            case WeaponKind.Arc: shooter.PlayWeaponRecoil(0.2f, false); break;
            case WeaponKind.Flame: shooter.PlayWeaponRecoil(0.08f, false); break;
            case WeaponKind.Rocket: shooter.PlayWeaponRecoil(0f, false); break;
        }
        PlayFireSound();
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
                if (CountAlive<WeaponMine>() < MineLimit) WeaponMine.Create(this, transform.position, damage);
                break;
            case WeaponKind.Rocket:
                WeaponProjectile.CreateRocket(this, origin, FlatDirection(origin, target.position), damage);
                // Отскок ракетницы, дым у жерла и из сопла, боеголовка в жерле возвращается к середине перезарядки.
                WeaponFireAnimation fx = ModelSkins.CurrentWeaponFx;
                if (fx != null) fx.Play(1f, Mathf.Clamp(weapon.fireRate * 0.5f, 0.15f, 0.8f));
                break;
            case WeaponKind.Ice:
                WeaponProjectile.CreateIce(this, origin, FlatDirection(origin, target.position), damage);
                break;
            case WeaponKind.Arc: FireArc(origin, damage, target); break;
            case WeaponKind.Flame: FireFlame(origin, damage, target); break;
            case WeaponKind.Turret:
                if (CountAlive<WeaponTurret>() < TurretLimit)
                {
                    Vector3 spot = transform.position + FlatDirection(transform.position, target.position) * 1.5f;
                    if (ArenaLayout.Instance != null && !ArenaLayout.Instance.IsWalkable(spot, 0.5f)) spot = transform.position;
                    WeaponTurret.Create(this, spot);
                }
                break;
            default: break; // drones act on their own
        }
    }

    private void PlayFireSound()
    {
        switch (weapon.kind)
        {
            case WeaponKind.Rail: GameAudio.Play(Sfx.Rail); break;
            case WeaponKind.Arc: GameAudio.Play(Sfx.Arc); break;
            case WeaponKind.Rocket: GameAudio.Play(Sfx.Rocket); break;
            case WeaponKind.Ice: GameAudio.Play(Sfx.Ice); break;
            // Только вспышка зажигания, когда пламя загорается заново; дальше звучит петля (Update).
            case WeaponKind.Flame: if (flame == null || !flame.Emitting) GameAudio.Play(Sfx.Flame, 0.5f); break;
            case WeaponKind.Boomerang: GameAudio.Play(Sfx.Throw); break;
            case WeaponKind.Turret: if (CountAlive<WeaponTurret>() < TurretLimit) GameAudio.Play(Sfx.Deploy, 0.6f); break;
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
                Hit(health, damage);
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
            Hit(current, Mathf.Max(1, Mathf.RoundToInt(strength)));
            hit.Add(current);
            Color electric = new Color(0.35f, 0.6f, 1f);
            LightningBolt.Create(from, point, electric, jump == 0 ? 0.42f : 0.34f, jump == 0 ? 0.55f : 0.48f, jump == 0 ? 3 : 2);
            if (jump == 0) LightningBolt.Impact(from, electric, 0.45f, false);   // вспышка у ствола
            LightningBolt.Impact(point, electric, 1f - 0.12f * jump);
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
            Health.HitSoundScale = 0.35f;   // тики огня частые - попадания тише
            try { Hit(health, damage); }
            finally { Health.HitSoundScale = 1f; }
            BurnEffect.Apply(health, weapon.burnPerSecond, weapon.effectDuration);
        }
        // 09.11.2026: вместо растянутой оранжевой сферы — струя огня из частиц (FlameJet) от сопла.
        if (flame == null) flame = FlameJet.Create();
        if (!Mathf.Approximately(flameRange, weapon.range)) { flameRange = weapon.range; flame.Configure(weapon.range, weapon.coneAngle); }
        flame.Aim(origin, direction);
        flameUntil = Time.time + 0.2f;
        IgniteGround(origin, direction, target);
    }

    // --- Огнемёт поджигает землю: небольшие очаги в конце струи поджигают врагов, героя не жгут.
    private const int GroundFireLimit = 5;
    private const float GroundFireInterval = 0.6f, GroundFireLife = 3f, GroundFireMerge = 1.3f;
    private readonly List<GroundFireZone> groundFires = new List<GroundFireZone>();
    private float nextGroundFire;
    public int GroundFireCount { get { groundFires.RemoveAll(f => f == null || !f.Burning); return groundFires.Count; } }

    private void IgniteGround(Vector3 origin, Vector3 direction, Transform target)
    {
        if (Time.time < nextGroundFire) return;
        nextGroundFire = Time.time + GroundFireInterval;
        Vector3 point = origin + direction * weapon.range * 0.65f;
        float toTarget = WeaponTargets.Flat(origin, target.position);
        if (toTarget < weapon.range) point = origin + direction * Mathf.Max(1.5f, toTarget);
        point.y = 0f;
        if (ArenaLayout.Instance != null && !ArenaLayout.Instance.IsWalkable(point, 0.5f)) return;

        groundFires.RemoveAll(f => f == null || !f.Burning);
        foreach (GroundFireZone fire in groundFires)
        {
            if (WeaponTargets.Flat(fire.transform.position, point) > GroundFireMerge) continue;
            fire.Refresh(GroundFireLife, 0.15f, 1.8f);
            return;
        }
        if (groundFires.Count >= GroundFireLimit)
        {
            groundFires[0].Extinguish();
            groundFires.RemoveAt(0);
        }
        float burn = weapon.burnPerSecond, duration = Mathf.Max(1f, weapon.effectDuration * 0.5f);
        GroundFireZone zone = GroundFireZone.Spawn(point, 1f, 1.3f, 0.5f, GroundFireLife,
            health => BurnEffect.Apply(health, burn, duration), 0);
        groundFires.Add(zone);
        Register(zone.gameObject);
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
        Health.HitSoundScale = 0f;   // горение без звука попадания
        try { SpecialWeapons.Damage(health, Mathf.Max(1, Mathf.RoundToInt(damagePerSecond * 0.5f))); }
        finally { Health.HitSoundScale = 1f; }
    }
}

public class SlowEffect : MonoBehaviour
{
    private float until;
    public float Factor { get; private set; } = 1f;

    public static float MovementFactor(Component target)
    {
        SlowEffect slow = target.GetComponent<SlowEffect>();
        return slow != null && Time.time < slow.until ? slow.Factor : 1f;
    }

    public static void Apply(Health target, float factor, float duration)
    {
        if (target == null || target.isDead) return;
        bool boss = target.GetComponent<BossController>() != null;
        if (!boss && target.GetComponent<EnemyAI>() == null) return;
        factor = Mathf.Clamp(factor, .5f, 1f);
        if (boss) factor = 1f - (1f - factor) * .5f;
        SlowEffect slow = target.GetComponent<SlowEffect>();
        if (slow == null) slow = target.gameObject.AddComponent<SlowEffect>();
        slow.Factor = Time.time < slow.until ? Mathf.Min(slow.Factor, factor) : factor;
        slow.until = Mathf.Max(slow.until, Time.time + Mathf.Max(0f, duration));
    }

    private void Update()
    {
        if (Time.time >= until) Destroy(this);
    }

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
        GameObject item = ProjectileModels.Create(ice ? "ice_shard" : "rocket", origin, Quaternion.LookRotation(direction), ice ? 0.75f : 1.6f);
        if (item == null)
        {
            item = SpecialWeapons.Prim(ice ? PrimitiveType.Sphere : PrimitiveType.Capsule, ice ? "Ice shot" : "Rocket", origin, scale, color);
            item.transform.rotation = Quaternion.LookRotation(direction) * (ice ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f));
        }
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
        if (!ice) SpecialWeapons.Blast(point, radius, damage, new Color(1f, 0.55f, 0.1f), owner);
        else
        {
            WeaponTargets.Collect(buffer);
            foreach (Health health in buffer)
                if (WeaponTargets.Flat(point, health.transform.position) <= radius + WeaponTargets.BodyRadius(health) * 0.5f)
                {
                    if (owner != null) owner.Hit(health, damage);
                    else SpecialWeapons.Damage(health, damage);
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
        // Звук вращения: громче рядом с героем, выше на быстрой пиле и на возврате.
        float near = owner != null ? Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(WeaponTargets.Flat(position, owner.transform.position) / 18f)) : 0.6f;
        GameAudio.Hold(SfxLoop.Saw, 0.55f * near, Mathf.Lerp(0.9f, 1.25f, Mathf.InverseLerp(6f, 30f, speed)) * (returning ? 1.06f : 1f));
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
                GameAudio.Play(Sfx.SawCut, 0.8f);
                GameAudio.Load(SfxLoop.Saw, 0.8f);   // диск «вязнет» в теле и снова раскручивается
                if (owner != null) owner.Hit(health, damage);
                else SpecialWeapons.Damage(health, damage);
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
                SpecialWeapons.Blast(transform.position, owner.Weapon.explosionRadius, damage, new Color(1f, 0.4f, 0.1f), owner);
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
        GameAudio.Play(Sfx.Zap, 0.35f);
        owner.Hit(target.GetComponent<Health>(), owner.SupportDamage(owner.TurretDamage));
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
        // Drones are spread evenly on the circle, however many there are.
        float radians = (angle + index * 360f / Mathf.Max(1, owner.DroneLimit)) * Mathf.Deg2Rad;
        transform.position = owner.transform.position + new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * 2.4f + Vector3.up * 2.2f;
        timer += Time.deltaTime;
        if (timer < Mathf.Max(0.05f, data.placedFireRate)) return;
        Transform target = WeaponTargets.Pick(transform.position, data.range, index % 2 == 0 ? TargetRule.Nearest : TargetRule.Strongest);
        if (target == null) return;
        timer = 0f;
        Vector3 to = target.position;
        to.y = 0.9f;
        GameAudio.Play(Sfx.Zap, 0.3f, 0.12f);
        owner.Hit(target.GetComponent<Health>(), owner.SupportDamage(owner.DroneDamage));
        BeamFlash.Create(transform.position, to, new Color(0.4f, 1f, 0.9f), 0.07f, 0.06f);
    }
}
