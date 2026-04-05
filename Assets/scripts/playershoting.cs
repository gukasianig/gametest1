using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    public GameObject bulletPrefab;
    public Transform firePoint;
    public int damage = 20;

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // ЛКМ
        {
            Shoot();
        }
    }

    void Shoot()
    {
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

    Bullet b = bullet.GetComponent<Bullet>();
    if (b != null)
    {
        b.SetDamage(damage);
    }
    }
}