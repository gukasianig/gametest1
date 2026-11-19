using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoShoter : MonoBehaviour
{
    // Бонусы своего героя (кооператив: у каждого свой набор, CombatStats.Of).
    private HeroStats heroStats;
    public HeroStats Stats => heroStats ?? (heroStats = CombatStats.Of(this));
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
    private Transform rightAimTarget, leftAimTarget, pendingAimedShot;
    public Transform RightAimTarget => rightAimTarget;
    public Transform LeftAimTarget => leftAimTarget;
    public bool DualAiming => branch == WeaponElement.Dual && hero != null && hero.CanAimWeapons;
    private bool PoseAiming => hero != null && hero.CanAimWeapons && baseWeapon != null && baseWeapon.kind == WeaponKind.Standard;

    private bool ValidAimTarget(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy || Vector3.Distance(transform.position, target.position) > range) return false;
        Health targetHealth = target.GetComponentInParent<Health>();
        return targetHealth == null || !targetHealth.isDead;
    }

    private Vector3 UpdateDualAim(Transform candidate)
    {
        if (!ValidAimTarget(rightAimTarget)) rightAimTarget = candidate;
        if (!ValidAimTarget(leftAimTarget) || leftAimTarget == rightAimTarget)
            leftAimTarget = rightAimTarget != null ? OtherTarget(rightAimTarget) : null;
        if (rightAimTarget == null) { leftAimTarget = null; return Vector3.zero; }
        Vector3 right = Vector3.ProjectOnPlane(rightAimTarget.position - transform.position, Vector3.up).normalized;
        Vector3 left = leftAimTarget != null ? Vector3.ProjectOnPlane(leftAimTarget.position - transform.position, Vector3.up).normalized : right;
        Vector3 facing = right + left;
        // Opposite targets have no bisector. Choose the hemisphere closest to the current stance.
        if (facing.sqrMagnitude < .04f)
        {
            facing = Vector3.Cross(Vector3.up, right);
            if (Vector3.Dot(facing, transform.forward) < 0) facing = -facing;
        }
        facing.Normalize();
        if (leftAimTarget != null && Vector3.Dot(Vector3.Cross(Vector3.up, facing), right - left) < -.1f)
        {
            Transform swap = rightAimTarget; rightAimTarget = leftAimTarget; leftAimTarget = swap;
        }
        return facing;
    }

    // Called explicitly by HeroLocomotion after the animation and arm pose have been evaluated.
    public void FlushAimedShot()
    {
        Transform target = pendingAimedShot;
        pendingAimedShot = null;
        if (!isActiveAndEnabled || !PoseAiming || !ValidAimTarget(target) || Time.timeScale <= 0 || (ownHealth != null && ownHealth.IsDead)) return;
        Shoot(target);
        timer = 0f;
    }
    public int CurrentDamage => currentDamage;
    public float CurrentFireInterval => (branch == WeaponElement.Minigun
        ? currentFireRate * Mathf.Lerp(1.65f, WeaponElements.MinigunFastInterval(branchRank), machineSpin)
        : currentFireRate) / ActiveFireFactor;
    public float MachineSpin => machineSpin;
    private HeroAbilities abilities;
    private float ActiveFireFactor => (abilities != null ? abilities : abilities = GetComponent<HeroAbilities>()) != null ? abilities.FireRateFactor : 1f;
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
        ownHealth = GetComponent<PlayerHealth>();

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
    private PlayerHealth ownHealth;
    public WeaponElementAttack ElementAttack { get; private set; }

    private void ConfigureSpecial()
    {
        ElementAttack = new WeaponElementAttack(startWeaponIndex, Stats);
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
        unit.Fire(RollDamage(false), target);
    }

    public bool SelectWeapon(int index)
    {
        if (weapons == null || index < 0 || index >= weapons.Length ||
            weapons[index] == null || !weapons[index].IsUsable || firePoint == null)
            return false;

        startWeaponIndex = index;
        Evolved = false;
        ResetBranchCycle();
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
    pendingAimedShot = null;
    if (Time.timeScale == 0f) return;
    if (CoopMode.Active && ownHealth != null && ownHealth.IsDead) return;
    timer += Time.deltaTime;
    TickSupportUnits();
    bool isSpecial = baseWeapon != null && baseWeapon.kind != WeaponKind.Standard;
    Transform target = isSpecial ? WeaponTargets.Pick(transform.position, range, baseWeapon.targeting) : FindClosestEnemy();
    Vector3 facing = target != null ? target.position - transform.position : Vector3.zero;
    if (DualAiming)
    {
        facing = UpdateDualAim(target);
        target = rightAimTarget;
        hero.SetCombatAim(target, leftAimTarget ?? target, facing);
    }
    else
    {
        rightAimTarget = leftAimTarget = null;
        if (hero != null) hero.SetCombatAim(PoseAiming ? target : null, null, PoseAiming ? facing : Vector3.zero);
    }
    TickRifleTarget(target);
    if (movement != null)
        movement.SetAimDirection(facing);
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

        if (timer >= CurrentFireInterval && Time.time >= reloadUntil)
        {
            if (PoseAiming) { pendingAimedShot = target; }
            else
            {
            if (movement != null) movement.FaceNow(dir);
            if (isSpecial) special.Fire(RollDamage(), target);
            else Shoot(target);
            timer = 0f;
            }
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
    range = (baseWeapon.range > 0f ? baseWeapon.range : inspectorRange) * (ordinary ? Stats.RangeMultiplier : 1f);
    currentDamage = Mathf.Max(1, Mathf.RoundToInt(baseWeapon.damage * WeaponTuning.Multiplier(startWeaponIndex) * progressionDamage * EvolutionDamage));
    currentFireRate = Mathf.Max(0.03f, baseWeapon.fireRate / progressionAttackSpeed / (Evolved ? Evo.rate : 1f));
    currentPelletCount = baseWeapon.pelletCount + (ordinary ? Stats.ExtraPellets : 0) + (Evolved ? Evo.pellets : 0);
    currentSpread = baseWeapon.spreadAngle + (Evolved ? Evo.spread : 0f);
    if (Evolved) range *= Evo.range;
    // Ветка пистолета из мастерской (12.11.2026): Револьвер / Двойные / Липкий.
    WeaponElement previousBranch = branch;
    branch = ordinary && WeaponElements.HasBranches(startWeaponIndex) ? WeaponElements.Equipped(startWeaponIndex) : WeaponElement.None;
    branchRank = branch == WeaponElement.None ? 0 : WeaponElements.Rank(startWeaponIndex, branch);
    branchDamage = 1f;
    rangeScale = 1f;
    if (previousBranch != branch) ResetBranchCycle();
    if (branch == WeaponElement.Revolver)
    {
        branchDamage = WeaponElements.RevolverDamage(branchRank);
        rangeScale = WeaponElements.RevolverRange(branchRank);
        range *= rangeScale;
        currentFireRate *= WeaponElements.RevolverInterval(branchRank);
        currentDamage = Mathf.Max(1, Mathf.RoundToInt(currentDamage * branchDamage));
    }
    else if (branch == WeaponElement.Dual) currentFireRate *= WeaponElements.DualInterval / WeaponElements.DualRate(branchRank);
    else if (WeaponElements.IsRifleBranch(branch))
    {
        branchDamage = WeaponElements.RifleDamage(branch, branchRank);
        if (branch == WeaponElement.Assault)
        {
            currentFireRate *= .5f;
            rangeScale = branchRank >= 2 ? 1.15f : 1f;
        }
        else if (branch == WeaponElement.Heavy)
        {
            currentFireRate *= 2.8f;
            rangeScale = branchRank >= 2 ? 1.3f : 1.2f;
        }
        range *= rangeScale;
        currentDamage = Mathf.Max(1, Mathf.RoundToInt(currentDamage * branchDamage));
    }
    ModelSkins.SetOffhandWeapon(transform, branch == WeaponElement.Dual);
    // A single-bullet weapon needs some spread, otherwise the extra bullets fly on top of each other.
    int extra = currentPelletCount - baseWeapon.pelletCount;
    if (ordinary && extra > 0) currentSpread = Mathf.Max(currentSpread, 8f + 6f * extra);
}

    // ---------------- Эволюция обычного оружия (11.11.2026) ----------------
    // Решение владельца: выпадает только из сундука, когда у героя «Урон пуль» ранг 5+ и парная карточка оружия
    // (PlayerProgression.ReadyEvolution); 4 обычных оружия; та же модель с золотым свечением и золотые пули.
    public struct EvolutionData
    {
        public WeaponKind kind;        // Standard — обычное оружие (номер записи = номер оружия 0–3), иначе вид спецоружия
        public string title, text;
        public UpgradeEffect partner;
        public int partnerRanks, pellets, pierce;
        public float damage, rate, range, spread;
        public bool everyFourthDouble;
    }

    public static readonly EvolutionData[] Evolutions =
    {
        new EvolutionData { title = GameText.Source("autoshoter.048"), partner = UpgradeEffect.CritChance, partnerRanks = 3, pellets = 1, damage = 1.3f, rate = 1f, range = 1f,
            spread = 6f, everyFourthDouble = true, text = GameText.Source("autoshoter.049") },
        new EvolutionData { title = GameText.Source("autoshoter.050"), partner = UpgradeEffect.AttackSpeed, partnerRanks = 3, pierce = 1, damage = 1f, rate = 1.5f, range = 1f,
            text = GameText.Source("autoshoter.051") },
        new EvolutionData { title = GameText.Source("autoshoter.052"), partner = UpgradeEffect.ExtraPellets, partnerRanks = 2, pellets = 3, damage = 1.15f, rate = 1f, range = 1f,
            spread = 10f, text = GameText.Source("autoshoter.053") },
        new EvolutionData { title = GameText.Source("autoshoter.054"), partner = UpgradeEffect.Pierce, partnerRanks = 2, pierce = 3, damage = 1.5f, rate = 1f, range = 1.2f,
            text = GameText.Source("autoshoter.055") },
        // Спецоружие (11.11.2026): поведение меняет SpecialWeapons.Fire по shooter.Evolved.
        new EvolutionData { kind = WeaponKind.Boomerang, title = GameText.Source("autoshoter.056"), partner = UpgradeEffect.CritChance, partnerRanks = 3, damage = 1.25f,
            rate = 1f, range = 1f, text = GameText.Source("autoshoter.057") },
        new EvolutionData { kind = WeaponKind.Rocket, title = GameText.Source("autoshoter.058"), partner = UpgradeEffect.AttackSpeed, partnerRanks = 3, damage = 1f,
            rate = 1f, range = 1f, text = GameText.Source("autoshoter.059") },
        new EvolutionData { kind = WeaponKind.Arc, title = GameText.Source("autoshoter.060"), partner = UpgradeEffect.CritChance, partnerRanks = 3, damage = 1.1f,
            rate = 1f, range = 1f, text = GameText.Source("autoshoter.061") },
        new EvolutionData { kind = WeaponKind.Flame, title = GameText.Source("autoshoter.062"), partner = UpgradeEffect.AttackSpeed, partnerRanks = 3, damage = 1.1f,
            rate = 1f, range = 1f, text = GameText.Source("autoshoter.063") },
        new EvolutionData { kind = WeaponKind.Ice, title = GameText.Source("autoshoter.064"), partner = UpgradeEffect.AttackSpeed, partnerRanks = 3, damage = 1f,
            rate = 1f, range = 1f, text = GameText.Source("autoshoter.065") },
        new EvolutionData { kind = WeaponKind.Rail, title = GameText.Source("autoshoter.066"), partner = UpgradeEffect.EliteDamage, partnerRanks = 2, damage = 1.6f,
            rate = 1f, range = 1f, text = GameText.Source("autoshoter.067") },
    };

    public int WeaponIndex => startWeaponIndex;
    public bool Evolved { get; private set; }

    // Номер записи эволюции текущего оружия (-1 — у этого оружия эволюции нет: дрон, турель, мины).
    public int EvolutionSlot
    {
        get
        {
            if (baseWeapon == null) return -1;
            for (int i = 0; i < Evolutions.Length; i++)
            {
                if (baseWeapon.kind == WeaponKind.Standard ? Evolutions[i].kind == WeaponKind.Standard && i == startWeaponIndex
                    : Evolutions[i].kind == baseWeapon.kind) return i;
            }
            return -1;
        }
    }

    private EvolutionData Evo => Evolutions[Mathf.Max(0, EvolutionSlot)];
    private float EvolutionDamage => Evolved ? Evo.damage : 1f;
    public bool CanEvolve => !Evolved && EvolutionSlot >= 0;
    private int evolvedShots;
    private static readonly Color Gold = new Color(1f, 0.78f, 0.25f);
    private static Material goldBullet;

    public void Evolve()
    {
        if (!CanEvolve) return;
        Evolved = true;
        ApplyWeaponStats();
        GlowWeapon();
        Vector3 feet = transform.position;
        feet.y = GroundHeight.At(feet) + 0.05f;
        EnemyFx.Ring(feet, 3.2f, Gold, 0.7f, 0.4f);
        EnemyFx.LightFlash(feet + Vector3.up * 1.6f, Gold, 9f, 4f, 0.6f);
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI / 5f;
            EnemyFx.Puff(feet + new Vector3(Mathf.Cos(a) * 0.8f, 0.5f, Mathf.Sin(a) * 0.8f), Gold, 0.18f, 0.9f, Vector3.up * 2.6f);
        }
        GameAudio.Play(Sfx.LevelUp, 1f, 0f);
        if (CombatFeel.Instance != null) CombatFeel.Instance.Flash(Gold, 0.35f);
        if (GameSession.Instance != null) GameSession.Instance.Announce(GameText.Source("autoshoter.068") + Evo.title, 3f);
    }

    // Модель в руке получает золотое свечение (свои копии материалов — у напарника свечения нет).
    private void GlowWeapon()
    {
        GlowWeaponAt(ModelSkins.FindMuzzle(transform));
        GlowWeaponAt(ModelSkins.FindOffhandMuzzle(transform));
    }

    private void GlowWeaponAt(Transform point)
    {
        Transform skin = point != null ? point.parent : null;
        if (skin == null) return;
        foreach (Renderer r in skin.GetComponentsInChildren<Renderer>())
            foreach (Material m in r.materials)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Gold * 0.55f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
    }

    // Золотые пули эволюции; big — усиленный выстрел Дуэлянта.
    private static void TintBullet(GameObject bullet, bool big)
    {
        foreach (Renderer r in bullet.GetComponentsInChildren<Renderer>())
        {
            if (r is TrailRenderer trail)
            {
                trail.startColor = Gold;
                trail.endColor = new Color(Gold.r, Gold.g, Gold.b, 0f);
                continue;
            }
            if (goldBullet == null)
            {
                goldBullet = new Material(r.sharedMaterial);
                goldBullet.color = Gold;
                goldBullet.EnableKeyword("_EMISSION");
                goldBullet.SetColor("_EmissionColor", Gold * 1.4f);
            }
            r.sharedMaterial = goldBullet;
        }
        if (big) bullet.transform.localScale *= 1.6f;
    }

    // Exact damage with upgrades (for statistics) and a per-hit roll that keeps the fractional part:
    // 7.2 deals 7 most of the time and 8 one hit in five, so the average equals the displayed value.
    public float ExactDamage => Mathf.Max(1f, baseWeapon != null ? baseWeapon.damage * WeaponTuning.Multiplier(startWeaponIndex) * progressionDamage * EvolutionDamage * branchDamage : currentDamage);

    // Ветка пистолета из мастерской: Револьвер (барабан), Двойные (второй ствол), Липкий (заряд — в WeaponElementAttack).
    private WeaponElement branch;
    private int branchRank;
    private float branchDamage = 1f, rangeScale = 1f, reloadUntil;
    private int revolverShots;
    private int assaultShots;
    private float machineSpin;
    private int machineTargetId;

    private void ResetBranchCycle()
    {
        revolverShots = 0;
        assaultShots = 0;
        reloadUntil = 0f;
        machineSpin = 0f;
        machineTargetId = 0;
    }

    private void TickRifleTarget(Transform target)
    {
        if (branch != WeaponElement.Minigun) return;
        int targetId = target != null ? target.GetInstanceID() : 0;
        if (targetId != 0 && machineTargetId != 0 && targetId != machineTargetId) machineSpin *= .5f;
        machineTargetId = targetId;
        machineSpin = Mathf.Clamp01(machineSpin + Time.deltaTime * (target != null ? .5f : -1f));
    }

    private Transform OtherTarget(Transform first)
    {
        Transform best = null;
        float bestDistance = range;
        foreach (GameObject enemy in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            if (enemy.transform == first || enemy.transform.IsChildOf(first) || !ValidAimTarget(enemy.transform)) continue;
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance < bestDistance) { bestDistance = distance; best = enemy.transform; }
        }
        return best;
    }

    private void SpawnBullet(Vector3 origin, Quaternion rotation, float damageScale, bool lastRound, bool doubleShot)
    {
        GameObject bullet = Instantiate(baseWeapon.bulletPrefab, origin, rotation);
        Bullet b = bullet.GetComponent<Bullet>();
        if (b == null) return;
        bool finisher = lastRound && branchRank >= 3;
        b.SetOwner(transform);
        b.elementAttack = ElementAttack;
        b.stats = Stats;
        b.pierce = Stats.Pierce + (Evolved ? Evo.pierce : 0) + (finisher ? 3 : 0) +
            (branch == WeaponElement.Dual && branchRank >= 3 ? 1 : 0) +
            (branch == WeaponElement.Heavy ? branchRank : 0);
        b.forceCrit = finisher;
        b.ricochet = Stats.Ricochet;
        b.damage = Mathf.Max(1, Mathf.RoundToInt(RollDamage() * (doubleShot ? 2 : 1) * damageScale));
        if (Evolved) TintBullet(bullet, doubleShot);
        b.speed = baseWeapon.bulletSpeed * (branch == WeaponElement.Heavy ? 1.25f : 1f);
        b.lifeTime = baseWeapon.bulletLifeTime * Stats.RangeMultiplier * rangeScale;
    }

    public int RollDamage(bool includeTuning = true)
    {
        float exact = ExactDamage / (includeTuning ? 1f : WeaponTuning.Multiplier(startWeaponIndex));
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
        if (!ValidAimTarget(enemy.transform)) continue;
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
    Vector3 ShotOrigin { get { return TryGetMuzzle(out Vector3 m) ? m : firePoint.position; } }

    // Дуло модели оружия этого героя (кооператив: у каждого своё), кэш до смены модели.
    private Transform muzzle;
    public bool TryGetMuzzle(out Vector3 position)
    {
        if (muzzle == null || !muzzle.gameObject.activeInHierarchy) muzzle = ModelSkins.FindMuzzle(transform);
        position = muzzle != null ? muzzle.position : Vector3.zero;
        return muzzle != null;
    }
    // Длина модели пули (bullet.cs, ProjectileModels.Skin): 09.11.2026 уменьшена 0,4 → 0,25 м по просьбе владельца.
    public const float BulletLength = 0.25f;

    void Shoot(Transform target)
{
    Transform other = branch == WeaponElement.Dual ? (ValidAimTarget(leftAimTarget) ? leftAimTarget : OtherTarget(target)) ?? target : null;
    if (PoseAiming) hero.AlignWeapons(target, other, true);
    Sfx shotSound = baseWeapon.pelletCount > 1 || baseWeapon.WeaponType == WeaponType.Shotgun
        ? Sfx.Shotgun
        : baseWeapon.useAutomaticShotSound && branch != WeaponElement.Heavy ? Sfx.AutoShot : Sfx.Shot;
    GameAudio.Play(shotSound, shotSound == Sfx.Shotgun ? 0.8f : 0.6f);
    // Медленные стволы и дробовик отскакивают сильнее, скорострельные — едва заметно.
    float kick = shotSound == Sfx.Shotgun ? 0.45f : Mathf.Clamp(baseWeapon.fireRate * 0.6f, 0.12f, 0.4f);
    if (branch == WeaponElement.Heavy) kick = .4f;
    PlayWeaponRecoil(kick, true);
    Vector3 origin = ShotOrigin;
    // Эволюция: золотые искры у дула; у Дуэлянта каждый 4-й выстрел — двойной урон и крупная пуля.
    bool doubleShot = Evolved && Evo.everyFourthDouble && ++evolvedShots % 4 == 0;
    if (Evolved) EnemyFx.Puff(origin, Gold, doubleShot ? 0.16f : 0.08f, 0.2f, Vector3.up * 0.6f);
    // Цель — центр тела врага, а не его опорная точка у земли: иначе пули шли от дула вниз под углом и казалось,
    // что они вылетают не из ствола (09.11.2026). Пуля появляется на срезе ствола, а не серединой внутри оружия.
    // Револьвер: барабан на 6 выстрелов, затем перезарядка; последний выстрел — «финишный» (ранг 3: крит и пробивание).
    bool lastRound = false;
    if (branch == WeaponElement.Revolver && ++revolverShots >= WeaponElements.RevolverBarrel)
    {
        lastRound = true;
        revolverShots = 0;
        reloadUntil = Time.time + currentFireRate + WeaponElements.RevolverReload(branchRank);
        EnemyFx.Puff(origin, Gold, 0.22f, 0.35f, Vector3.up * 0.8f);
        GameAudio.Play(Sfx.Deploy, 0.4f);
    }

    float volleyDamage = 1f;
    if (branch == WeaponElement.Assault && ++assaultShots >= 3)
    {
        assaultShots = 0;
        reloadUntil = Time.time + currentFireRate * 5.2f;
        if (branchRank >= 3) volleyDamage = 1.5f;
    }
    FireVolley(target, origin, lastRound, doubleShot, volleyDamage);
    if (branch == WeaponElement.Dual && (hero == null || hero.OffhandAvailable))
    {
        Transform leftMuzzle = ModelSkins.FindOffhandMuzzle(transform);
        Vector3 leftOrigin = leftMuzzle != null ? leftMuzzle.position : origin - transform.right * 0.45f;
        FireVolley(other, leftOrigin, false, doubleShot);
        WeaponFireAnimation leftFx = leftMuzzle != null ? leftMuzzle.GetComponentInParent<WeaponFireAnimation>() : null;
        if (leftFx != null) leftFx.PlayLight(kick, true);
        if (hero != null) hero.Recoil(true);
    }
}

    private void FireVolley(Transform target, Vector3 origin, bool lastRound, bool doubleShot, float damageScale = 1f)
    {
        Collider body = target.GetComponentInChildren<Collider>();
        Vector3 aim = body != null ? body.bounds.center : target.position + Vector3.up * 0.6f;
        Vector3 direction = (aim - origin).normalized;
        Quaternion rotation = Quaternion.LookRotation(direction);
        origin += direction * BulletLength * 0.5f;
        int count = Mathf.Max(1, currentPelletCount);
        float spread = currentSpread * (branch == WeaponElement.Assault ? .55f : 1f);
        float step = count > 1 ? spread / (count - 1) : 0f;
        for (int i = 0; i < count; i++)
        {
            float angle = count > 1 ? -spread * 0.5f + step * i : 0f;
            SpawnBullet(origin, rotation * Quaternion.Euler(0f, angle, 0f), damageScale, lastRound, doubleShot);
        }
    }

    // kick 0..1 — отскок модели оружия в руке (WeaponFireAnimation), smoke — дымок у дула. 0 — только наклон руки.
    public void PlayWeaponRecoil(float kick = 0.3f, bool smoke = true)
    {
        if (hero != null) hero.Recoil();
        WeaponFireAnimation fx = WeaponFx;
        if (fx != null && kick > 0f) fx.PlayLight(kick, smoke);
    }

    // Анимация выстрела модели оружия в руке именно этого героя (в кооперативе героев двое,
    // а ModelSkins.CurrentWeaponFx помнит только последнюю установленную модель).
    private WeaponFireAnimation weaponFx;
    public WeaponFireAnimation WeaponFx
    {
        get
        {
            if (weaponFx == null || !weaponFx.gameObject.activeInHierarchy) weaponFx = GetComponentInChildren<WeaponFireAnimation>();
            return weaponFx;
        }
    }
}



