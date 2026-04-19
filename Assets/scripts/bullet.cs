using UnityEngine;

public class Bullet : MonoBehaviour, IProjectile
{
    
    public int damage = 20;
    public float speed;
    public float lifeTime;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private Transform target;
    public void SetDamage(int dmg)
    {
        damage = dmg;
    }

    public void SetTarget(Transform t)
    {
        target = t;
    }
    
    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        // 🔥 берём центр врага
        Collider col = target.GetComponentInChildren<Collider>();
        Vector3 targetPos = col != null ? col.bounds.center : target.position;

        Vector3 dir = (targetPos - transform.position).normalized;

        transform.position += dir * speed * Time.deltaTime;

        // 🔥 попадание по дистанции
        if (Vector3.Distance(transform.position, targetPos) < 0.5f)
        {
            OnHit(target);
            Destroy(gameObject);
        }
    }

    public void OnHit(Transform target)
    {
        IDamagable h = target.GetComponentInParent<IDamagable>();

        if (h != null)
        {
            h.TakeDamage(new Damage(damage));
        }
    }
}