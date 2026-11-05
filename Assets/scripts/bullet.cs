using UnityEngine;

public class Bullet : MonoBehaviour, IProjectile
{
    
    public int damage = 20;
    public float speed;
    public float lifeTime;
    bool hasHit = false;

    private Rigidbody rb;
    private Transform owner;

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
        rb.velocity = transform.forward * speed;
        
        Destroy(gameObject, lifeTime);
    }

    
    public void SetDamage(int dmg)
    {
        damage = dmg;
    }

    
    
    void Update()
    {
      
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
       hasHit = true;
        // A Shielder's front blocks most of a bullet; grenades and orbiting weapons are not affected.
        EnemyAbility ability = other.GetComponentInParent<EnemyAbility>();
        int applied = ability != null && ability.BlocksShot(transform.position) ?
            Mathf.Max(1, Mathf.RoundToInt(damage * EnemyAbility.ShieldPassFraction)) : damage;
        dmg.TakeDamage(applied);
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
