using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Values are stored in prefabs by number; 5 was a removed role, keep the explicit numbers.
public enum EnemyRole { Shooter, Charger, Bomber, Healer, Splitter, Summoner = 6, Sniper = 7, Mortar = 8, Teleporter = 9, Buffer = 10, Armored = 11 }

[RequireComponent(typeof(EnemyAI), typeof(Health))]
public class EnemyAbility : MonoBehaviour
{
    public EnemyRole role;
    // Splitter: the fragment prefab. Summoner: the minion prefab.
    public GameObject splitPrefab;
    public const int SummonerMinionLimit = 4;
    private readonly List<Health> minions = new List<Health>();
    public int LiveMinions { get { minions.RemoveAll(m => m == null || m.isDead); return minions.Count; } }
    private EnemyAI ai;
    private Health health;
    private LineRenderer warning;
    private Material warningMaterial, fillMaterial;
    private TelegraphFill fill;
    private Color warningColor;
    private bool acting;
    private float nextAction;
    public bool Telegraphing => warning != null && warning.enabled;
    public bool Ready => warning != null;
    public static Color RoleColor(EnemyRole value)
    {
        switch (value)
        {
            case EnemyRole.Shooter: return new Color(0.3f, 0.65f, 1f);
            case EnemyRole.Charger: return new Color(1f, 0.64f, 0.12f);
            case EnemyRole.Bomber: return new Color(1f, 0.2f, 0.3f);
            case EnemyRole.Healer: return new Color(0.2f, 0.95f, 0.55f);
            case EnemyRole.Summoner: return new Color(1f, 0.35f, 0.75f);
            case EnemyRole.Sniper: return new Color(0.95f, 0.95f, 0.3f);
            case EnemyRole.Mortar: return new Color(0.6f, 0.45f, 0.25f);
            case EnemyRole.Teleporter: return new Color(0.35f, 0.9f, 1f);
            case EnemyRole.Buffer: return new Color(1f, 0.55f, 0.15f);
            case EnemyRole.Armored: return new Color(0.55f, 0.6f, 0.68f);
            default: return new Color(0.75f, 0.4f, 1f);
        }
    }


    private void Awake()
    {
        ai = GetComponent<EnemyAI>();
        health = GetComponent<Health>();
        health.Died += OnDeath;
    }

