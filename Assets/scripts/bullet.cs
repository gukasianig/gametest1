using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 10f;
    public int damage = 10;

    private Transform target;

    public void SetTarget(Transform t)
    {
        target = t;
    }

    void Update()
    {
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
            Health h = target.GetComponentInParent<Health>();

            if (h != null)
            {
                h.TakeDamage(damage);
            }

            Destroy(gameObject);
        }
    }
}