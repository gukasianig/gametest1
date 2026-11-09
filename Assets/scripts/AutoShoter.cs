using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoShoter : MonoBehaviour
{
   public Transform firePoint;
   public GameObject bulletPrefab;
   public float fireRate = 0.5f;
   public float range = 20f;
   public int damage = 20;
   public GameObject grenadePrefab;
   public float grenadeCooldown = 3f;
   float lastGrenadeTime = 0f;
   public int grenadeDamage = 30;
   public float grenadeRadius = 3f;
   public WeaponData currentWeapon;
   public WeaponData[] weapons;
   public int startWeaponIndex = 0;
   public WeaponData baseWeapon;

   public GameObject orbitWeaponPrefab;

   private int currentDamage;
    private PlayMovement movement;
    private HeroLocomotion hero;
    public int CurrentDamage => currentDamage;
    public float CurrentFireInterval => currentFireRate;
    public int CurrentPelletCount => Mathf.Max(1, currentPelletCount);
    public float CurrentSpread => currentPelletCount > 1 ? currentSpread : 0f;
    private float progressionDamage = 1f;
    private float progressionAttackSpeed = 1f;
    private float grenadeDamageMultiplier = 1f;
    private float grenadeRadiusMultiplier = 1f;
    public int CurrentGrenadeDamage => Mathf.RoundToInt(grenadeDamage * grenadeDamageMultiplier);
    public float CurrentGrenadeRadius => grenadeRadius * grenadeRadiusMultiplier;

    // Граната бросается только после первого улучшения «Гранаты» (урон или радиус).
    public bool GrenadeUnlocked { get; private set; }
    public void SetGrenadeUnlocked(bool unlocked) { GrenadeUnlocked = unlocked; }

    public void SetSecondaryMultipliers(float damageMultiplier, float radiusMultiplier)
    {
        grenadeDamageMultiplier = Mathf.Max(1f, damageMultiplier);
        grenadeRadiusMultiplier = Mathf.Max(1f, radiusMultiplier);
    }

    public void SetProgressionMultipliers(float damageMultiplier, float attackSpeedMultiplier)
    {
        progressionDamage = Mathf.Max(1f, damageMultiplier);
        progressionAttackSpeed = Mathf.Max(1f, attackSpeedMultiplier);
        if (baseWeapon != null) ApplyWeaponStats();
    }
private float currentFireRate;
private int currentPelletCount;
private float currentSpread;


   float timer;

    void Start()
    {
        movement = GetComponent<PlayMovement>();
        hero = GetComponent<HeroLocomotion>();

        if (weapons != null && weapons.Length > 0)
            baseWeapon = weapons[Mathf.Clamp(startWeaponIndex, 0, weapons.Length - 1)];
        if (baseWeapon == null || !baseWeapon.IsUsable || firePoint == null)
        {
            Debug.LogError("AutoShoter requires a weapon, bullet prefab and fire point.", this);
            enabled = false;
            return;
        }
        ApplyWeaponStats();
        ConfigureSpecial();
    }

    private SpecialWeapons special;
    public WeaponElementAttack ElementAttack { get; private set; }

    private void ConfigureSpecial()
    {
        ElementAttack = new WeaponElementAttack(startWeaponIndex);
        if (special == null) special = GetComponent<SpecialWeapons>();
        if (special == null) special = gameObject.AddComponent<SpecialWeapons>();
        special.Configure(this, baseWeapon);
        special.SetUnits(0, 0, 0, droneDamageMultiplier, turretDamageMultiplier);
    }

    // Юниты из улучшений боя: дрон, турель и мины работают рядом с любым основным оружием.
    // Настройки каждого юнита берутся из записи оружия того же вида в weapons (её нельзя выбрать перед боем).
    private SpecialWeapons supDrone, supTurret, supMine;
    private float supTurretTimer, supMineTimer;
    private int unitDrones, unitTurrets, unitMines;
    private float droneDamageMultiplier = 1f, turretDamageMultiplier = 1f;

    public void SetSupportUnits(int drones, int turrets, int mines, float droneDamage, float turretDamage)
    {
        unitDrones = drones;
        unitTurrets = turrets;
        unitMines = mines;
        droneDamageMultiplier = droneDamage;
        turretDamageMultiplier = turretDamage;
        if (special != null) special.SetUnits(0, 0, 0, droneDamage, turretDamage);
        UpdateUnit(WeaponKind.Drone, ref supDrone);
        UpdateUnit(WeaponKind.Turret, ref supTurret);
        UpdateUnit(WeaponKind.Mines, ref supMine);
    }

    // Компонент юнита создаётся при первом ранге; дальше только обновляются числа.
    private void UpdateUnit(WeaponKind kind, ref SpecialWeapons slot)
    {
        int count = kind == WeaponKind.Drone ? unitDrones : kind == WeaponKind.Turret ? unitTurrets : unitMines;
        if (slot == null)
        {
            if (count <= 0) return;
            WeaponData data = weapons != null ? System.Array.Find(weapons, w => w != null && w.kind == kind) : null;
            if (data == null) return;
            slot = gameObject.AddComponent<SpecialWeapons>();
            slot.support = true;
            slot.Configure(this, data);
        }
        slot.SetUnits(unitDrones, unitTurrets, unitMines, droneDamageMultiplier, turretDamageMultiplier);
    }

    // Турель и мины ставятся раз в fireRate своей записи, когда рядом есть враг (дроны летают сами).
    private void TickSupportUnits()
    {
        TickUnit(supTurret, ref supTurretTimer, 5f);
        TickUnit(supMine, ref supMineTimer, 8f);
    }

    private void TickUnit(SpecialWeapons unit, ref float unitTimer, float minRange)
    {
        if (unit == null) return;
        unitTimer += Time.deltaTime;
        if (unitTimer < Mathf.Max(0.5f, unit.Weapon.fireRate)) return;
        Transform target = WeaponTargets.Pick(transform.position, Mathf.Max(minRange, unit.Weapon.range), TargetRule.Nearest);
        if (target == null) return;
        unitTimer = 0f;
        unit.Fire(RollDamage(), target);
    }

    public bool SelectWeapon(int index)
    {
        if (weapons == null || index < 0 || index >= weapons.Length ||
            weapons[index] == null || !weapons[index].IsUsable || firePoint == null)
            return false;

        startWeaponIndex = index;
        ModelSkins.ApplyHeroWeapon(transform, ModelSkins.WeaponKey(weapons[index], index));
        baseWeapon = weapons[index];
        currentWeapon = baseWeapon;
        ApplyWeaponStats();
        ConfigureSpecial();
        timer = 0f;
        lastGrenadeTime = Time.time;
        enabled = true;
        return true;
    }

   void Update()
   {

    if (Time.timeScale == 0f) return;
    timer += Time.deltaTime;
    TickSupportUnits();
    bool isSpecial = baseWeapon != null && baseWeapon.kind != WeaponKind.Standard;
    Transform target = isSpecial ? WeaponTargets.Pick(transform.position, range, baseWeapon.targeting) : FindClosestEnemy();
    if (movement != null)
        movement.SetAimDirection(target != null ? target.position - transform.position : Vector3.zero);
    if (target != null)
    {
        Vector3 dir = (target.position - transform.position);
        dir.y = 0;

        if (movement == null && dir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                targetRotation,
                10f * Time.deltaTime
            );
        }

        if (timer >= currentFireRate)
        {
            if (movement != null) movement.FaceNow(dir);
            if (isSpecial) special.Fire(RollDamage(), target);
            else Shoot(target);
            timer = 0f;
        }
    }
        if (Time.time - lastGrenadeTime >= grenadeCooldown)
            ThrowGrenade();
   }

        private float inspectorRange;

        void ApplyWeaponStats()
{
    // Older scene weapon entries have no range configured (serialized as zero).
    // Preserve the shooter's Inspector range for those entries.
    if (inspectorRange <= 0f) inspectorRange = Mathf.Max(0.1f, range);
    bool ordinary = baseWeapon.kind == WeaponKind.Standard;
    range = (baseWeapon.range > 0f ? baseWeapon.range : inspectorRange) * (ordinary ? CombatStats.RangeMultiplier : 1f);
    currentDamage = Mathf.Max(1, Mathf.RoundToInt(baseWeapon.damage * progressionDamage));
    currentFireRate = Mathf.Max(0.03f, baseWeapon.fireRate / progressionAttackSpeed);
    currentPelletCount = baseWeapon.pelletCount + (ordinary ? CombatStats.ExtraPellets : 0);
    currentSpread = baseWeapon.spreadAngle;
    // A single-bullet weapon needs some spread, otherwise the extra bullets fly on top of each other.
    if (ordinary && CombatStats.ExtraPellets > 0) currentSpread = Mathf.Max(currentSpread, 8f + 6f * CombatStats.ExtraPellets);
}

    // Exact damage with upgrades (for statistics) and a per-hit roll that keeps the fractional part:
    // 7.2 deals 7 most of the time and 8 one hit in five, so the average equals the displayed value.
    public float ExactDamage => Mathf.Max(1f, baseWeapon != null ? baseWeapon.damage * progressionDamage : currentDamage);

    public int RollDamage()
    {
        float exact = ExactDamage;
        int whole = Mathf.FloorToInt(exact);
        return Mathf.Max(1, whole + (Random.value < exact - whole ? 1 : 0));
    }


    private void ThrowGrenade()
    {
        if (hero != null && hero.GrenadeWindupBusy)
            return;
        Transform target = grenadePrefab != null && GrenadeUnlocked ? FindClosestEnemy() : null;
        if (target == null)
        {
            lastGrenadeTime = Time.time;
            return;
        }

        Vector3 aim = target.position;
        if (hero != null && hero.HasGrenadeAnimation)
        {
            hero.TryBeginThrow(hand => LaunchGrenade(hand, target != null ? target.position : aim, true));
            return;
        }
        LaunchGrenade(transform.position + Vector3.up * 3f, aim, false);
    }

    private void LaunchGrenade(Vector3 origin, Vector3 aim, bool fromHand)
    {
        if (!isActiveAndEnabled || !GrenadeUnlocked || grenadePrefab == null)
            return;

        lastGrenadeTime = Time.time;
        GameObject grenade = Instantiate(grenadePrefab, origin, Quaternion.identity);
        if (fromHand && grenade.TryGetComponent(out Collider grenadeCollider))
            foreach (Collider ownerCollider in GetComponents<Collider>())
                Physics.IgnoreCollision(grenadeCollider, ownerCollider);

        if (grenade.TryGetComponent(out Rigidbody grenadeBody))
        {
            Vector3 direction = aim - origin;
            if (fromHand) direction.y = 0f;
            grenadeBody.AddForce(direction.normalized * 8f + Vector3.up * 5f, ForceMode.Impulse);
        }
        if (grenade.TryGetComponent(out Grenade settings))
        {
            settings.damage = CurrentGrenadeDamage;
            settings.explosionRadius = CurrentGrenadeRadius;
        }
    }

   Transform FindClosestEnemy()
{
    GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

    float minDist = Mathf.Infinity;
    Transform closest = null;

    foreach (GameObject enemy in enemies)
    {
        float dist = Vector3.Distance(transform.position, enemy.transform.position);

        if (dist < minDist && dist <= range)
        {
            minDist = dist;
            closest = enemy.transform;
        }
    }

    return closest;
}


    // Дуло модели оружия, если она есть; иначе прежний firePoint.
    Vector3 ShotOrigin { get { return ModelSkins.TryGetMuzzle(out Vector3 m) ? m : firePoint.position; } }

    void Shoot(Transform target)
{
    Sfx shotSound = baseWeapon.pelletCount > 1 || baseWeapon.WeaponType == WeaponType.Shotgun
        ? Sfx.Shotgun
        : baseWeapon.useAutomaticShotSound ? Sfx.AutoShot : Sfx.Shot;
    GameAudio.Play(shotSound, shotSound == Sfx.Shotgun ? 0.8f : 0.6f);
    // Медленные стволы и дробовик отскакивают сильнее, скорострельные — едва заметно.
    float kick = shotSound == Sfx.Shotgun ? 0.45f : Mathf.Clamp(baseWeapon.fireRate * 0.6f, 0.12f, 0.4f);
    PlayWeaponRecoil(kick, true);
    Vector3 origin = ShotOrigin;
    Vector3 dir = (target.position - origin).normalized;
    Quaternion baseRotation = Quaternion.LookRotation(dir);

    if (currentPelletCount <= 1)
    {
        GameObject bullet = Instantiate(
            baseWeapon.bulletPrefab,
            origin,
            baseRotation
        );

        Bullet b = bullet.GetComponent<Bullet>();
        if (b != null)
        {
            b.SetOwner(transform);
            b.elementAttack = ElementAttack;
            b.pierce = CombatStats.Pierce;
            b.ricochet = CombatStats.Ricochet;
            b.damage = RollDamage();
            b.speed = baseWeapon.bulletSpeed;
            b.lifeTime = baseWeapon.bulletLifeTime * CombatStats.RangeMultiplier;
        }

        return;
    }

    float step = currentSpread / (currentPelletCount - 1);

    for (int i = 0; i < currentPelletCount; i++)
    {
        float angle = -currentSpread / 2f + step * i;
        Quaternion rotation = baseRotation * Quaternion.Euler(0f, angle, 0f);

        GameObject bullet = Instantiate(
            baseWeapon.bulletPrefab,
            origin,
            rotation
        );

        Bullet b = bullet.GetComponent<Bullet>();
        if (b != null)
        {
            b.SetOwner(transform);
            b.elementAttack = ElementAttack;
            b.pierce = CombatStats.Pierce;
            b.ricochet = CombatStats.Ricochet;
            b.damage = RollDamage();
            b.speed = baseWeapon.bulletSpeed;
            b.lifeTime = baseWeapon.bulletLifeTime * CombatStats.RangeMultiplier;
        }
    }
}

    // kick 0..1 — отскок модели оружия в руке (WeaponFireAnimation), smoke — дымок у дула. 0 — только наклон руки.
    public void PlayWeaponRecoil(float kick = 0.3f, bool smoke = true)
    {
        if (hero != null) hero.Recoil();
        WeaponFireAnimation fx = ModelSkins.CurrentWeaponFx;
        if (fx != null && kick > 0f) fx.PlayLight(kick, smoke);
    }
}



