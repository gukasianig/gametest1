using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public float speed = 3f;
    public float attackDistance = 1.5f;
    public int damage = 10;
    public float attackCooldown = 1f;
    public int maxHP = 50;
    public float stopDistance = 1.5f;
    public float separationRadius = 1.2f;
    public float separationStrength = 2f;
    public float attackRange = 1.5f;
    public Transform target;
    float lastAttackTime = 0f;

    

    void Update()
    {
        if (target == null) return;

    Vector3 toPlayer = target.position - transform.position;
    toPlayer.y = 0f;

    float distance = toPlayer.magnitude;

    Vector3 moveDir = Vector3.zero;

    if (distance > stopDistance)
    {
        moveDir += toPlayer.normalized;
    }

    Collider[] nearby = Physics.OverlapSphere(transform.position, separationRadius);

    Vector3 separation = Vector3.zero;

    foreach (Collider col in nearby)
    {
        if (col.gameObject == gameObject) continue;
        if (!col.CompareTag("Enemy")) continue;

        Vector3 away = transform.position - col.transform.position;
        away.y = 0f;

        float dist = away.magnitude;
        if (dist > 0.001f)
        {
            separation += away.normalized / dist;
        }
    }

    moveDir += separation * separationStrength;
    moveDir.y = 0f;

    if (moveDir != Vector3.zero)
    {
        moveDir.Normalize();

        transform.position += moveDir * speed * Time.deltaTime;

        Quaternion rot = Quaternion.LookRotation(moveDir);
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            rot,
            10f * Time.deltaTime
        );
    }

    

    if (distance <= attackRange)
    {
        Attack();
    }
    }

    void Attack()
    {
        if (Time.time - lastAttackTime < attackCooldown)
        return;

    lastAttackTime = Time.time;

    PlayerHealth player = target.GetComponent<PlayerHealth>();
    if (player != null)
    {
        player.TakeDamage(damage);
    }
    }
}