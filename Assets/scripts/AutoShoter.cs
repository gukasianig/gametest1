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

    private void ConfigureSpecial()
    {
        if (special == null) special = GetComponent<SpecialWeapons>();
        if (special == null) special = gameObject.AddComponent<SpecialWeapons>();
        special.Configure(this, baseWeapon);
    }

    public bool SelectWeapon(int index)
    {
        if (weapons == null || index < 0 || index >= weapons.Length ||
            weapons[index] == null || !weapons[index].IsUsable || firePoint == null)
            return false;

        startWeaponIndex = index;
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
            if (isSpecial) special.Fire(RollDamage(), target);
            else Shoot(target);
            timer = 0f;
        }
    }
        if (Time.time - lastGrenadeTime >= grenadeCooldown)
        {
            ThrowGrenade();
            lastGrenadeTime = Time.time;
        }
   }

        void ApplyWeaponStats()
{
    // Older scene weapon entries have no range configured (serialized as zero).
    // Preserve the shooter's Inspector range for those entries.
    range = baseWeapon.range > 0f ? baseWeapon.range : Mathf.Max(0.1f, range);
    currentDamage = Mathf.Max(1, Mathf.RoundToInt(baseWeapon.damage * progressionDamage));
    currentFireRate = Mathf.Max(0.03f, baseWeapon.fireRate / progressionAttackSpeed);
    currentPelletCount = baseWeapon.pelletCount;
    currentSpread = baseWeapon.spreadAngle;
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


        void ThrowGrenade()
        {
             if (grenadePrefab == null) return;
             Transform target = FindClosestEnemy();
            if (target == null) return;

            Vector3 spawnPos = transform.position + Vector3.up * 3f;

            GameObject grenade = Instantiate(grenadePrefab, spawnPos, Quaternion.identity);

            Rigidbody rb = grenade.GetComponent<Rigidbody>();


            if (rb != null)
                {
                    Vector3 targetPos = target.position + Vector3.up * 0.5f;
                    Vector3 direction = (target.position - spawnPos).normalized;

        // делаем "дугу"
                    Vector3 throwForce = direction * 8f + Vector3.up * 5f;

                    rb.AddForce(throwForce, ForceMode.Impulse);
                }

    Grenade g = grenade.GetComponent<Grenade>();
    if (g != null)
    {
        g.damage = CurrentGrenadeDamage;
        g.explosionRadius = CurrentGrenadeRadius;
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


    void Shoot(Transform target)
{
    Vector3 dir = (target.position - firePoint.position).normalized;
    Quaternion baseRotation = Quaternion.LookRotation(dir);

    if (currentPelletCount <= 1)
    {
        GameObject bullet = Instantiate(
            baseWeapon.bulletPrefab,
            firePoint.position,
            baseRotation
        );

        Bullet b = bullet.GetComponent<Bullet>();
        if (b != null)
        {
            b.SetOwner(transform);
            b.damage = RollDamage();
            b.speed = baseWeapon.bulletSpeed;
            b.lifeTime = baseWeapon.bulletLifeTime;
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
            firePoint.position,
            rotation
        );

        Bullet b = bullet.GetComponent<Bullet>();
        if (b != null)
        {
            b.SetOwner(transform);
            b.damage = RollDamage();
            b.speed = baseWeapon.bulletSpeed;
            b.lifeTime = baseWeapon.bulletLifeTime;
        }
    }
}
}



