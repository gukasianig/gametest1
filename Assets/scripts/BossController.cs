using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Финальные боссы (12.11.2026): в конце забега случайно выбирается один (GameSession.PrepareBoss).
public enum BossKind { Guardian, SwarmQueen, StormSniper }

// 19.11.2026 (просьба владельца: «исправить анимации и визуал, усилить, новый HUD, много вариантов атак»):
// босс поворачивается к герою и по ходу рывка, держится на рельефе, позы атак через ModelWalkAnimator
// (замах, прицел, рывок, заклинание), эффектное появление; у каждого 5–6 атак в случайном порядке без повторов подряд
// и вторая фаза ниже 50% HP. Атаки Королевы и Снайпера — BossQueen.cs и BossSniper.cs; зоны урона — BossHazard.
public partial class BossController : MonoBehaviour
{
    public BossKind Kind { get; private set; }
    public string Title => Titles[(int)Kind];
    public static readonly string[] Titles = { GameText.Source("bosscontroller.069"), GameText.Source("bosscontroller.070"), GameText.Source("bosscontroller.071") };
    private static readonly string[] SkinKeys = { "Boss", "Swarm queen", "Storm sniper" };
    private static readonly int[] BaseHealth = { 5200, 4600, 4000 };
    // HP при появлении с учётом испытаний и круга бесконечного режима (проверки берут его отсюда, а не числом).
    public static int StartingHealth(BossKind kind) =>
        Mathf.RoundToInt(BaseHealth[(int)kind] * HealthMultiplier * RunModifiers.BossHealth * GameSession.EndlessFactor);
    // 12.11.2026: финальный босс сильнее в несколько раз; 19.11.2026 ещё сильнее (HP ×4 → ×5, урон ×1,6 → ×1,9).
    public const float HealthMultiplier = 5f, DamageMultiplier = 1.9f;
    // Цвет босса: предупреждения, полоса HP, вспышки.
    public static readonly Color[] Accents = { new Color(1f, 0.55f, 0.2f), new Color(0.72f, 0.45f, 1f), new Color(0.55f, 0.85f, 1f) };
    public Color Accent => Accents[(int)Kind];
    public bool Enraged { get; private set; }
    public Health Health { get; private set; }
    public Transform target;
    public int slamDamage = 18, projectileDamage = 12, dashDamage = 20;
    public float attackDelay = 1.25f;
    public int AttackIndex { get; private set; }
    public bool Telegraphing { get; private set; }
    public string LastAttack { get; private set; } = "";
    private LineRenderer warning;
    private Vector3 waypoint;
    private float nextPath;
    private bool attacking;
    private Material warningMaterial;
    private const float Radius = 1.6f;   // корень — сфера 3,2 м: центр на 1,6 м над землёй