    private void Start()
    {
        ai.UsesMelee = role == EnemyRole.Splitter;
        ai.PreferredRange = role == EnemyRole.Shooter ? 9f : role == EnemyRole.Healer ? 6f :
            role == EnemyRole.Summoner ? 8f : role == EnemyRole.Sniper ? 17f :
            role == EnemyRole.Mortar ? 15f : role == EnemyRole.Buffer ? 7f : 0f;
        ai.UsesMelee = role == EnemyRole.Splitter || role == EnemyRole.Teleporter || role == EnemyRole.Armored;
        if (role == EnemyRole.Armored) health.DamageTakenFactor = 0.5f;
        nextAction = Time.time + 1.5f;
        Color color = RoleColor(role);
        if (ArenaLayout.Instance != null) ArenaLayout.Instance.Tint(gameObject, color);
        GameObject line = new GameObject("Attack telegraph", typeof(LineRenderer));
        line.transform.SetParent(transform, false);
        warning = line.GetComponent<LineRenderer>();
        warning.useWorldSpace = true;
        warning.widthMultiplier = 0.12f;
        // Круги и линии предупреждений полупрозрачные (владелец 09.11.2026), обводка пульсирует, внутри растёт заливка.
        warningColor = color;
        warningMaterial = EnemyFx.SeeThrough(color, 0.5f);
        fillMaterial = EnemyFx.SeeThrough(color, 0.16f);
        warning.sharedMaterial = warningMaterial;
        warning.enabled = false;
        ClearFill();
        if (role == EnemyRole.Healer)
        {
            Part(PrimitiveType.Cube, new Vector3(0, 0.8f, 0), new Vector3(0.25f, 0.65f, 0.2f));
            Part(PrimitiveType.Cube, new Vector3(0, 0.8f, 0), new Vector3(0.65f, 0.25f, 0.2f));
        }
        else if (role == EnemyRole.Shooter)
            Part(PrimitiveType.Cylinder, new Vector3(0, 0.2f, 0.65f), new Vector3(0.22f, 0.5f, 0.22f)).localRotation = Quaternion.Euler(90, 0, 0);
        else if (role == EnemyRole.Charger)
        {
            Part(PrimitiveType.Cube, new Vector3(-0.35f, 0.4f, 0.3f), new Vector3(0.18f, 0.65f, 0.25f));
            Part(PrimitiveType.Cube, new Vector3(0.35f, 0.4f, 0.3f), new Vector3(0.18f, 0.65f, 0.25f));
        }
        else if (role == EnemyRole.Bomber)
            Part(PrimitiveType.Sphere, new Vector3(0, 0.65f, 0), Vector3.one * 0.5f);
        else if (role == EnemyRole.Summoner)
        {
            Part(PrimitiveType.Cube, new Vector3(-0.25f, 0.75f, 0), new Vector3(0.12f, 0.6f, 0.12f));
            Part(PrimitiveType.Cube, new Vector3(0.25f, 0.75f, 0), new Vector3(0.12f, 0.6f, 0.12f));
            Part(PrimitiveType.Sphere, new Vector3(0, 0.75f, 0), Vector3.one * 0.35f);
        }
        else if (role == EnemyRole.Sniper)
            Part(PrimitiveType.Cylinder, new Vector3(0, 0.25f, 1.0f), new Vector3(0.1f, 0.9f, 0.1f)).localRotation = Quaternion.Euler(90, 0, 0);
        else if (role == EnemyRole.Mortar)
            Part(PrimitiveType.Cylinder, new Vector3(0, 1.0f, 0.2f), new Vector3(0.35f, 0.5f, 0.35f)).localRotation = Quaternion.Euler(-30, 0, 0);
        else if (role == EnemyRole.Teleporter)
        {
            Part(PrimitiveType.Cube, new Vector3(0, 1.1f, 0), new Vector3(0.3f, 0.3f, 0.3f)).localRotation = Quaternion.Euler(45, 45, 0);
        }
        else if (role == EnemyRole.Buffer)
        {
            Part(PrimitiveType.Cylinder, new Vector3(0, 1.0f, 0), new Vector3(0.12f, 0.8f, 0.12f));
            Part(PrimitiveType.Sphere, new Vector3(0, 1.8f, 0), Vector3.one * 0.35f);
        }
        else if (role == EnemyRole.Armored)
        {
            Part(PrimitiveType.Cube, new Vector3(0, 0.5f, 0.6f), new Vector3(1.0f, 1.0f, 0.15f));
        }
        else
        {
            Part(PrimitiveType.Sphere, new Vector3(-0.4f, 0.4f, 0), Vector3.one * 0.45f);
            Part(PrimitiveType.Sphere, new Vector3(0.4f, 0.4f, 0), Vector3.one * 0.45f);
        }
    }

    private Transform Part(PrimitiveType shape, Vector3 position, Vector3 size)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = role + " marker";
        part.transform.SetParent(transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        part.GetComponent<Collider>().enabled = false;
        Destroy(part.GetComponent<Collider>());
        if (ArenaLayout.Instance != null) ArenaLayout.Instance.Tint(part, Color.white);
        // У врага с настоящей моделью роль уже видна по ней — белые «маркеры-примитивы» скрываем.
        if (transform.Find("SkinModel") != null) part.GetComponent<Renderer>().enabled = false;
        return part.transform;
    }

    private bool Running => GameSession.Instance != null && GameSession.Instance.State == GameRunState.Playing;
    private float DistanceToTarget => ai.target == null ? float.PositiveInfinity : FlatDistance(transform.position, ai.target.position);
    private void Update()
    {
        if (warning != null && warning.enabled && Time.deltaTime > 0f)
        {
            Color pulse = warningColor;
            pulse.a = 0.38f + 0.22f * Mathf.Sin(Time.time * 9f);
            warningMaterial.SetColor("_BaseColor", pulse);
        }
        if (!Running || ai.target == null || acting || Time.time < nextAction || health.isDead) return;
        if (role == EnemyRole.Shooter && DistanceToTarget <= 15f) StartCoroutine(Shoot());
        else if (role == EnemyRole.Charger && DistanceToTarget <= 13f) StartCoroutine(Charge());
        else if (role == EnemyRole.Bomber && DistanceToTarget <= 3.5f) StartCoroutine(Explode());
        else if (role == EnemyRole.Healer) StartCoroutine(HealAllies());
        else if (role == EnemyRole.Summoner && DistanceToTarget <= 26f && LiveMinions < SummonerMinionLimit) StartCoroutine(Summon());
        else if (role == EnemyRole.Mortar && DistanceToTarget <= 20f) StartCoroutine(Mortar());
        else if (role == EnemyRole.Teleporter && DistanceToTarget > 6f && DistanceToTarget <= 18f) StartCoroutine(Teleport());
        else if (role == EnemyRole.Buffer && EnemyAI.CountWithin(transform.position, BuffRadius) > 1) StartCoroutine(Buff());
        else if (role == EnemyRole.Sniper && DistanceToTarget <= 24f &&
            ArenaLayout.Instance.PathClear(transform.position, ai.target.position, 0.2f)) StartCoroutine(Snipe());
    }

