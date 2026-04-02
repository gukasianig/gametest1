using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoShoter : MonoBehaviour
{
   public Transform firePoint;
   public GameObject bulletPrefab;
   public float fireRate = 0.5f;
   public float range = 20f;

   float timer;

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

        if (timer >= fireRate)
        {
            Shoot(target);
            timer = 0f;
        }
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
    if (firePoint == null || bulletPrefab == null) return;

    Vector3 targetPos = target.position;
    targetPos.y = firePoint.position.y;
    Vector3 dir = (targetPos - firePoint.position).normalized;
    //dir.y = 0;

    GameObject bullet = Instantiate(
        bulletPrefab,
        firePoint.position,
        Quaternion.LookRotation(dir)
    );

    Bullet b = bullet.GetComponent<Bullet>();
    if (b != null)
    {
        b.SetTarget(target);
    }
}
}

   