    public static BossController Create(Vector3 position, Transform player, BossKind kind = BossKind.Guardian)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        root.name = kind == BossKind.Guardian ? GameText.Source("bosscontroller.072") : kind == BossKind.SwarmQueen ? GameText.Source("bosscontroller.073") : GameText.Source("bosscontroller.074");
        root.tag = "Enemy";
        root.transform.position = position;
        root.transform.localScale = Vector3.one * 3.2f;
        ArenaLayout.Instance.Tint(root, new Color(0.8f, 0.34f, 0.12f));
        ModelSkins.ApplyEnemy(root, SkinKeys[(int)kind]);
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        Health health = root.AddComponent<Health>();
        health.SetHealth(StartingHealth(kind));
        health.experienceReward = 0;
        BossController boss = root.AddComponent<BossController>();
        boss.Kind = kind;
        boss.slamDamage = Mathf.RoundToInt(boss.slamDamage * DamageMultiplier);
        boss.projectileDamage = Mathf.RoundToInt(boss.projectileDamage * DamageMultiplier);
        boss.dashDamage = Mathf.RoundToInt(boss.dashDamage * DamageMultiplier);
        boss.Health = health;
        boss.target = player;
        // Сразу лицом к герою (раньше корень не поворачивался, и босс стоял к герою спиной).
        if (player != null)
        {
            Vector3 face = player.position - position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f) root.transform.rotation = Quaternion.LookRotation(face);
        }
        return boss;
    }

    private static BossKind lastKind = (BossKind)(-1);

    // Случайный финальный босс, не тот же, что в прошлом забеге (за одну игру).
    public static BossKind RandomKind()
    {
        int count = System.Enum.GetValues(typeof(BossKind)).Length;
        BossKind pick = (BossKind)Random.Range(0, count);
        if (pick == lastKind) pick = (BossKind)(((int)pick + 1 + Random.Range(0, count - 1)) % count);
        lastKind = pick;
        return pick;
    }

    private ModelWalkAnimator animator;
    private ModelWalkAnimator Anim => animator != null ? animator : (animator = ModelWalkAnimator.Of(this));

    private void Start()
    {
        GameAudio.BossFight(true);
        Health = GetComponent<Health>();
        GameObject line = new GameObject("Attack warning", typeof(LineRenderer));
        line.transform.SetParent(transform, false);
        warning = line.GetComponent<LineRenderer>();
        warning.useWorldSpace = true;
        warning.widthMultiplier = 0.16f;
        warning.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        warningMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        warningMaterial.color = Accent;
        warning.sharedMaterial = warningMaterial;
        warning.enabled = false;
        Entrance();
        StartCoroutine(AttackLoop());
    }

    // Появление: модель вырастает из земли, кольцо и вспышка цвета босса, тряска и рёв.
    private void Entrance()
    {
        Vector3 feet = Ground(transform.position);
        if (Anim != null) Anim.PopIn(0f, 0.7f);
        EnemyFx.Ring(feet, 7f, Accent, 0.8f, 0.5f);
        EnemyFx.Ring(feet, 4f, Color.white, 0.5f, 0.25f);
        EnemyFx.LightFlash(feet + Vector3.up * 2f, Accent, 14f, 4f, 0.8f);
        EnemyFx.Explosion(feet, 3f, 0.6f, false);
        ScreenShake.AddAt(feet, 0.7f);
        GameAudio.Play(Sfx.Explosion, 0.9f);
    }

    private bool Running => target != null && Health != null && !Health.isDead &&
        GameSession.Instance != null && GameSession.Instance.State == GameRunState.Playing;

    private float retargetTimer;

    private void Update()
    {
        // Кооператив: босс выбирает ближайшего живого героя между атаками.
        if (CoopMode.Active && !attacking && (retargetTimer -= Time.deltaTime) <= 0f)
        {
            retargetTimer = 0.5f;
            Transform nearest = CoopMode.NearestAlive(transform.position);
            if (nearest != null) target = nearest;
        }
        if (!Running || attacking) return;
        Vector3 delta = target.position - transform.position;
        delta.y = 0f;
        if (Kind == BossKind.StormSniper) { KeepDistance(delta); return; }
        if (delta.magnitude < 9f) return;
        if (Time.time >= nextPath || (waypoint - transform.position).sqrMagnitude < 0.5f)
        {
            waypoint = ArenaLayout.Instance.NextWaypoint(transform.position, target.position, Radius);
            nextPath = Time.time + 0.6f;
        }
        float speed = (Kind == BossKind.SwarmQueen ? 2.6f : 2.4f) * (Enraged ? 1.25f : 1f);
        Vector3 desired = Vector3.MoveTowards(transform.position, new Vector3(waypoint.x, transform.position.y, waypoint.z), speed * SlowEffect.MovementFactor(this) * Time.deltaTime);
        transform.position = ArenaLayout.Instance.ResolveMovement(transform.position, desired, Radius);
    }

    // Поворот и высота над рельефом — после движения и атак (рывок, прыжок, телепорт).
    private bool faceLocked;
    private Vector3 faceDirection;
    private float airborne;

    private void LateUpdate()
    {
        if (Health == null || Health.isDead || Time.deltaTime <= 0f) return;
        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, GroundHeight.At(p) + Radius + airborne, p.z);
        Vector3 look = faceLocked ? faceDirection : target != null ? target.position - transform.position : transform.forward;
        look.y = 0f;
        if (look.sqrMagnitude < 0.01f) return;
        float turn = (Kind == BossKind.StormSniper ? 360f : 220f) * (Enraged ? 1.3f : 1f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), turn * Time.deltaTime);
    }

    private void Face(Vector3 direction) { direction.y = 0f; if (direction.sqrMagnitude > 0.001f) { faceDirection = direction.normalized; faceLocked = true; } }

    private enum Attack { Slam, Fan, Dash, Shockwave, RockRain, TripleDash, Brood, Acid, Pounce, AcidPools, Spiral, Strikes, Beam, Sweep, Creep, TripleBeam }
    private Attack previous = (Attack)(-1);

    // Окно передышки (21.11.2026): тяжёлые серии добавляют паузу до следующей атаки даже во второй фазе,
    // чтобы был ритм «уклонился → нанёс урон». Атака сама выставляет бонус; цикл сбрасывает его перед каждой атакой.
    public const float HeavyRecovery = 1.2f;
    private float recoveryBonus;
    public float LastRecovery { get; private set; }

    // Набор атак: первая — фирменная, дальше случайно без повтора подряд; во второй фазе добавляются сильные.
    private Attack NextAttack()
    {
        var pool = new List<Attack>();
        switch (Kind)
        {
            case BossKind.Guardian:
                pool.AddRange(new[] { Attack.Slam, Attack.Fan, Attack.Dash, Attack.Shockwave, Attack.RockRain });
                if (Enraged) pool.Add(Attack.TripleDash);
                break;
            case BossKind.SwarmQueen:
                pool.AddRange(new[] { Attack.Brood, Attack.Acid, Attack.Pounce, Attack.AcidPools, Attack.Spiral });
                break;
            default:
                pool.AddRange(new[] { Attack.Strikes, Attack.Beam, Attack.Sweep, Attack.Creep });
                if (Enraged) pool.Add(Attack.TripleBeam);
                break;
        }
        if (AttackIndex == 0) return pool[0];
        pool.Remove(previous);
        return pool[Random.Range(0, pool.Count)];
    }

    private IEnumerator Run(Attack attack)
    {
        LastAttack = attack.ToString();
        switch (attack)
        {
            case Attack.Slam: return Slam();
            case Attack.Fan: return Fan();
            case Attack.Dash: return Dash(1.1f);
            case Attack.Shockwave: return Shockwave();
            case Attack.RockRain: return RockRain();
            case Attack.TripleDash: return TripleDash();
            case Attack.Brood: return QueenBrood();
            case Attack.Acid: return QueenAcid();
            case Attack.Pounce: return QueenPounce();
            case Attack.AcidPools: return QueenAcidPools();
            case Attack.Spiral: return QueenSpiral();
            case Attack.Strikes: return SniperStrikes();
            case Attack.Beam: return SniperBeam();
            case Attack.Sweep: return SniperSweep();
            case Attack.Creep: return SniperCreep();
            default: return SniperTripleBeam();
        }
    }

    private IEnumerator AttackLoop()
    {
        yield return Wait(2.2f);
        while (Health != null && !Health.isDead)
        {
            while (!Running) yield return null;
            attacking = true;
            Attack attack = NextAttack();
            AttackIndex++;
            recoveryBonus = 0f;
            yield return Run(attack);
            previous = attack;
            attacking = false;
            faceLocked = false;
            airborne = 0f;
            if (warning != null) warning.enabled = false;
            Telegraphing = false;
            if (Anim != null) Anim.Release();
            if (!Enraged && Health != null && Health.CurrentHealth < Health.maxHealth / 2) Enrage();
            LastRecovery = recoveryBonus;
            yield return Wait(attackDelay * RunModifiers.BossAttackDelay * (Enraged ? 0.6f : 1f) + recoveryBonus);
        }
    }

    // Пауза только по игровому времени забега (не идёт на паузе и в окне карточек).
    private IEnumerator Wait(float seconds)
    {
        while (seconds > 0f)
        {
            if (Health == null || Health.isDead) yield break;
            if (Running) seconds -= Time.deltaTime;
            yield return null;
        }
    }

    // Вторая фаза ниже 50% HP: атаки чаще и сильнее, своё сообщение у каждого босса.
    private void Enrage()
    {
        Enraged = true;
        Vector3 feet = Ground(transform.position);
        EnemyFx.Ring(feet, 7f, Accent, 0.6f, 0.5f);
        EnemyFx.Ring(feet, 4.5f, Color.white, 0.4f, 0.25f);
        EnemyFx.LightFlash(feet + Vector3.up * 2f, Accent, 12f, 4f, 0.6f);
        ScreenShake.AddAt(transform.position, 0.5f);
        GameAudio.Play(Sfx.Explosion, 0.7f);
        if (Anim != null) { Anim.Hold(ModelWalkAnimator.Pose.Swell, 0.8f); Anim.Flash(Accent * 1.6f, 0.4f); }
        string text = Kind == BossKind.SwarmQueen ? GameText.Source("bosscontroller.075") :
            Kind == BossKind.Guardian ? GameText.Source("boss.phase2.guardian") : GameText.Source("boss.phase2.sniper");
        if (GameSession.Instance != null) GameSession.Instance.Announce(text, 2.5f);
    }

    private void Circle(Vector3 centre, float radius)
    {
        warning.enabled = true;
        warning.loop = true;
        warning.positionCount = 64;
        for (int i = 0; i < 64; i++)
        {
            float angle = i * Mathf.PI * 2f / 64;
            Vector3 p = new Vector3(centre.x + Mathf.Cos(angle) * radius, 0f, centre.z + Mathf.Sin(angle) * radius);
            p.y = GroundHeight.At(p) + 0.08f;
            warning.SetPosition(i, p);
        }
        Telegraphing = true;
    }

    private void Line(Vector3 start, Vector3 end, float width)
    {
        warning.enabled = true;
        warning.loop = false;
        warning.positionCount = 2;
        start.y = GroundHeight.At(start) + 0.08f;
        end.y = GroundHeight.At(end) + 0.08f;
        warning.SetPosition(0, start);
        warning.SetPosition(1, end);
        warning.widthMultiplier = width;
        Telegraphing = true;
    }

    private Vector3 ToTarget()
    {
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
    }

    // ---------------- Страж арены ----------------

    // Удар по земле под героем; во второй фазе — два удара подряд вдогонку.
    private IEnumerator Slam()
    {
        if (target == null) yield break;
        int count = Enraged ? 2 : 1;
        for (int k = 0; k < count && target != null; k++)
        {
            Vector3 centre = Ground(target.position);
            Face(centre - transform.position);
            warning.widthMultiplier = 0.16f;
            Circle(centre, 4.5f);
            if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Windup, 2f);
            float left = k == 0 ? 1.2f : 0.8f;
            while (left > 0f)
            {
                if (Running) left -= Time.deltaTime;
                warning.widthMultiplier = Mathf.Lerp(0.14f, 0.5f, 1f - left / 1.2f);
                yield return null;
                if (Health == null || Health.isDead) yield break;
            }
            Telegraphing = false;
            warning.enabled = false;
            if (Anim != null) { Anim.Release(); Anim.Fire(); }
            CoopMode.DamageHeroesWithin(centre, 4.9f, slamDamage, HeroDamageCause.BossSlam);
            EnemyFx.Explosion(centre, 3.6f, 0.7f, true);
            EnemyFx.Ring(centre, 4.5f, Accent, 0.45f, 0.45f);
            GameAudio.Play(Sfx.Explosion, 0.9f);
            ScreenShake.AddAt(centre, 0.45f);
            yield return Wait(0.3f);
        }
    }

    // Веер огненных шаров; во второй фазе — 11 шаров и второй залп со сдвигом.
    private IEnumerator Fan()
    {
        if (target == null) yield break;
        Vector3 direction = ToTarget();
        Face(direction);
        int side = Enraged ? 5 : 3;
        float step = Enraged ? 9f : 12f;
        DrawFan(direction, side, step, 20f);
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Aim, 2f);
        yield return Wait(1.0f);
        warning.enabled = false;
        Telegraphing = false;
        if (Anim != null) { Anim.Release(); Anim.Fire(); }
        Volley(direction, side, step, 0f);
        GameAudio.Play(Sfx.Throw, 0.8f);
        if (!Enraged) yield break;
        yield return Wait(0.35f);
        if (Anim != null) Anim.Fire();
        Volley(direction, side, step, step * 0.5f);
        GameAudio.Play(Sfx.Throw, 0.8f);
    }

    private void DrawFan(Vector3 direction, int side, float step, float length)
    {
        warning.enabled = true;
        warning.loop = false;
        warning.widthMultiplier = 0.16f;
        warning.positionCount = side * 4 + 3;
        Vector3 start = Ground(transform.position);
        int n = 0;
        for (int i = -side; i <= side; i++)
        {
            warning.SetPosition(n++, start);
            Vector3 end = start + Quaternion.Euler(0, i * step, 0) * direction * length;
            end.y = GroundHeight.At(end) + 0.08f;
            warning.SetPosition(n++, end);
        }
        warning.SetPosition(n, start);
        Telegraphing = true;
    }

    private void Volley(Vector3 direction, int side, float step, float offset, EnemyProjectile.Style style = EnemyProjectile.Style.Ball)
    {
        for (int i = -side; i <= side; i++)
            EnemyProjectile.Create(transform.position + direction * 2f, Quaternion.Euler(0, i * step + offset, 0) * direction, target,
                projectileDamage, style, HeroDamageCause.BossProjectile);
    }

    // Таран по полосе.
    private IEnumerator Dash(float telegraph)
    {
        if (target == null) yield break;
        Vector3 direction = ToTarget();
        Face(direction);
        Vector3 end = transform.position + direction * 16f;
        Line(transform.position, end, 3.2f);
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Windup, telegraph + 0.2f);
        yield return Wait(telegraph);
        warning.enabled = false;
        Telegraphing = false;
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Dash, 1.5f);
        var hit = new HashSet<PlayerHealth>();
        float elapsed = 0f;
        while (elapsed < 1.1f)
        {
            if (Health == null || Health.isDead) yield break;
            if (Running)
            {
                elapsed += Time.deltaTime;
                Vector3 desired = transform.position + direction * 15f * Time.deltaTime;
                Vector3 allowed = ArenaLayout.Instance.ResolveMovement(transform.position, desired, Radius);
                if ((allowed - transform.position).sqrMagnitude < 0.00001f) break;
                transform.position = allowed;
                if (Random.value < 0.5f) EnemyFx.Puff(Ground(transform.position), new Color(0.7f, 0.62f, 0.48f), 0.7f, 0.5f, Vector3.up * 0.8f - direction);
                foreach (PlayerHealth hero in CoopMode.Heroes)
                    if (hero != null && !hero.IsDead && !hit.Contains(hero) && HorizontalDistance(hero.transform.position, transform.position) < 2.4f)
                    {
                        hit.Add(hero);
                        hero.TakeDamage(dashDamage, HeroDamageCause.BossCharge);
                    }
            }
            yield return null;
        }
        if (Anim != null) Anim.Release();
        ScreenShake.AddAt(transform.position, 0.3f);
    }

    // Вторая фаза: три тарана подряд с коротким прицелом.
    private IEnumerator TripleDash()
    {
        for (int i = 0; i < 3 && target != null; i++)
        {
            yield return Dash(i == 0 ? 0.8f : 0.5f);
            yield return Wait(0.2f);
        }
        recoveryBonus = HeavyRecovery;
    }

    // Ударные волны: кольца расходятся от Стража, их нужно перепрыгнуть рывком или переждать вдали.
    private IEnumerator Shockwave()
    {
        Vector3 centre = Ground(transform.position);
        Circle(centre, 2.6f);
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Windup, 1.5f);
        yield return Wait(0.9f);
        warning.enabled = false;
        Telegraphing = false;
        if (Anim != null) { Anim.Release(); Anim.Fire(); }
        int waves = Enraged ? 3 : 2;
        for (int i = 0; i < waves; i++)
        {
            StartCoroutine(WaveRing(Ground(transform.position), 20f, 11f));
            ScreenShake.AddAt(centre, 0.3f);
            GameAudio.Play(Sfx.Explosion, 0.6f);
            yield return Wait(0.6f);
        }
        yield return Wait(1f);
    }

    private IEnumerator WaveRing(Vector3 centre, float maxRadius, float speed)
    {
        var go = new GameObject("Boss shockwave", typeof(LineRenderer));
        LineRenderer line = go.GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = 56;
        line.widthMultiplier = 0.5f;
        line.sharedMaterial = RingMaterial(Accent);
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // Страховка: если босс погибнет посреди волны, корутина остановится, а кольцо всё равно исчезнет.
        Destroy(go, (maxRadius - 1.5f) / Mathf.Max(1f, speed) + 2f);
        var hit = new HashSet<PlayerHealth>();
        float radius = 1.5f;
        while (go != null && radius < maxRadius && Health != null && !Health.isDead)
        {
            if (Running) radius += speed * Time.deltaTime;
            for (int i = 0; i < 56; i++)
            {
                float a = i * Mathf.PI * 2f / 56;
                Vector3 p = centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                p.y = GroundHeight.At(p) + 0.12f;
                line.SetPosition(i, p);
            }
            foreach (PlayerHealth hero in CoopMode.Heroes)
                if (hero != null && !hero.IsDead && !hit.Contains(hero) && Mathf.Abs(HorizontalDistance(hero.transform.position, centre) - radius) < 0.9f)
                {
                    hit.Add(hero);
                    hero.TakeDamage(Mathf.RoundToInt(slamDamage * 0.7f), HeroDamageCause.BossSlam);
                }
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // Каменный дождь: круги вокруг героя (один под ним), камни падают по очереди.
    private IEnumerator RockRain()
    {
        if (target == null) yield break;
        int count = Enraged ? 10 : 7;
        var spots = new Vector3[count];
        var marks = new GameObject[count];
        for (int i = 0; i < count; i++)
        {
            Vector2 around = i == 0 ? Vector2.zero : Random.insideUnitCircle * 7f;
            spots[i] = Ground(target.position + new Vector3(around.x, 0f, around.y));
            marks[i] = Ring(spots[i], 2.1f, Accent, 0.14f);
        }
        Telegraphing = true;
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Cast, 2.5f);
        ScreenShake.AddAt(transform.position, 0.25f);
        yield return Wait(0.6f);
        for (int i = 0; i < count; i++)
        {
            MortarShell.Launch(spots[i] + Vector3.up * 14f, spots[i], 0.45f, 2.1f);
            yield return Wait(0.12f);
        }
        yield return Wait(0.35f);
        Telegraphing = false;
        if (Anim != null) Anim.Release();
        for (int i = 0; i < count; i++)
        {
            if (marks[i] != null) Destroy(marks[i]);
            CoopMode.DamageHeroesWithin(spots[i], 2.3f, Mathf.RoundToInt(slamDamage * 0.8f), HeroDamageCause.Meteor);
        }
    }

    // ---------------- Общие помощники ----------------

    private static Vector3 Ground(Vector3 p) { return new Vector3(p.x, GroundHeight.At(p) + 0.08f, p.z); }

    private static readonly Dictionary<Color, Material> ringMaterials = new Dictionary<Color, Material>();

    private static Material RingMaterial(Color color)
    {
        if (!ringMaterials.TryGetValue(color, out Material material) || material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = color };
            ringMaterials[color] = material;
        }
        return material;
    }

    // Отдельное кольцо-предупреждение (их бывает несколько одновременно).
    private GameObject Ring(Vector3 centre, float radius, Color color, float width)
    {
        var go = new GameObject("Boss warning ring", typeof(LineRenderer));
        LineRenderer line = go.GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = 40;
        line.widthMultiplier = width;
        line.sharedMaterial = RingMaterial(color);
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        for (int i = 0; i < 40; i++)
        {
            float a = i * Mathf.PI * 2f / 40;
            Vector3 p = centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            p.y = GroundHeight.At(p) + 0.08f;
            line.SetPosition(i, p);
        }
        Destroy(go, 4f);
        return go;
    }

    // Временная линия (несколько лучей одновременно).
    private LineRenderer TempLine(Vector3 start, Vector3 end, float width, Color color, float life)
    {
        var go = new GameObject("Boss warning line", typeof(LineRenderer));
        LineRenderer line = go.GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = width;
        line.sharedMaterial = RingMaterial(color);
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        start.y = GroundHeight.At(start) + 0.08f;
        end.y = GroundHeight.At(end) + 0.08f;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        Destroy(go, life);
        return line;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void OnDestroy()
    {
        GameAudio.BossFight(false);
        if (warningMaterial != null) Destroy(warningMaterial);
    }
}

