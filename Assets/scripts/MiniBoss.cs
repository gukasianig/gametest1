using System.Collections;
using UnityEngine;

// Мини-босс «Громила» (11.11.2026, решение владельца): великан из модели врага-танка на середине забега (5-я минута).
// Медленно идёт к ближайшему герою и по очереди: удар по земле (красный круг 1,1 с) и рывок (полоса 0,8 с).
// Награда — гарантированный малый сундук (в нём может быть эволюция оружия) и кольцо кристаллов. Полоса HP — у GameSession.
public class MiniBoss : MonoBehaviour
{
    public const float SpawnShare = 0.5f;          // доля времени забега до появления
    public const int BaseHealth = 2000;
    public const float Scale = 2.4f;
    public const float SlamRadius = 5f;
    public const int SlamDamage = 25, ChargeDamage = 22;
    public static readonly string Title = GameText.Source("miniboss.190");

    public static MiniBoss Active { get; private set; }
    public Health Health { get; private set; }
    public bool Telegraphing { get; private set; }
    public int Attacks { get; private set; }

    private EnemyAI ai;
    private LineRenderer warning;
    private static Material warningMaterial;

    private void Awake()
    {
        Active = this;
        Health = GetComponent<Health>();
        ai = GetComponent<EnemyAI>();
        GameObject line = new GameObject("Mini boss warning", typeof(LineRenderer));
        warning = line.GetComponent<LineRenderer>();
        warning.useWorldSpace = true;
        if (warningMaterial == null)
        {
            warningMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            warningMaterial.color = new Color(1f, 0.25f, 0.15f);
        }
        warning.sharedMaterial = warningMaterial;
        warning.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        warning.enabled = false;
        if (Health != null) Health.Died += Died;
    }

    private void Start() { StartCoroutine(Loop()); }

    private void OnDestroy()
    {
        if (Active == this) Active = null;
        if (warning != null) Destroy(warning.gameObject);
    }

    private bool Running => Health != null && !Health.isDead && GameSession.Instance != null &&
        GameSession.Instance.State == GameRunState.Playing && Time.timeScale > 0f;

    private IEnumerator Loop()
    {
        yield return new WaitForSeconds(2.5f);
        while (Health != null && !Health.isDead)
        {
            while (!Running) yield return null;
            yield return Slam();
            yield return new WaitForSeconds(3.5f);
            while (!Running) yield return null;
            yield return Charge();
            yield return new WaitForSeconds(3.5f);
        }
    }

    private Vector3 Ground(Vector3 p) { return new Vector3(p.x, GroundHeight.At(p) + 0.08f, p.z); }

    // Удар по земле: стоит, красный круг вокруг себя, затем урон всем героям внутри, взрыв и тряска.
    private IEnumerator Slam()
    {
        if (ai != null) ai.MovementLocked = true;
        Vector3 centre = Ground(transform.position);
        Telegraphing = true;
        float left = 1.1f;
        while (left > 0f)
        {
            if (Running) left -= Time.deltaTime;
            // Круг пульсирует и толстеет к удару.
            Circle(centre, SlamRadius, Mathf.Lerp(0.12f, 0.45f, 1f - left / 1.1f));
            yield return null;
            if (Health == null || Health.isDead) { warning.enabled = false; yield break; }
        }
        warning.enabled = false;
        Telegraphing = false;
        Attacks++;
        CoopMode.DamageHeroesWithin(centre, SlamRadius + 0.3f, SlamDamage, HeroDamageCause.BossSlam);
        EnemyFx.Explosion(centre, SlamRadius * 0.8f, 0.7f, true);
        EnemyFx.Ring(centre, SlamRadius, new Color(1f, 0.55f, 0.25f), 0.45f, 0.45f);
        GameAudio.Play(Sfx.Explosion, 0.9f);
        ScreenShake.AddAt(centre, 0.5f);
        yield return new WaitForSeconds(0.35f);
        if (ai != null) ai.MovementLocked = false;
    }

