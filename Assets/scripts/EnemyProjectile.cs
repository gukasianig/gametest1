using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public Transform target;
    public Vector3 direction;
    public int damage = 12;
    public float speed = 8f;
    private float age;

    public static EnemyProjectile Create(Vector3 position, Vector3 direction, Transform target, int damage)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        root.name = "Hostile shot";
        root.transform.position = new Vector3(position.x, 0.9f, position.z);
        root.transform.localScale = Vector3.one * 0.4f;
        root.GetComponent<Collider>().enabled = false;
        Destroy(root.GetComponent<Collider>());
        ArenaLayout.Instance.Tint(root, new Color(1f, 0.52f, 0.15f));
        EnemyProjectile shot = root.AddComponent<EnemyProjectile>();
        shot.direction = direction.normalized;
        shot.target = target;
        shot.damage = damage;
        return shot;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (target == null || GameSession.Instance == null || GameSession.Instance.IsFinished) { Destroy(gameObject); return; }
        Vector3 start = transform.position;
        Vector3 end = start + direction * speed * Time.deltaTime;
        age += Time.deltaTime;
        if (age > 7f || !ArenaLayout.Instance.IsWalkable(end, 0.15f) ||
            !ArenaLayout.Instance.PathClear(start, end, 0.15f)) { Destroy(gameObject); return; }
        Vector3 segment = end - start;
        float t = Mathf.Clamp01(Vector3.Dot(target.position - start, segment) / Mathf.Max(segment.sqrMagnitude, 0.00001f));
        if (Vector3.Distance(start + segment * t, target.position) < 0.75f)
        {
            target.GetComponent<PlayerHealth>().TakeDamage(damage);
            Destroy(gameObject);
            return;
        }
        transform.position = end;
    }
}
