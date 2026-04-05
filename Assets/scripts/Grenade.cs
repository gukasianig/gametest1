using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grenade : MonoBehaviour
{
    public float explosionRadius = 3f;
    public int damage = 20;
    public GameObject explosionEffect;

    public float delay = 2f;

    void Start()
    {
        Invoke(nameof(Explode), delay);
    }

    void Explode()
    {
        if (explosionEffect != null)
    {
        GameObject effect = Instantiate(
            explosionEffect,
            transform.position,
            Quaternion.identity
        );

        // размер = радиус
        effect.transform.localScale = new Vector3(
    explosionRadius,
    explosionRadius,
    explosionRadius
);

        Destroy(effect, 0.5f);
    }

    Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);

    foreach (Collider col in hits)
    {
        if (col.CompareTag("Enemy"))
        {
            Health hp = col.GetComponent<Health>();
            if (hp != null)
            {
                hp.TakeDamage(damage);
            }
        }
    }

    Destroy(gameObject);
    }
    void Update()
    {
        transform.Rotate(300 * Time.deltaTime, 0, 0);
    }
}

