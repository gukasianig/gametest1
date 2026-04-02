using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public float speed = 3f;
    public float attackDistance = 1.5f;
    public int damage = 10;
    public float attackCooldown = 1f;

    public Transform target;
    float lastAttackTime = 0f;

    void Start()
    {
        //target = GameObject.FindGameObjectWithTag("target").transform;
    }

    void Update()
    {
        if (target == null) return;

        float dist = Vector3.Distance(transform.position, target.position);

        // движение к игроку
        if (dist > attackDistance)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                target.position,
                speed * Time.deltaTime
            );
        }
        else
        {
            Attack();
        }
    }

    void Attack()
    {
        if (Time.time - lastAttackTime < attackCooldown) return;

        PlayerHealth ph = target.GetComponent<PlayerHealth>();

        if (ph != null)
        {
            ph.TakeDamage(damage);
            Debug.Log("Enemy hit player");
        }

        lastAttackTime = Time.time;
    }
}