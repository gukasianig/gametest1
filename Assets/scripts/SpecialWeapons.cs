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
        return Mathf.Max(1, Mathf.RoundToInt(shooter.RollDamage(false) * multiplier));
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
        // Пока горит, сопло следует за дулом каждый кадр (между выстрелами герой двигается и поворачивается).
        if (flame != null && flame.Emitting) flame.Aim(FlameNozzle(Origin(), flameDirection), flameDirection);
        // Огнемёт бьёт каждые ~0,15 с: звук - непрерывная петля, пока горит пламя, а не новый звук на каждый тик.
        if (flame != null && flame.Emitting && Running) GameAudio.Hold(SfxLoop.Flame, 0.6f);
    }

    // Beams, projectiles and blasts start at the hero's centre (like target selection), not at the muzzle,
    // so lines to far targets are not skewed by the muzzle offset.
    private Vector3 Origin()
    {
        // Ракетница целится двумя руками (AutoShoter.PoseAiming): ракета выходит из жерла модели.
        if (weapon != null && weapon.kind == WeaponKind.Rocket && shooter != null && !support)
        {
            Transform muzzle = ModelSkins.FindMuzzle(shooter.transform);
            if (muzzle != null) return muzzle.position;
        }
        Vector3 point = transform.position;
        // 19.11: высота от земли, а не от плоскости 0 — на холмах GreenArenaPlay снаряды вылетали под землёй.
        point.y = GroundHeight.At(point) + 0.9f;
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
        // Эволюция спецоружия (11.11.2026, AutoShoter.Evolutions): золотые искры у ствола, веер снарядов, сильнее луч/разряд/огонь.
        bool evolved = !support && shooter.Evolved;
        if (evolved) EnemyFx.Puff(origin, EvolvedGold, 0.12f, 0.25f, Vector3.up * 0.6f);
        switch (weapon.kind)
        {
            case WeaponKind.Rail: FireRail(origin, damage, target, evolved); break;
            case WeaponKind.Boomerang:
                // Двойная пила: две пилы веером, предел одновременно летящих тоже вдвое.
                if (CountAlive<WeaponSaw>() < Mathf.Max(1, weapon.maxActive) * (evolved ? 2 : 1))
                {
                    float reach = Mathf.Min(weapon.range, WeaponTargets.Flat(origin, target.position) + 3f);
                    Vector3 aim = FlatDirection(origin, target.position);
                    if (!evolved) WeaponSaw.Create(this, origin, aim, damage, reach);
                    else foreach (float turn in new[] { -14f, 14f }) WeaponSaw.Create(this, origin, Quaternion.Euler(0f, turn, 0f) * aim, damage, reach);
                }
                break;
            case WeaponKind.Mines:
                if (CountAlive<WeaponMine>() < MineLimit) WeaponMine.Create(this, transform.position, damage);
                break;
            case WeaponKind.Rocket:
                // Залп: три ракеты веером.
                foreach (float turn in evolved ? new[] { -12f, 0f, 12f } : new[] { 0f })
                    WeaponProjectile.CreateRocket(this, origin, Quaternion.Euler(0f, turn, 0f) * FlatDirection(origin, target.position), damage);
                // Отскок ракетницы, дым у жерла и из сопла, боеголовка в жерле возвращается к середине перезарядки.
                WeaponFireAnimation fx = shooter != null ? shooter.WeaponFx : ModelSkins.CurrentWeaponFx;
                if (fx != null) fx.Play(1f, Mathf.Clamp(weapon.fireRate * 0.5f, 0.15f, 0.8f));
                break;
            case WeaponKind.Ice:
                // Абсолютный ноль: три осколка веером.
                foreach (float turn in evolved ? new[] { -10f, 0f, 10f } : new[] { 0f })
                    WeaponProjectile.CreateIce(this, origin, Quaternion.Euler(0f, turn, 0f) * FlatDirection(origin, target.position), damage);
                break;
            case WeaponKind.Arc: FireArc(origin, damage, target, evolved); break;
            case WeaponKind.Flame: FireFlame(origin, damage, target, evolved); break;
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

    private static readonly Color EvolvedGold = new Color(1f, 0.78f, 0.25f);

    private void FireRail(Vector3 origin, int damage, Transform target, bool evolved = false)
    {
        float width = weapon.beamWidth * (evolved ? 2f : 1f);   // Гаусс-пушка: луч вдвое шире
        Vector3 direction = FlatDirection(origin, target.position);
        float length = Mathf.Max(2f, weapon.range);
        float clipped = length;
        if (ArenaLayout.Instance != null)
            for (float d = 1f; d <= length; d += 1f)
                if (!ArenaLayout.Instance.PathClear(origin, origin + direction * d, 0.05f)) { clipped = Mathf.Max(0.5f, d - 1f); break; }
        Vector3 end = origin + direction * clipped;
        WeaponTargets.Collect(scratch);
        foreach (Health health in scratch)
            if (SegmentDistance(origin, end, health.transform.position) <= width + WeaponTargets.BodyRadius(health))
                Hit(health, damage);
        BeamFlash.Create(origin, end, new Color(0.55f, 0.9f, 1f), 0.35f * (evolved ? 1.8f : 1f), 0.15f);
        if (evolved) BeamFlash.Create(origin, end, EvolvedGold, 0.18f, 0.22f);
    }

    private void FireArc(Vector3 origin, int damage, Transform target, bool evolved = false)
    {
        var hit = new List<Health>();
        Health current = target.GetComponent<Health>();
        Vector3 from = origin;
        float strength = damage;
        // Буря: ещё 3 прыжка, урон за прыжок слабеет медленнее, разряд золотистый.
        int jumps = weapon.chainJumps + (evolved ? 3 : 0);
        float falloff = evolved ? Mathf.Max(weapon.chainFalloff, 0.85f) : weapon.chainFalloff;
        for (int jump = 0; jump <= jumps && current != null; jump++)
        {
            Vector3 point = current.transform.position + Vector3.up * 0.2f;
            point.y = 0.9f;
            Hit(current, Mathf.Max(1, Mathf.RoundToInt(strength)));
            hit.Add(current);
            Color electric = evolved ? new Color(1f, 0.85f, 0.45f) : new Color(0.35f, 0.6f, 1f);
            LightningBolt.Create(from, point, electric, jump == 0 ? 0.42f : 0.34f, jump == 0 ? 0.55f : 0.48f, jump == 0 ? 3 : 2);
            if (jump == 0) LightningBolt.Impact(from, electric, 0.45f, false);   // вспышка у ствола
            LightningBolt.Impact(point, electric, 1f - 0.12f * jump);
            from = point;
            strength *= falloff;
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

    private Vector3 flameDirection = Vector3.forward;

    // Сопло огнемёта: дуло модели в руке (если есть), иначе точка перед героем.
    private Vector3 FlameNozzle(Vector3 fallback, Vector3 direction)
    {
        if (shooter != null ? shooter.TryGetMuzzle(out Vector3 muzzle) : ModelSkins.TryGetMuzzle(out muzzle)) return muzzle;
        return fallback + direction * 0.8f;
    }

    private void FireFlame(Vector3 origin, int damage, Transform target, bool evolved = false)
    {
        Vector3 direction = FlatDirection(origin, target.position);
        // Напалм: конус шире на 20°, дальность +25%, горение вдвое сильнее.
        float half = weapon.coneAngle * 0.5f + (evolved ? 10f : 0f);
        float reach = weapon.range * (evolved ? 1.25f : 1f);
        WeaponTargets.Collect(scratch);
        foreach (Health health in scratch)
        {
            Vector3 offset = health.transform.position - origin;
            offset.y = 0f;
            float distance = offset.magnitude;
            float radius = WeaponTargets.BodyRadius(health);
            if (distance > reach + radius) continue;
            // Wide bodies count when their edge is inside the cone.
            float slack = distance > 0.01f ? Mathf.Atan2(radius, distance) * Mathf.Rad2Deg : 180f;
            if (distance > 0.8f && Vector3.Angle(direction, offset) > half + slack) continue;
            Health.HitSoundScale = 0.35f;   // тики огня частые - попадания тише
            try { Hit(health, damage); }
            finally { Health.HitSoundScale = 1f; }
            // Горение растёт вместе с уроном героя (раньше было плоским 6/с и отставало от улучшений).
            float burnScale = Mathf.Max(1f, damage / (float)Mathf.Max(1, weapon.damage));
            BurnEffect.Apply(health, weapon.burnPerSecond * burnScale * (evolved ? 2f : 1f), weapon.effectDuration);
        }
        // 09.11.2026: вместо растянутой оранжевой сферы — струя огня из частиц (FlameJet) от сопла.
        if (flame == null) flame = FlameJet.Create();
        // Струя Напалма длиннее и шире (перенастройка только при смене значений).
        if (!Mathf.Approximately(flameRange, reach)) { flameRange = reach; flame.Configure(reach, half * 2f); }
        flameDirection = direction;
        flame.Aim(FlameNozzle(origin, direction), direction);   // 10.11.2026: струя идёт от дула модели, а не от центра тела героя
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
    private float speed, traveled, maxTravel, age, maxAge, lift;
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
        // Снаряд держит высоту над землёй (как пила): на подъёме не прячется в холм.
        shot.lift = Mathf.Clamp(origin.y - GroundHeight.At(origin), 0.5f, 1.8f);
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
        float ground = GroundHeight.At(next) + lift;
        next.y = Mathf.Abs(next.y - ground) > 3f ? ground : Mathf.Lerp(next.y, ground, 1f - Mathf.Exp(-14f * Time.deltaTime));
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
    private float outDistance, traveled, age, radius, speed, lift, spin;
    private bool returning;
    private int damage;
    private Vector3 fullScale;
    private readonly HashSet<Health> hit = new HashSet<Health>();
    private static readonly List<Health> buffer = new List<Health>();
    // 19.11 (владелец): диск 1,4 м был слишком крупным и появлялся у ствола сразу целиком. Видимый размер 0,85 м
    // (зона урона по-прежнему beamWidth), диск вырастает за 0,15 с и летит с наклоном по ходу.
    private const float VisualSize = 0.85f, GrowTime = 0.15f, Tilt = 14f;

    public static void Create(SpecialWeapons owner, Vector3 origin, Vector3 direction, int damage, float outDistance)
    {
        GameObject item = SpecialWeapons.Prim(PrimitiveType.Cylinder, "Saw", origin, new Vector3(VisualSize, 0.05f, VisualSize), new Color(0.95f, 0.6f, 0.2f));
        WeaponSaw saw = item.AddComponent<WeaponSaw>();
        saw.owner = owner;
        saw.direction = direction;
        saw.damage = damage;
        saw.outDistance = Mathf.Max(2f, outDistance);
        saw.radius = Mathf.Max(0.3f, owner.Weapon.beamWidth);
        saw.speed = Mathf.Max(2f, owner.Weapon.bulletSpeed);
        // Высота над землёй, а не над нулём: на рельефе диск идёт по склону, а не уходит в холм.
        // Точка вылета спецоружия считается от плоскости 0 (≈0,9 м): на холме она под землёй, поэтому берём её как высоту над землёй.
        float ground = GroundHeight.At(origin);
        saw.lift = Mathf.Clamp(origin.y - ground, 0.45f, 1.1f);
        item.transform.position = new Vector3(origin.x, ground + saw.lift, origin.z);
        saw.fullScale = item.transform.localScale;
        item.transform.localScale = saw.fullScale * 0.25f;
        saw.Orient();
        owner.Register(item);
    }

    private void Orient()
    {
        Vector3 travel = direction;
        if (returning && owner != null) { travel = owner.transform.position - transform.position; travel.y = 0f; }
        if (travel.sqrMagnitude < 0.0001f) travel = Vector3.forward;
        // Наклон «носом» по ходу полёта, вращение диска в его собственной плоскости.
        transform.rotation = Quaternion.LookRotation(travel.normalized) * Quaternion.Euler(Tilt, 0f, 0f) * Quaternion.Euler(0f, spin, 0f);
    }

    private void Update()
    {
        if (!SpecialWeapons.Running) return;
        float step = speed * Time.deltaTime;
        age += Time.deltaTime;
        spin = (spin + 900f * Time.deltaTime) % 360f;
        if (age < GrowTime) transform.localScale = fullScale * Mathf.Lerp(0.25f, 1f, Mathf.SmoothStep(0f, 1f, age / GrowTime));
        else if (transform.localScale != fullScale) transform.localScale = fullScale;
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
            if (WeaponTargets.Flat(position, target) <= 0.9f) { Destroy(gameObject); return; }
            transform.position = Vector3.MoveTowards(position, target, step * 1.15f);
        }
        Vector3 placed = transform.position;
        float ground = GroundHeight.At(placed) + lift;
        placed.y = Mathf.Abs(placed.y - ground) > 3f ? ground : Mathf.Lerp(placed.y, ground, 1f - Mathf.Exp(-18f * Time.deltaTime));
        if (placed.y < GroundHeight.At(placed) + 0.25f) placed.y = GroundHeight.At(placed) + 0.25f;
        transform.position = placed;
        Orient();
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
        Transform lamp = ItemModelParts.Find(item.transform, "Light");
        mine.lamp = lamp != null ? lamp.GetComponent<Renderer>() : null;
        mine.pop = new ItemModelParts.Pop(item.transform);
        owner.Register(item);
    }

    private Renderer lamp;
    private ItemModelParts.Pop pop;

    private void Update()
    {
        if (!SpecialWeapons.Running) return;
        age += Time.deltaTime;
        pop.Tick(age);
        if (age >= lifetime) { Destroy(gameObject); return; }
        // Огонёк модели: погашен до взвода, затем мигает (чаще к концу срока).
        if (lamp != null) lamp.enabled = age >= armTime && Mathf.Repeat(age * (age > lifetime - 2f ? 6f : 2.5f), 1f) < 0.55f;
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
// 19.11.2026: модель-миниган (make_items.py): стволы Barrels раскручиваются перед очередью и плавно останавливаются,
// каждый выстрел — короткая очередь из трёх трассеров (урон прежний, один раз за выстрел), отдача головы, гильзы,
// вспышка у дула; в конце срока турель оседает и исчезает с дымком.
public class WeaponTurret : MonoBehaviour
{
    private const float SpinMax = 1800f, SpinUp = 2400f, SpinDown = 700f, Burst = 3, BurstGap = 0.045f, FadeTime = 0.35f;
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
        // Модель турели (make_items.py): поворотная голова Head, блок стволов Barrels и точка выстрела TurretMuzzle.
        turret.head = ItemModelParts.Find(item.transform, "Head");
        turret.barrels = ItemModelParts.Find(item.transform, "Barrels");
        turret.muzzle = ItemModelParts.Find(item.transform, "TurretMuzzle");
        if (turret.head != null) turret.headRest = turret.head.localPosition;
        turret.skin = item.transform.Find("SkinModel");
        turret.pop = new ItemModelParts.Pop(item.transform);
        owner.Register(item);
    }

    private Transform head, barrels, muzzle, skin;
    private Vector3 headRest;
    private ItemModelParts.Pop pop;
    private Vector3 aim;
    private bool aiming;
    private float spin, lastShot = -10f, kick;
    private int tracersLeft;
    private float nextTracer;
    private Vector3 tracerTarget;

    public int TracersLeft => tracersLeft;
    public float Spin => spin;

    private void Update()
    {
        if (!SpecialWeapons.Running) return;
        WeaponData data = owner.Weapon;
        float life = Mathf.Max(2f, data.lifetime);
        age += Time.deltaTime;
        timer += Time.deltaTime;
        pop.Tick(age);
        // Конец срока: турель оседает и сжимается, затем исчезает с дымком.
        if (age >= life - FadeTime && skin != null)
        {
            float f = Mathf.Clamp01((age - (life - FadeTime)) / FadeTime);
            skin.localScale = skinSize * Mathf.Max(0.01f, 1f - f * f);
        }
        if (age >= life)
        {
            EnemyFx.Puff(transform.position + Vector3.up * 0.5f, new Color(0.6f, 0.62f, 0.6f), 0.5f, 0.5f, Vector3.up * 0.8f);
            Destroy(gameObject);
            return;
        }
        Animate(data);
        if (timer < Mathf.Max(0.05f, data.placedFireRate)) return;
        Transform target = WeaponTargets.Pick(transform.position, data.range, TargetRule.Nearest);
        if (target == null) return;
        lastSeen = Time.time;
        Vector3 to = target.position;
        to.y = 0.9f;
        aim = to;
        aiming = true;
        // Стволы сначала раскручиваются: стрельба только на половине оборотов (первая очередь чуть позже).
        if (barrels != null && spin < SpinMax * 0.5f) return;
        timer = 0f;
        if (head != null && muzzle != null) ItemModelParts.AimYaw(head, muzzle.position, aim, 360f);
        Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up * 0.7f;
        barrel.rotation = Quaternion.LookRotation(new Vector3(to.x - from.x, 0f, to.z - from.z));
        GameAudio.Play(Sfx.Zap, 0.35f);
        owner.Hit(target.GetComponent<Health>(), owner.SupportDamage(owner.TurretDamage));
        lastShot = Time.time;
        tracerTarget = to;
        tracersLeft = (int)Burst;
        nextTracer = Time.time;
    }

    private Vector3 skinSize = Vector3.one;
    private bool skinKnown;
    private float lastSeen = -10f;

    // Раскрутка стволов, доворот головы, отдача и очередь трассеров.
    private void Animate(WeaponData data)
    {
        if (skin != null && !skinKnown && age > 0.3f) { skinSize = skin.localScale; skinKnown = true; }
        bool engaged = Time.time - lastSeen < 0.8f;
        spin = Mathf.MoveTowards(spin, engaged ? SpinMax : 0f, (engaged ? SpinUp : SpinDown) * Time.deltaTime);
        if (barrels != null && muzzle != null && spin > 1f)
        {
            Vector3 axis = muzzle.position - barrels.position;
            if (axis.sqrMagnitude > 1e-6f) barrels.Rotate(axis.normalized, spin * Time.deltaTime, Space.World);
        }
        // Голова плавно доворачивает к последней цели (до 540°/с).
        if (aiming && head != null && muzzle != null) ItemModelParts.AimYaw(head, muzzle.position, aim, 540f * Time.deltaTime);
        kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 0.6f);
        if (head != null && muzzle != null && head.parent != null)
        {
            // Отдача назад по линии стволов, в метрах мира (у модели свой масштаб внутри SkinModel).
            Vector3 back = head.position - muzzle.position;
            back.y = 0f;
            head.localPosition = headRest + (back.sqrMagnitude > 1e-6f ? head.parent.InverseTransformVector(back.normalized * kick) : Vector3.zero);
        }
        while (tracersLeft > 0 && Time.time >= nextTracer)
        {
            tracersLeft--;
            nextTracer += BurstGap;
            Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up * 0.7f;
            Vector3 jitter = new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(-0.15f, 0.2f), Random.Range(-0.25f, 0.25f));
            BeamFlash.Create(from, tracerTarget + jitter, new Color(1f, 0.85f, 0.35f), 0.05f, 0.05f);
            EnemyFx.Puff(from, new Color(1f, 0.8f, 0.35f), 0.14f, 0.07f, Vector3.zero);
            kick = 0.035f;
            // Гильза вылетает вбок из ящика.
            Vector3 side = head != null ? head.right : transform.right;
            Vector3 breech = muzzle != null && head != null ? from - (muzzle.position - head.position).normalized * 0.35f : from;
            EnemyFx.Blobs(breech + side * 0.12f,
                new Color(0.95f, 0.72f, 0.3f), 1, 2.5f, 0.05f, 0.45f, GroundHeight.At(transform.position));
        }
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
        // Модель дрона (make_items.py): винты RotorL/RotorR вращаются, глаз Eye смотрит на цель.
        drone.rotors = new[] { ItemModelParts.Find(item.transform, "RotorL"), ItemModelParts.Find(item.transform, "RotorR") };
        Transform eye = ItemModelParts.Find(item.transform, "Eye");
        drone.eye = eye != null ? eye.GetComponent<Renderer>() : null;
        drone.pop = new ItemModelParts.Pop(item.transform);
        owner.Register(item);
    }

    private Transform[] rotors;
    private Renderer eye;
    private ItemModelParts.Pop pop;
    private float age;
    private Vector3 look;

    private void Update()
    {
        if (!SpecialWeapons.Running || owner == null) return;
        WeaponData data = owner.Weapon;
        age += Time.deltaTime;
        pop.Tick(age);
        angle += 80f * Time.deltaTime;
        // Drones are spread evenly on the circle, however many there are.
        float radians = (angle + index * 360f / Mathf.Max(1, owner.DroneLimit)) * Mathf.Deg2Rad;
        float bob = Mathf.Sin(age * 3.1f + index) * 0.12f;
        transform.position = owner.transform.position + new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * 2.4f + Vector3.up * (2.2f + bob);
        foreach (Transform rotor in rotors)
            if (rotor != null) rotor.Rotate(Vector3.up, 1500f * Time.deltaTime, Space.World);
        // Без цели глаз смотрит по ходу облёта, с целью — на неё.
        Vector3 tangent = new Vector3(-Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        Vector3 facing = look.sqrMagnitude > 0f ? look : transform.position + tangent;
        if (eye != null) ItemModelParts.AimYaw(transform, eye.bounds.center, facing, 300f * Time.deltaTime);
        timer += Time.deltaTime;
        if (timer > 1.5f) look = Vector3.zero;
        if (timer < Mathf.Max(0.05f, data.placedFireRate)) return;
        Transform target = WeaponTargets.Pick(transform.position, data.range, index % 2 == 0 ? TargetRule.Nearest : TargetRule.Strongest);
        if (target == null) return;
        timer = 0f;
        Vector3 to = target.position;
        to.y = 0.9f;
        look = to;
        GameAudio.Play(Sfx.Zap, 0.3f, 0.12f);
        owner.Hit(target.GetComponent<Health>(), owner.SupportDamage(owner.DroneDamage));
        // Вместо длинного луча: сгусток летит к цели, у дрона вспышка и отдача, в цели — искры.
        Color boltColor = new Color(0.4f, 1f, 0.9f);
        Vector3 dir = (to - transform.position).normalized;
        Vector3 muzzle = transform.position + dir * 0.45f;
        DroneBolt.Launch(muzzle, to, boltColor);
        EnemyFx.LightFlash(muzzle, boltColor, 3f, 2.5f, 0.12f);
        EnemyFx.Puff(muzzle, boltColor, 0.3f, 0.18f, dir * 1.5f);
        kick = -dir * 0.35f;
        flash = 1f;
    }

    private Vector3 kick;
    private float flash;

    private void LateUpdate()
    {
        if (!SpecialWeapons.Running) return;
        // Отдача затухает, глаз вспыхивает увеличением в момент выстрела.
        kick = Vector3.Lerp(kick, Vector3.zero, 1f - Mathf.Exp(-Time.deltaTime * 14f));
        transform.position += kick;
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 6f);
        if (eye != null) eye.transform.localScale = Vector3.one * (1f + 0.6f * flash);
    }
}

// Быстрый светящийся сгусток дрона (только визуально: урон наносится при выстреле).
public class DroneBolt : MonoBehaviour
{
    private Vector3 target;
    private Color color;

    public static void Launch(Vector3 from, Vector3 to, Color color)
    {
        GameObject go = SpecialWeapons.Prim(PrimitiveType.Sphere, "Drone bolt", from, Vector3.one * 0.22f, color);
        TrailRenderer trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.12f;
        trail.widthMultiplier = 0.2f;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        trail.sharedMaterial = EnemyFx.Mat(color);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        DroneBolt bolt = go.AddComponent<DroneBolt>();
        bolt.target = to;
        bolt.color = color;
    }

    private void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, target, 45f * Time.deltaTime);
        if ((transform.position - target).sqrMagnitude > 0.01f) return;
        EnemyFx.Blobs(target, color, 5, 3f, 0.07f, 0.25f, GroundHeight.At(target));
        EnemyFx.LightFlash(target, color, 2.5f, 2f, 0.1f);
        Destroy(gameObject);
    }
}