    // Рывок: полоса к ближайшему герою, затем быстрый проход по ней; задетые герои получают урон один раз.
    private IEnumerator Charge()
    {
        Transform target = CoopMode.NearestAlive(transform.position);
        if (target == null) yield break;
        if (ai != null) ai.MovementLocked = true;
        Vector3 from = Ground(transform.position);
        Vector3 dir = target.position - from;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
        dir.Normalize();
        float length = Mathf.Clamp(Vector3.Distance(from, Ground(target.position)) + 3f, 6f, 14f);
        Telegraphing = true;
        float left = 0.8f;
        while (left > 0f)
        {
            if (Running) left -= Time.deltaTime;
            Line(from, from + dir * length, Mathf.Lerp(0.6f, 1.6f, 1f - left / 0.8f));
            transform.rotation = Quaternion.LookRotation(dir);
            yield return null;
            if (Health == null || Health.isDead) { warning.enabled = false; yield break; }
        }
        warning.enabled = false;
        Telegraphing = false;
        Attacks++;
        var hit = new System.Collections.Generic.HashSet<PlayerHealth>();
        float travelled = 0f;
        while (travelled < length && Health != null && !Health.isDead)
        {
            if (!Running) { yield return null; continue; }
            float step = 22f * Time.deltaTime;
            Vector3 next = transform.position + dir * step;
            if (ArenaLayout.Instance != null && !ArenaLayout.Instance.IsWalkable(next, 1.2f)) break;
            next.y = GroundHeight.At(next) + (transform.position.y - GroundHeight.At(transform.position));
            transform.position = next;
            travelled += step;
            foreach (PlayerHealth hero in CoopMode.Heroes)
                if (hero != null && !hero.IsDead && !hit.Contains(hero) && WeaponTargets.Flat(next, hero.transform.position) < 2f)
                {
                    hit.Add(hero);
                    hero.TakeDamage(ChargeDamage, HeroDamageCause.BossCharge);
                }
            if (Random.value < 0.5f) EnemyFx.Puff(Ground(next), new Color(0.7f, 0.62f, 0.48f), 0.5f, 0.5f, Vector3.up * 0.8f - dir);
            yield return null;
        }
        ScreenShake.AddAt(transform.position, 0.25f);
        yield return new WaitForSeconds(0.4f);
        if (ai != null) ai.MovementLocked = false;
    }

    private void Circle(Vector3 centre, float radius, float width)
    {
        warning.enabled = true;
        warning.loop = true;
        warning.positionCount = 64;
        warning.widthMultiplier = width;
        for (int i = 0; i < 64; i++)
        {
            float a = i * Mathf.PI * 2f / 64;
            Vector3 p = centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            p.y = GroundHeight.At(p) + 0.08f;
            warning.SetPosition(i, p);
        }
    }

    private void Line(Vector3 start, Vector3 end, float width)
    {
        warning.enabled = true;
        warning.loop = false;
        warning.positionCount = 2;
        warning.widthMultiplier = width;
        warning.SetPosition(0, start);
        warning.SetPosition(1, Ground(end));
    }

    // Награда: сундук, кольцо кристаллов, стоп-кадр и вспышка.
    private void Died(Health dead)
    {
        WorkshopProjects.RecordBoss(1);
        warning.enabled = false;
        Vector3 at = Ground(transform.position);
        SmallChest.Spawn(at);
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI / 6f;
            ExperiencePickup.Spawn(at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.4f, 15);
        }
        EnemyFx.Explosion(at, 4f, 0.8f, true);
        if (CombatFeel.Instance != null)
        {
            CombatFeel.Instance.Flash(new Color(1f, 0.7f, 0.35f), 0.45f);
            CombatFeel.Instance.HitStop(0.14f, 0.05f);
        }
        ScreenShake.AddAt(at, 0.8f);
        if (GameSession.Instance != null) GameSession.Instance.Announce(GameText.Source("miniboss.191"), 3f);
        if (Active == this) Active = null;
    }
}
