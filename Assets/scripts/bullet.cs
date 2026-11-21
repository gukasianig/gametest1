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
    // Бонусы героя, выпустившего пулю (взрывные пули); null — игрок 1.
    public HeroStats stats;
    private HeroStats Stats => stats ?? CombatStats.Primary;
    // Extra enemies this bullet may pass through (from the Pierce upgrade).
    public int pierce;
    // Револьвер, ранг 3: последний выстрел барабана всегда критует.
    public bool forceCrit;
    // How many times this bullet may jump to another enemy after a hit (Ricochet upgrade); each jump keeps 80% damage.
    public int ricochet;
    private static readonly System.Collections.Generic.List<Health> ricochetTargets = new System.Collections.Generic.List<Health>();
    // Карточка «Разделение пули»: после первого попадания — осколки из этого префаба (у осколков 0).
    public int split;
    public GameObject splitPrefab;
    // Связка «Критический пробой»: сколько раз крит ещё может добавить пробивание.
    private int critPierceLeft = 2;

    private void Split(Health hit)
    {
        if (split <= 0 || splitPrefab == null || rb == null) return;
        Vector3 forward = rb.velocity;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return;
        forward.Normalize();
        int count = split;
        split = 0;
        float spread = 70f;
        for (int i = 0; i < count; i++)
        {
            float angle = count > 1 ? -spread * 0.5f + spread * i / (count - 1) : 0f;
            Quaternion rotation = Quaternion.LookRotation(Quaternion.Euler(0f, angle, 0f) * forward);
            Bullet fragment = Spawn(splitPrefab, transform.position + rotation * Vector3.forward * 0.35f, rotation);
            if (fragment == null) continue;
            GameObject piece = fragment.gameObject;
            fragment.SetOwner(owner);
            fragment.elementAttack = elementAttack;
            fragment.stats = stats;
            fragment.damage = Mathf.Max(1, Mathf.RoundToInt(damage * HeroCardEffects.SplitShare));
            fragment.speed = speed;
            fragment.lifeTime = Mathf.Max(0.15f, lifeTime * 0.35f);
            fragment.pierced.Add(hit);
            piece.transform.localScale *= 0.7f;
            fragment.Launch();
        }
    }

    // Explosive bullets (upgrade): a small blast around the hit that damages the other enemies nearby.
    private void Explode(Health hit)
    {
        float radius = Stats.ExplosionRadius;
        int blast = Mathf.Max(1, Mathf.RoundToInt(damage * HeroStats.ExplosionDamageShare));
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

    private static readonly System.Collections.Generic.List<Collider> ownColliders = new System.Collections.Generic.List<Collider>();
    private static readonly System.Collections.Generic.List<Collider> ownerColliders = new System.Collections.Generic.List<Collider>();

    public void SetOwner(Transform shooter)
    {
        owner = shooter;
        if (owner == null) return;
        // Списки-буферы вместо массивов: SetOwner вызывается на каждый выстрел (пул пуль, 20.11.2026).
        GetComponentsInChildren(ownColliders);
        owner.GetComponentsInChildren(ownerColliders);
        foreach (Collider projectileCollider in ownColliders)
        foreach (Collider ownerCollider in ownerColliders)
            Physics.IgnoreCollision(projectileCollider, ownerCollider);
        ownColliders.Clear();
        ownerColliders.Clear();
    }

    // ---- Пул пуль (20.11.2026): вместо Instantiate/Destroy на каждый выстрел и осколок. ----
    // Ключ — префаб (у оружий могут быть разные пулефабы). Пуля, созданная напрямую Instantiate (проверки,
    // старый PlayerShooting), в пул не входит и по-прежнему уничтожается.
    private sealed class Pool
    {
        public Bullet template;
        public readonly System.Collections.Generic.Queue<Bullet> idle = new System.Collections.Generic.Queue<Bullet>();
    }
    private static readonly System.Collections.Generic.Dictionary<GameObject, Pool> pools = new System.Collections.Generic.Dictionary<GameObject, Pool>();
    private const int MaxIdlePerPrefab = 256;

    private Pool pool;
    private bool idleInPool;
    private int releasedFrame = -1;
    private bool launched;
    // Исходные рендереры префаба (до модели ProjectileModels) и их материалы/цвета следа — для сброса после золотой пули.
    private Renderer[] baseRenderers;
    private Material[][] baseMaterials;
    private Color[] trailStart, trailEnd;
    // Модель пули: Fit компенсирует масштаб хоста, при повторном выпуске компенсацию пересчитываем под новый масштаб.
    private Transform model;
    private Vector3 modelScale, modelPosition, skinHostScale;
    private bool skinned;

    public System.Collections.Generic.IReadOnlyList<Renderer> BaseRenderers => baseRenderers;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitPools()
    {
        pools.Clear();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // Пули — объекты сцены: после смены/перезагрузки сцены в очередях остались бы уничтоженные ссылки.
        if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single) pools.Clear();
    }

    // Выдаёт пулю из пула (или новую) в позиции, со сброшенным состоянием префаба. Вызывающий настраивает поля и зовёт Launch().
    public static Bullet Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;
        if (!pools.TryGetValue(prefab, out Pool p))
        {
            Bullet template = prefab.GetComponent<Bullet>();
            if (template == null) return null;
            p = new Pool { template = template };
            pools[prefab] = p;
        }
        Bullet bullet = null;
        // Отпущенная в этом кадре пуля ещё может получить отложенные события физики — берём только «остывшие».
        while (p.idle.Count > 0)
        {
            Bullet candidate = p.idle.Peek();
            if (candidate == null) { p.idle.Dequeue(); continue; }
            if (candidate.releasedFrame == Time.frameCount) break;
            bullet = p.idle.Dequeue();
            break;
        }
        if (bullet == null)
        {
            bullet = Instantiate(prefab, position, rotation).GetComponent<Bullet>();
            bullet.pool = p;
            bullet.ResetState(p.template, prefab.transform.localScale);
            return bullet;
        }
        bullet.idleInPool = false;
        bullet.transform.SetPositionAndRotation(position, rotation);
        bullet.ResetState(p.template, prefab.transform.localScale);
        bullet.gameObject.SetActive(true);
        if (bullet.rb != null)
        {
            bullet.rb.position = position;
            bullet.rb.rotation = rotation;
            bullet.rb.velocity = Vector3.zero;
            bullet.rb.angularVelocity = Vector3.zero;
        }
        foreach (Renderer r in bullet.baseRenderers)
            if (r is TrailRenderer trail) trail.Clear();
        return bullet;
    }

    // Все поля, которые выставляют SpawnBullet/Split/карточки, — к значениям префаба; материалы и масштаб — исходные.
    private void ResetState(Bullet template, Vector3 scale)
    {
        damage = template.damage;
        speed = template.speed;
        lifeTime = template.lifeTime;
        elementAttack = template.elementAttack;
        stats = template.stats;
        pierce = template.pierce;
        forceCrit = template.forceCrit;
        ricochet = template.ricochet;
        split = template.split;
        splitPrefab = template.splitPrefab;
        critPierceLeft = 2;
        hasHit = false;
        owner = null;
        launched = false;
        expireAt = float.MaxValue;
        pierced.Clear();
        transform.localScale = scale;
        for (int i = 0; i < baseRenderers.Length; i++)
        {
            Renderer r = baseRenderers[i];
            if (r == null) continue;
            if (r is TrailRenderer trail)
            {
                trail.startColor = trailStart[i];
                trail.endColor = trailEnd[i];
            }
            else r.sharedMaterials = baseMaterials[i];
        }
    }

    // Старт полёта: модель, скорость и срок жизни. Для пуль из пула вызывается явно после настройки полей,
    // для созданных напрямую — из Start.
    public void Launch()
    {
        launched = true;
        if (!skinned)
        {
            skinned = true;
            if (ProjectileModels.Skin(gameObject, "bullet", AutoShoter.BulletLength)) // было 0,7 → 0,4 → 0,25 м по просьбе владельца
            {
                model = transform.Find("ProjectileModel");
                if (model != null)
                {
                    modelScale = model.localScale;
                    modelPosition = model.localPosition;
                    skinHostScale = transform.lossyScale;
                }
            }
        }
        else if (model != null)
        {
            Vector3 now = transform.lossyScale;
            Vector3 k = new Vector3(Ratio(skinHostScale.x, now.x), Ratio(skinHostScale.y, now.y), Ratio(skinHostScale.z, now.z));
            model.localScale = Vector3.Scale(modelScale, k);
            model.localPosition = Vector3.Scale(modelPosition, k);
        }
        if (rb != null) rb.velocity = transform.forward * speed;
        // Срок жизни считается вручную: рикошет продлевает его до новой цели (иначе короткая пуля гаснет в пути).
        expireAt = Time.time + lifeTime;
    }

    private static float Ratio(float a, float b) { return Mathf.Abs(b) < 0.0001f ? 1f : a / b; }

    // Конец пули: в пул (если пришла оттуда) или уничтожение.
    public void Release()
    {
        hasHit = true;
        if (idleInPool) return;
        if (pool == null || pool.idle.Count >= MaxIdlePerPrefab || !pools.ContainsValue(pool))
        {
            Destroy(gameObject);
            return;
        }
        idleInPool = true;
        releasedFrame = Time.frameCount;
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        gameObject.SetActive(false);
        pool.idle.Enqueue(this);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        baseRenderers = GetComponentsInChildren<Renderer>(true);
        baseMaterials = new Material[baseRenderers.Length][];
        trailStart = new Color[baseRenderers.Length];
        trailEnd = new Color[baseRenderers.Length];
        for (int i = 0; i < baseRenderers.Length; i++)
        {
            if (baseRenderers[i] is TrailRenderer trail) { trailStart[i] = trail.startColor; trailEnd[i] = trail.endColor; }
            else baseMaterials[i] = baseRenderers[i].sharedMaterials;
        }
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
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (!launched) Launch();
    }

    private float expireAt = float.MaxValue;

    
    public void SetDamage(int dmg)
    {
        damage = dmg;
    }

    
    
    void Update()
    {
        if (!launched) Launch(); // страховка: пулю из пула выдали без Launch()
        if (Time.time >= expireAt) Release();
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
        Release();
        return;
    }

    IDamagable dmg = other.GetComponentInParent<IDamagable>();

    if (other.GetComponent<Bullet>() != null) return;
    if (dmg != null)
    {
        Health health = other.GetComponentInParent<Health>();
        // One enemy with several colliders must not take the same bullet twice.
        if (health != null && !pierced.Add(health)) return;
        bool crit = false;
        if (elementAttack != null && health != null && health.CompareTag("Enemy")) crit = elementAttack.Hit(health, damage, forceCrit);
        else dmg.TakeDamage(damage);
        bool enemy = health != null && health.CompareTag("Enemy");
        if (enemy && Stats.ExplosionRadius > 0f) Explode(health);
        if (enemy && split > 0) Split(health);
        if (enemy && crit && Stats.CritPierce && critPierceLeft > 0) { critPierceLeft--; pierce++; }
        // Пробивание и рикошет вместе (09.11.2026): раньше пробивание тратилось первым, и если за врагом было пусто,
        // пуля улетала и рикошет не срабатывал (особенно вплотную). Теперь сквозь — только если впереди есть ещё враг,
        // иначе рикошет к ближайшему; пробивание без цели впереди — последний вариант.
        if (pierce > 0 && enemy && EnemyAhead(health)) { pierce--; return; }
        if (ricochet > 0 && enemy && Redirect()) { ricochet--; return; }
        if (pierce > 0 && enemy) { pierce--; return; }
        Release();
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
