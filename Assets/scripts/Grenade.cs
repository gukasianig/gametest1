using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grenade : MonoBehaviour
{
    public const float VisualLength = .32f;
    public float explosionRadius = 3f;
    public int damage = 20;
    public GameObject explosionEffect;

    public float delay = 2f;

    void Start()
    {
        ProjectileModels.Skin(gameObject, "grenade", VisualLength);
        Invoke(nameof(Explode), delay);
    }

    void Explode()
    {
        GameAudio.Play(Sfx.Explosion);
        // Общий эффект взрыва вместо старого explosionEffect (растянутой сферы); поле оставлено ради сцены.
        Vector3 point = transform.position;
        EnemyFx.Explosion(new Vector3(point.x, GroundHeight.At(point), point.z), explosionRadius * 0.8f, 0.5f);

    Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);

    var damagedTargets = new HashSet<Health>();
    foreach (Collider col in hits)
    {
        Health hp = col.GetComponentInParent<Health>();
        if (hp != null && hp.CompareTag("Enemy") && damagedTargets.Add(hp))
        {
            hp.TakeDamage(damage);
        }
    }

    Destroy(gameObject);
    }
    void Update()
    {
        transform.Rotate(300 * Time.deltaTime, 0, 0);
    }
}

