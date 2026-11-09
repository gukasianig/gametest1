using UnityEngine;

public class Bullet : MonoBehaviour, IProjectile
{
    
    public int damage = 20;
    public float speed;
    public float lifeTime;
    bool hasHit = false;

    private Rigidbody rb;
    private Transform owner;
    public WeaponElementAttack elementAttack;
    // Extra enemies this bullet may pass through (from the Pierce upgrade).
    public int pierce;
    // How many times this bullet may jump to another enemy after a hit (Ricochet upgrade); each jump keeps 80% damage.
    public int ricochet;
    private static readonly System.Collections.Generic.List<Health> ricochetTargets = new System.Collections.Generic.List<Health>();

    // Explosive bullets (upgrade): a small blast around the hit that damages the other enemies nearby.
    private void Explode(Health hit)
    {
        float radius = CombatStats.ExplosionRadius;
        int blast = Mathf.Max(1, Mathf.RoundToInt(damage * CombatStats.ExplosionDamageShare));
        WeaponTargets.Collect(ricochetTargets);
        foreach (Health candidate in ricochetTargets)
        {
            if (candidate == null || candidate == hit || candidate.isDead) continue;
            if (WeaponTargets.Flat(transform.position, candidate.transform.position) <= radius + WeaponTargets.BodyRadius(candidate) * 0.5f)
                candidate.TakeDamage(blast);
        }
        // Взрывная пуля: лёгкий вариант взрыва без тряски (их бывает много).
        Vector3 blastPoint = transform.position;
        EnemyFx.Explosion(new Vector3(blastPoint.x, GroundHeight.At(blastPoint), blastPoint.z), radius * 0.7f, 0f, false);
    }

    private bool Redirect()
    {
        WeaponTargets.Collect(ricochetTargets);
        Health best = null;
        float bestDistance = 12f;
        foreach (Health candidate in ricochetTargets)
        {
            if (candidate == null || candidate.isDead || pierced.Contains(candidate)) continue;
            float distance = WeaponTargets.Flat(transform.position, candidate.transform.position);
            if (distance < bestDistance) { bestDistance = distance; best = candidate; }
        }
        if (best == null || rb == null) return false;
        Vector3 direction = best.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return false;
        direction.Normalize();
        transform.rotation = Quaternion.LookRotation(direction);
        rb.velocity = direction * speed;
        expireAt = Mathf.Max(expireAt, Time.time + bestDistance / Mathf.Max(1f, speed) + 0.3f);
        damage = Mathf.Max(1, Mathf.RoundToInt(damage * 0.8f));
        return true;
    }
    private readonly System.Collections.Generic.HashSet<Health> pierced = new System.Collections.Generic.HashSet<Health>();

    public void SetOwner(Transform shooter)
    {
        owner = shooter;
        if (owner == null) return;
        foreach (Collider projectileCollider in GetComponentsInChildren<Collider>())
        foreach (Collider ownerCollider in owner.GetComponentsInChildren<Collider>())
            Physics.IgnoreCollision(projectileCollider, ownerCollider);
    }

    private bool IsFriendly(Collider other)
    {
        if (owner != null && (other.transform == owner || other.transform.IsChildOf(owner))) return true;
        // Child hitboxes need the same protection as the tagged player root.
        for (Transform node = other.transform; node != null; node = node.parent)
            if (node.CompareTag("Player")) return true;
        return other.GetComponentInParent<PlayerHealth>() != null;
    }
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        ProjectileModels.Skin(gameObject, "bullet", AutoShoter.BulletLength); // было 0,7 → 0,4 → 0,25 м по просьбе владельца
        rb.velocity = transform.forward * speed;
        // Срок жизни считается вручную: рикошет продлевает его до новой цели (иначе короткая пуля гаснет в пути).
        expireAt = Time.time + lifeTime;
    }

    private float expireAt = float.MaxValue;

    
    public void SetDamage(int dmg)
    {
        damage = dmg;
    }

    
    
    void Update()
    {
        if (Time.time >= expireAt) Destroy(gameObject);
    }

    // Есть ли ещё враг впереди по ходу пули (узкий конус, до 5 м): только тогда пробивание полезнее рикошета.
    private bool EnemyAhead(Health hit)
    {
        if (rb == null) return false;
        Vector3 forward = rb.velocity;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return false;
        forward.Normalize();
        WeaponTargets.Collect(ricochetTargets);
        foreach (Health candidate in ricochetTargets)
        {
            if (candidate == null || candidate == hit || candidate.isDead || pierced.Contains(candidate)) continue;
            Vector3 to = candidate.transform.position - transform.position;
            to.y = 0f;
            float along = Vector3.Dot(to, forward);
            if (along <= 0f || along > 5f) continue;
            float side = (to - forward * along).magnitude;
            if (side <= 0.6f + WeaponTargets.BodyRadius(candidate) * 0.5f) return true;
        }
        return false;
    }

    void OnTriggerEnter(Collider other)
{
    if (hasHit) return;
    if (IsFriendly(other)) return;
    if (ArenaLayout.Instance != null && ArenaLayout.Instance.IsBlocker(other))
    {
        hasHit = true;
        Destroy(gameObject);
        return;
    }

    IDamagable dmg = other.GetComponentInParent<IDamagable>();

    if (other.GetComponent<Bullet>() != null) return;
    if (dmg != null)
    {
        Health health = other.GetComponentInParent<Health>();
        // One enemy with several colliders must not take the same bullet twice.
        if (health != null && !pierced.Add(health)) return;
        if (elementAttack != null && health != null && health.CompareTag("Enemy")) elementAttack.Hit(health, damage);
        else dmg.TakeDamage(damage);
        bool enemy = health != null && health.CompareTag("Enemy");
        if (enemy && CombatStats.ExplosionRadius > 0f) Explode(health);
        // Пробивание и рикошет вместе (09.11.2026): раньше пробивание тратилось первым, и если за врагом было пусто,
        // пуля улетала и рикошет не срабатывал (особенно вплотную). Теперь сквозь — только если впереди есть ещё враг,
        // иначе рикошет к ближайшему; пробивание без цели впереди — последний вариант.
        if (pierce > 0 && enemy && EnemyAhead(health)) { pierce--; return; }
        if (ricochet > 0 && enemy && Redirect()) { ricochet--; return; }
        if (pierce > 0 && enemy) { pierce--; return; }
        hasHit = true;
        Destroy(gameObject);
    }
}

    public void OnHit(Transform target)
    {
        // IDamagable h = target.GetComponentInParent<IDamagable>();

        // if (h != null)
        // {
        //     h.TakeDamage(damage);
        // }
    }
}
