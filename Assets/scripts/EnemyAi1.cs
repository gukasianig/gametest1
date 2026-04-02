using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public Transform target;
    public float speed = 3f;

    void Update()
    {
        if (target == null) return;

        Vector3 direction = (target.position - transform.position).normalized;

        direction.y = 0;

        transform.position += direction * speed * Time.deltaTime;

        // поворот к игроку
        if (direction != Vector3.zero)
        {
            Quaternion rot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Lerp(transform.rotation, rot, 10f * Time.deltaTime);
        }
    }
}