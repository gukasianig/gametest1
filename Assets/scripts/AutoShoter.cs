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
   
   private int currentDamage;
private float currentFireRate;
private int currentPelletCount;
private float currentSpread;
   

   float timer;

    void Start()
    {
        baseWeapon = weapons[startWeaponIndex];
        ApplyWeaponStats();
    }

   void Update()
   {
    
    timer += Time.deltaTime;
    Transform target = FindClosestEnemy();
    if (target != null)
    {
        Vector3 dir = (target.position - transform.position);
        dir.y = 0;

        if (dir != Vector3.zero)
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
            Shoot(target);
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
    currentDamage = baseWeapon.damage;
    currentFireRate = baseWeapon.fireRate;
    currentPelletCount = baseWeapon.pelletCount;
    currentSpread = baseWeapon.spreadAngle;
}


        void ThrowGrenade()
        {
             Transform target = FindClosestEnemy();
            if (target == null) return;

            Vector3 spawnPos = transform.position + Vector3.up * 3f;

            GameObject grenade = Instantiate(grenadePrefab, spawnPos, Quaternion.identity);

            Rigidbody rb = grenade.GetComponent<Rigidbody>();
            Debug.Log(rb);

            if (rb != null)
                {
                    Vector3 targetPos = target.position + Vector3.up * 0.5f;
                    Vector3 direction = (target.position - spawnPos).normalized;

        // делаем "дугу"
                    Vector3 throwForce = direction * 8f + Vector3.up * 5f;

                    rb.AddForce(throwForce, ForceMode.Impulse);
                }

    Grenade g = grenade.GetComponent<Grenade>();
    // if (g != null)
    // {
    //     g.damage = grenadeDamage;
    //     g.explosionRadius = grenadeRadius;
        
    // }
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
    for (int i = 0; i < currentPelletCount; i++)
    {
        float angle = Random.Range(-currentSpread, currentSpread);

       
        Vector3 dir = (target.position - firePoint.position).normalized;
        Quaternion baseRotation = Quaternion.LookRotation(dir);

         
        Quaternion rotation = baseRotation * Quaternion.Euler(0, angle, 0);

        GameObject bullet = Instantiate(
            baseWeapon.bulletPrefab,
            firePoint.position,
            rotation
        );

        Bullet b = bullet.GetComponent<Bullet>();
        if (b != null)
        {
            b.damage = currentDamage;
            b.speed = baseWeapon.bulletSpeed;
            b.lifeTime = baseWeapon.bulletLifeTime;
        }

        Debug.Log("SHOOT");
    }
}
}

   