    // World-space ring at an arbitrary point (mortar shell landing, teleport destination).
    // fillSeconds > 0: внутри круга растёт полупрозрачная заливка, заполняя его ровно к моменту удара.
    private void CircleAt(Vector3 center, float radius, float fillSeconds = 0f)
    {
        warning.loop = true;
        warning.positionCount = 48;
        for (int i = 0; i < 48; i++)
        {
            float angle = i * Mathf.PI * 2 / 48;
            warning.SetPosition(i, OnGround(center.x + Mathf.Cos(angle) * radius, center.z + Mathf.Sin(angle) * radius));
        }
        warning.enabled = true;
        ClearFill();
        if (fillSeconds > 0f)
        {
            Vector3 ground = OnGround(center.x, center.z);
            ground.y -= 0.03f;
            fill = EnemyFx.Fill(ground, radius, fillSeconds, fillMaterial);
        }
    }

    // Точка предупреждения чуть над землёй: на рельефе зелёной арены круг не уходит в склон.
    private static Vector3 OnGround(float x, float z)
    {
        var point = new Vector3(x, 0f, z);
        point.y = GroundHeight.At(point) + 0.15f;
        return point;
    }

    private void ClearFill()
    {
        if (fill != null) Destroy(fill.gameObject);
        fill = null;
    }

    public const float MortarRadius = 2.6f;
    public const int MortarDamage = 22;
    // Lobbed shell: the ring marks where the hero stood when it was fired; leaving it in time avoids the hit.
    private IEnumerator Mortar()
    {
        BeginAction();
        Vector3 impact = ai.target.position;
        CircleAt(impact, MortarRadius, 1.5f);   // 0,45 с замах + 1,05 с полёт
        Face(impact - transform.position);
        // Набирает спору (мешок пульсирует), выстреливает её по дуге — она падает в кольцо ровно к моменту урона.
        Anim?.Hold(ModelWalkAnimator.Pose.Aim);
        yield return Wait(0.45f);
        Anim?.Release();
        Anim?.Fire();
        Vector3 landing = new Vector3(impact.x, GroundHeight.At(impact), impact.z);
        MortarShell.Launch(Muzzle("SporeSac", transform.position + Vector3.up * 1.2f), landing, 1.05f, MortarRadius);
        yield return Wait(1.05f);
        warning.enabled = false;
        ClearFill();
        if (ai.target != null && FlatDistance(impact, ai.target.position) <= MortarRadius)
        {
            ai.target.GetComponent<PlayerHealth>()?.TakeDamage(MortarDamage);
            // Попадание по герою: брызги споры и вспышка на нём (сам взрыв рисует MortarShell).
            Vector3 hero = ai.target.position;
            EnemyFx.Blobs(hero + Vector3.up * 1f, new Color(1f, 0.55f, 0.3f), 9, 4f, 0.11f, 0.45f, GroundHeight.At(hero));
            EnemyFx.LightFlash(hero + Vector3.up * 1.2f, new Color(1f, 0.6f, 0.3f), 5f, 3f, 0.25f);
        }
        EndAction(4.5f);
    }