// Зона урона на земле (кислотная лужа королевы): кольцо и пятно, урон героям внутри раз в 0,5 с, затем исчезает.
public sealed class BossHazard : MonoBehaviour
{
    private float radius, until, tick;
    private int damage;
    private HeroDamageCause cause;
    private LineRenderer ring;
    private Color color;

    public static BossHazard Spawn(Vector3 position, float radius, float seconds, int damage, Color color, HeroDamageCause cause)
    {
        var go = new GameObject("Boss hazard", typeof(LineRenderer));
        go.transform.position = new Vector3(position.x, GroundHeight.At(position), position.z);
        BossHazard hazard = go.AddComponent<BossHazard>();
        hazard.radius = radius;
        hazard.until = Time.time + seconds;
        hazard.damage = Mathf.Max(1, damage);
        hazard.cause = cause;
        hazard.color = color;
        hazard.ring = go.GetComponent<LineRenderer>();
        hazard.ring.useWorldSpace = true;
        hazard.ring.loop = true;
        hazard.ring.positionCount = 40;
        hazard.ring.sharedMaterial = EnemyFx.Mat(color);
        hazard.ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        for (int i = 0; i < 40; i++)
        {
            float a = i * Mathf.PI * 2f / 40;
            Vector3 p = go.transform.position + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            p.y = GroundHeight.At(p) + 0.1f;
            hazard.ring.SetPosition(i, p);
        }
        EnemyFx.GroundSplat(go.transform.position + Vector3.up * 0.05f, radius * 0.9f, color * 0.7f, seconds);
        return hazard;
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f) return;
        if (Time.time >= until || (GameSession.Instance != null && GameSession.Instance.IsFinished)) { Destroy(gameObject); return; }
        ring.widthMultiplier = 0.12f + 0.06f * Mathf.Sin(Time.time * 8f);
        if (Random.value < 0.15f)
            EnemyFx.Puff(transform.position + new Vector3(Random.Range(-radius, radius) * 0.7f, 0.2f, Random.Range(-radius, radius) * 0.7f), color, 0.25f, 0.6f, Vector3.up * 0.6f);
        if ((tick -= Time.deltaTime) > 0f) return;
        tick = 0.5f;
        CoopMode.DamageHeroesWithin(transform.position, radius, damage, cause);
    }
}
