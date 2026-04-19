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
        Invoke(nameof(  ), delay);
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
        float size = explosionRadius * 2f;
        effect.transform.localScale = new Vector3(
        
            size,
            size,
            size
        );


        Destroy(effect, 0.5f);
    }

    Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);

    foreach (Collider col in hits)
    {
        if (col.CompareTag("Enemy"))
        {
            IDamagable hp = col.GetComponent<IDamagable>();
            if (hp != null)
            {
                hp.TakeDamage(new Damage(damage));
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