    // Marks a spot close to the hero, then blinks there; afterwards it attacks in melee.
    private IEnumerator Teleport()
    {
        BeginAction();
        Vector3 destination = transform.position;
        bool found = false;
        for (int i = 0; i < 12 && !found; i++)
        {
            Vector3 spot = ai.target.position + Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward * 3.5f;
            spot.y = transform.position.y;
            if (ArenaLayout.Instance.IsWalkable(spot, 0.5f)) { destination = spot; found = true; }
        }
        if (found)
        {
            CircleAt(destination, 1.0f, 0.7f);
            Anim?.Hold(ModelWalkAnimator.Pose.Blink);   // складывает крылья и исчезает, крутясь
            yield return Wait(0.7f);
            warning.enabled = false;
            ClearFill();
            Color blink = RoleColor(role);
            EnemyFx.Blobs(transform.position + Vector3.up * 0.5f, blink, 8, 3f, 0.1f, 0.4f, GroundHeight.At(transform.position));
            if (ArenaLayout.Instance.IsWalkable(destination, 0.5f)) transform.position = destination;
            Anim?.Release();
            Anim?.PopIn(0f, 0.3f);
            EnemyFx.Blobs(transform.position + Vector3.up * 0.5f, blink, 8, 3f, 0.1f, 0.4f, GroundHeight.At(transform.position));
            EnemyFx.Ring(new Vector3(transform.position.x, GroundHeight.At(transform.position) + 0.1f, transform.position.z), 1.2f, blink, 0.3f, 0.25f);
        }
        EndAction(5f);
    }

    public const float BuffRadius = 7f;
    public const float BuffFactor = 1.6f;
    private IEnumerator Buff()
    {
        BeginAction();
        Circle(BuffRadius, 0.7f);
        Anim?.Hold(ModelWalkAnimator.Pose.Cast);
        yield return Wait(0.7f);
        CastBurst(BuffRadius);
        foreach (EnemyAI ally in FindObjectsOfType<EnemyAI>())
            if (ally != ai && FlatDistance(transform.position, ally.transform.position) <= BuffRadius)
            {
                ally.Boost(BuffFactor, 4f);
                ModelWalkAnimator.Of(ally)?.Flash(new Color(2f, 1.3f, 0.6f), 0.25f);
            }
        EndAction(6f);
    }

    private IEnumerator Wait(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (Running) elapsed += Time.deltaTime;
            yield return null;
        }
    }
    private void BeginAction() { acting = true; ai.MovementLocked = true; }
    private void EndAction(float cooldown)
    {
        warning.enabled = false;
        ClearFill();
        acting = false;
        ai.MovementLocked = false;
        nextAction = Time.time + cooldown;
        if (anim != null) anim.Release();
    }
    private Vector3 Aim()
    {
        Vector3 direction = ai.target.position - transform.position;
        direction.y = 0f;
        return direction.normalized;
    }
    private void Line(Vector3 end, Vector3? start = null)
    {
        Vector3 from = start ?? transform.position;
        warning.loop = false;
        warning.positionCount = 2;
        warning.SetPosition(0, OnGround(from.x, from.z));
        warning.SetPosition(1, OnGround(end.x, end.z));
        warning.enabled = true;
    }
    // Круг вокруг самого врага (взрыв подрывника, лечение, усиление, призыв).
    private void Circle(float radius, float fillSeconds = 0f) { CircleAt(transform.position, radius, fillSeconds); }
    private IEnumerator Shoot()
    {
        BeginAction();
        Vector3 direction = Aim();
        Face(direction);
        Line(transform.position + direction * 15f);
        // Раздувает горловой мешок и воротник, затем плюёт сгустком кислоты.
        Anim?.Hold(ModelWalkAnimator.Pose.Aim);
        yield return Wait(0.65f);
        Anim?.Release();
        if (ai.target != null && ArenaLayout.Instance.PathClear(transform.position, transform.position + direction * 1.2f, 0.15f))
        {
            Anim?.Fire();
            EnemyProjectile.Create(transform.position + direction * 1.2f, direction, ai.target, 10, EnemyProjectile.Style.Acid);
            EnemyFx.Blobs(Muzzle("Head", transform.position + direction * 0.9f + Vector3.up), new Color(0.6f, 0.95f, 0.2f), 4, 2.5f, 0.08f, 0.3f,
                GroundHeight.At(transform.position));
        }
        EndAction(2.4f);
    }
    private IEnumerator Charge()
    {
        BeginAction();
        Vector3 direction = Aim();
        Face(direction);
        Line(transform.position + direction * 8f);
        // Замах: приседает, опускает голову, бьёт лапой и роет землю.
        Anim?.Hold(ModelWalkAnimator.Pose.Windup);
        float ground = GroundHeight.At(transform.position);
        for (float dust = 0f; dust < 0.9f; dust += 0.15f)
        {
            yield return Wait(0.15f);
            EnemyFx.Puff(new Vector3(transform.position.x, ground + 0.15f, transform.position.z) + direction * 0.5f - Vector3.Cross(Vector3.up, direction) * 0.3f,
                new Color(0.55f, 0.5f, 0.45f), 0.35f, 0.4f, -direction * 1.5f + Vector3.up);
        }
        warning.enabled = false;
        ClearFill();
        Anim?.Hold(ModelWalkAnimator.Pose.Dash);
        DashTrail(true);
        float elapsed = 0f, nextDust = 0f;
        bool hit = false;
        while (elapsed < 0.65f)
        {
            if (Running)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= nextDust)
                {
                    nextDust = elapsed + 0.05f;
                    Vector3 feet = transform.position;
                    feet.y = GroundHeight.At(feet) + 0.15f;
                    EnemyFx.Puff(feet - direction * 0.4f + Random.insideUnitSphere * 0.25f, new Color(0.58f, 0.52f, 0.46f), 0.45f, 0.45f,
                        -direction * 2f + Vector3.up * 0.8f);
                }
                Vector3 before = transform.position;
                Vector3 after = ArenaLayout.Instance.ResolveMovement(before, before + direction * 12f * Time.deltaTime, 0.7f);
                transform.position = after;
                if (!hit && ai.target != null && SegmentDistance(before, after, ai.target.position) < 1.2f)
                {
                    ai.target.GetComponent<PlayerHealth>()?.TakeDamage(18);
                    hit = true;
                }
                if ((after - before).sqrMagnitude < 0.000001f)
                {
                    // Врезался в препятствие: облако пыли и лёгкая тряска.
                    EnemyFx.Puff(after + direction * 0.6f + Vector3.up * 0.4f, new Color(0.6f, 0.55f, 0.5f), 1f, 0.5f, Vector3.up);
                    ScreenShake.AddAt(after, 0.12f);
                    break;
                }
            }
            yield return null;
        }
        DashTrail(false);
        Anim?.Release();
        Anim?.Fire();   // тормозит, мотнув головой
        EndAction(3.5f);
    }
    // Подрывник взорвался сам (а не убит): труп разлетается обугленными кусками.
    public bool Detonated { get; private set; }
    private IEnumerator Explode()
    {
        BeginAction();
        Circle(3.5f, 1.2f);
        // Раздувается, мигает красным и дрожит всё быстрее.
        Anim?.Hold(ModelWalkAnimator.Pose.Swell);
        yield return Wait(1.2f);
        if (DistanceToTarget <= 3.5f && ArenaLayout.Instance.PathClear(transform.position, ai.target.position, 0f))
            ai.target.GetComponent<PlayerHealth>()?.TakeDamage(25);
        Detonated = true;
        Vector3 ground = transform.position;
        ground.y = GroundHeight.At(ground);
        EnemyFx.Explosion(ground, 3.5f, 1f);
        GameAudio.Play(Sfx.Explosion);
        health.TakeDamage(health.CurrentHealth);
    }
    private IEnumerator HealAllies()
    {
        BeginAction();
        Circle(7f, 0.7f);
        Anim?.Hold(ModelWalkAnimator.Pose.Cast);
        yield return Wait(0.7f);
        CastBurst(7f);
        foreach (EnemyAI ally in FindObjectsOfType<EnemyAI>())
            if (ally != ai && FlatDistance(transform.position, ally.transform.position) <= 7f &&
                ArenaLayout.Instance.PathClear(transform.position, ally.transform.position, 0f))
            {
                Health allyHealth = ally.GetComponent<Health>();
                if (allyHealth != null && allyHealth.Heal(12) > 0)
                {
                    ModelWalkAnimator.Of(ally)?.Flash(new Color(0.8f, 2f, 1.1f), 0.25f);
                    EnemyFx.Blobs(ally.transform.position + Vector3.up * 0.8f, RoleColor(role), 3, 1.5f, 0.09f, 0.45f, GroundHeight.At(ally.transform.position));
                }
            }
        EndAction(4f);
    }

    // ---- Вид атак ----

    private ModelWalkAnimator anim;
    private ModelWalkAnimator Anim => anim != null ? anim : (anim = ModelWalkAnimator.Of(this));
    private TrailRenderer dashTrail;

    // Повернуться к цели атаки (иначе модель могла смотреть туда, куда шла).
    private void Face(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    // Мировая точка части модели (мешок миномёта, голова стрелка) или запасная точка, если модели нет.
    private Vector3 Muzzle(string part, Vector3 fallback)
    {
        Transform skin = transform.Find("SkinModel");
        if (skin == null) return fallback;
        foreach (Transform t in skin.GetComponentsInChildren<Transform>())
            if (t.name == part)
            {
                Renderer r = t.GetComponent<Renderer>();
                return r != null ? r.bounds.center : t.position;
            }
        return fallback;
    }

    // След за тараном на рывке.
    private void DashTrail(bool on)
    {
        if (dashTrail == null)
        {
            if (!on) return;
            var go = new GameObject("Dash trail");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.5f;
            dashTrail = go.AddComponent<TrailRenderer>();
            dashTrail.time = 0.15f;
            dashTrail.widthMultiplier = 0.45f;
            dashTrail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            dashTrail.sharedMaterial = EnemyFx.Mat(Color.Lerp(RoleColor(role), Color.white, 0.3f));
            dashTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dashTrail.receiveShadows = false;
        }
        dashTrail.Clear();
        dashTrail.emitting = on;
    }

    // Выброс силы в конце каста (лечение, усиление, призыв): кольцо цвета роли и искры.
    private void CastBurst(float radius)
    {
        Anim?.Release();
        Anim?.Fire();
        Vector3 ground = transform.position;
        ground.y = GroundHeight.At(ground) + 0.1f;
        Color color = RoleColor(role);
        EnemyFx.Ring(ground, radius, color, 0.45f, 0.3f);
        EnemyFx.Blobs(Muzzle("Head", transform.position + Vector3.up), color, 6, 3f, 0.09f, 0.45f, ground.y);
    }

    // Long thin warning, a pause, then one fast and heavy shot along the locked direction (sidestep to dodge).
    private IEnumerator Snipe()
    {
        BeginAction();
        Vector3 direction = Aim();
        // The barrel marker points along transform.forward: turn the sniper to the locked shot direction.
        if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        // Both the aim line and the shot start at the barrel tip (marker: local z 1.0, length 0.9).
        Vector3 muzzle = transform.TransformPoint(new Vector3(0f, 0.25f, 1.45f));
        muzzle.y = transform.position.y;
        Line(muzzle + direction * 28f, muzzle);
        warning.widthMultiplier = 0.07f;
        // Пригибается и замирает, целясь костяным «стволом» на морде; выстрел — иглой с отдачей.
        Anim?.Hold(ModelWalkAnimator.Pose.Aim);
        yield return Wait(1.2f);
        Anim?.Release();
        warning.widthMultiplier = 0.12f;
        muzzle = transform.TransformPoint(new Vector3(0f, 0.25f, 1.45f));
        muzzle.y = transform.position.y;
        if (ai.target != null && ArenaLayout.Instance.PathClear(transform.position, muzzle + direction * 0.3f, 0.15f))
        {
            Anim?.Fire();
            EnemyProjectile shot = EnemyProjectile.Create(muzzle + direction * 0.3f, direction, ai.target, 22, EnemyProjectile.Style.Quill);
            shot.speed = 24f;
            EnemyFx.Puff(new Vector3(muzzle.x, 0.9f, muzzle.z), new Color(0.95f, 0.9f, 0.75f), 0.35f, 0.25f, direction * 2f);
        }
        EndAction(4.5f);
    }

    // Circle warning, then up to two weak minions next to the summoner; at most four alive per summoner.
    private IEnumerator Summon()
    {
        BeginAction();
        Circle(2.5f, 0.8f);
        Anim?.Hold(ModelWalkAnimator.Pose.Cast);   // мешки с выводком пульсируют
        yield return Wait(0.8f);
        CastBurst(2.5f);
        WaveManager waves = FindObjectOfType<WaveManager>();
        if (splitPrefab != null && waves != null && (!waves.Suspended || GameSession.TestMode))
        {
            int cap = waves.settings.AbsoluteLimitForWave(waves.wave);
            for (int i = 0; i < 2 && LiveMinions < SummonerMinionLimit && EnemyAI.ActiveCount < cap; i++)
            {
                Vector3 point = transform.position + Quaternion.Euler(0, 90f + i * 180f, 0) * transform.forward * 1.8f;
                if (!ArenaLayout.Instance.IsWalkable(point, 0.4f)) continue;
                Health minion = SpawnMinion(splitPrefab, point, waves.groundHeight);
                if (minion != null) minions.Add(minion);
            }
        }
        EndAction(7f);
    }

    private Health SpawnMinion(GameObject prefab, Vector3 point, float groundHeight)
    {
        GameObject child = Instantiate(prefab, point, Quaternion.identity);
        child.name = "Summoned minion";
        child.transform.localScale *= 0.55f;
        ModelSkins.ApplyEnemy(child, "Enemy_Weak");   // миньон — клон базового Weak: тот же вид, но меньше
        float bottom = float.PositiveInfinity;
        foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>()) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
        child.transform.position += Vector3.up * (groundHeight - bottom);
        EnemyAI childAI = child.GetComponent<EnemyAI>();
        childAI.target = ai.target;
        childAI.speed = 4.2f;
        childAI.damage = 5;
        Health childHealth = child.GetComponent<Health>();
        childHealth.SetHealth(15);
        childHealth.experienceReward = 0;
        ArenaLayout.Instance.Tint(child, RoleColor(role));
        ModelWalkAnimator.Of(child.transform)?.PopIn(0f, 0.35f);   // «вылупляется» с перелётом масштаба
        EnemyFx.Blobs(child.transform.position + Vector3.up * 0.3f, RoleColor(role), 5, 2.5f, 0.08f, 0.4f, groundHeight);
        return childHealth;
    }

    private void OnDeath(Health dead)
    {
        if (role != EnemyRole.Splitter || splitPrefab == null || !Running) return;
        WaveManager waves = FindObjectOfType<WaveManager>();
        // В тест-арене волны выключены, но разделение должно работать, чтобы его можно было посмотреть.
        if (waves == null || (waves.Suspended && !GameSession.TestMode)) return;
        int limit = waves.settings.AliveLimitForWave(waves.wave);
        for (int i = 0; i < 2 && EnemyAI.ActiveCount < limit; i++)
        {
            Vector3 point = transform.position + (i == 0 ? Vector3.left : Vector3.right) * 1.3f;
            if (!ArenaLayout.Instance.IsWalkable(point, 0.4f)) continue;
            GameObject child = Instantiate(splitPrefab, point, Quaternion.identity);
            child.name = "Splitter fragment";
            child.transform.localScale *= 0.55f;
            ModelSkins.ApplyEnemy(child, "Enemy_Splitter");   // осколок выглядит как уменьшенный Splitter
            float bottom = float.PositiveInfinity;
            foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>()) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
            child.transform.position += Vector3.up * (waves.groundHeight - bottom);
            EnemyAI childAI = child.GetComponent<EnemyAI>();
            childAI.target = ai.target;
            childAI.speed = 4f;
            childAI.damage = 5;
            Health childHealth = child.GetComponent<Health>();
            childHealth.SetHealth(20);
            childHealth.experienceReward = 0;
            ArenaLayout.Instance.Tint(child, RoleColor(role));
            // Осколок «вырастает» там, куда доехала половина трупа (EnemyCorpse.Split).
            ModelWalkAnimator.Of(child.transform)?.PopIn(0.16f, 0.3f);
        }
    }
    private static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }
    private static float SegmentDistance(Vector3 a, Vector3 b, Vector3 point)
    {
        a.y = b.y = point.y = 0f;
        Vector3 delta = b - a;
        return Vector3.Distance(point, a + delta * Mathf.Clamp01(Vector3.Dot(point - a, delta) / Mathf.Max(0.00001f, delta.sqrMagnitude)));
    }
    private void OnDisable()
    {
        StopAllCoroutines();
        acting = false;
        if (ai != null) ai.MovementLocked = false;
        if (warning != null) warning.enabled = false;
        ClearFill();
        if (dashTrail != null) dashTrail.emitting = false;
        if (anim != null) anim.Release();
    }
    private void OnDestroy()
    {
        if (health != null) health.Died -= OnDeath;
        if (warningMaterial != null) Destroy(warningMaterial);
        if (fillMaterial != null) Destroy(fillMaterial);
        ClearFill();
    }
}
