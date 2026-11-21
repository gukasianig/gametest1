using System.Collections;
using UnityEngine;

// Мини-босс «Громила» (11.11.2026, решение владельца): великан из модели врага-танка на середине забега (5-я минута).
// Медленно идёт к ближайшему герою и по очереди: удар по земле (красный круг 1,1 с) и рывок (полоса 0,8 с).
// Награда — гарантированный малый сундук (в нём может быть эволюция оружия) и кольцо кристаллов. Полоса HP — у GameSession.
// 19.11.2026: сильнее (HP 2000 → 2800, урон выше), новые атаки — бросок камней, рёв с подкреплением, двойной удар;
// случайный порядок без повторов подряд, вторая фаза ниже 50% HP (паузы короче), позы атак через ModelWalkAnimator.
public class MiniBoss : MonoBehaviour
{
    public const float SpawnShare = 0.5f;          // доля времени забега до появления
    public const int BaseHealth = 2800;
    public const float Scale = 2.4f;
    public const float SlamRadius = 5f;
    public const int SlamDamage = 30, ChargeDamage = 27, RockDamage = 24;
    public static readonly Color Accent = new Color(1f, 0.32f, 0.2f);
    public bool Enraged { get; private set; }
    public string LastAttack { get; private set; } = "";
    private ModelWalkAnimator animator;
    private ModelWalkAnimator Anim => animator != null ? animator : (animator = ModelWalkAnimator.Of(this));
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
        yield return Wait(2.5f);
        int previous = -1;
        while (Health != null && !Health.isDead)
        {
            while (!Running) yield return null;
            // 0 удар, 1 рывок, 2 камни, 3 рёв, 4 двойной удар; первым — удар, дальше случайно без повтора.
            int pick = Attacks == 0 ? 0 : Random.Range(0, 5);
            if (pick == previous) pick = (pick + 1 + Random.Range(0, 4)) % 5;
            previous = pick;
            LastAttack = pick == 0 ? "Slam" : pick == 1 ? "Charge" : pick == 2 ? "Boulders" : pick == 3 ? "Roar" : "DoubleSlam";
            if (pick == 0) yield return Slam();
            else if (pick == 1) yield return Charge();
            else if (pick == 2) yield return Boulders();
            else if (pick == 3) yield return Roar();
            else yield return DoubleSlam();
            if (Anim != null) Anim.Release();
            if (ai != null) ai.MovementLocked = false;
            if (!Enraged && Health != null && Health.CurrentHealth < Health.maxHealth / 2) Enrage();
            yield return Wait(Enraged ? 2.2f : 3.2f);
        }
    }

    // Пауза только по игровому времени.
    private IEnumerator Wait(float seconds)
    {
        while (seconds > 0f)
        {
            if (Health == null || Health.isDead) yield break;
            if (Running) seconds -= Time.deltaTime;
            yield return null;
        }
    }

    private void Enrage()
    {
        Enraged = true;
        Vector3 feet = Ground(transform.position);
        EnemyFx.Ring(feet, 6f, Accent, 0.6f, 0.45f);
        EnemyFx.LightFlash(feet + Vector3.up * 2f, Accent, 10f, 3f, 0.5f);
        ScreenShake.AddAt(feet, 0.45f);
        GameAudio.Play(Sfx.Explosion, 0.7f);
        if (Anim != null) { Anim.Hold(ModelWalkAnimator.Pose.Swell, 0.8f); Anim.Flash(Accent * 1.6f, 0.4f); }
        if (GameSession.Instance != null) GameSession.Instance.Announce(GameText.Source("boss.phase2.mini"), 2.5f);
    }

    private void FaceTo(Vector3 point)
    {
        Vector3 d = point - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
    }

    // Бросок камней: Громила вырывает камни и швыряет по дуге в круги вокруг героя (во второй фазе — больше).
    private IEnumerator Boulders()
    {
        Transform target = CoopMode.NearestAlive(transform.position);
        if (target == null) yield break;
        if (ai != null) ai.MovementLocked = true;
        int count = Enraged ? 5 : 3;
        var spots = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            Vector2 around = i == 0 ? Vector2.zero : Random.insideUnitCircle * 5f;
            spots[i] = Ground(target.position + new Vector3(around.x, 0f, around.y));
        }
        FaceTo(target.position);
        Telegraphing = true;
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Windup, 2f);
        var marks = new GameObject[count];
        for (int i = 0; i < count; i++) marks[i] = Mark(spots[i], 2.2f);
        yield return Wait(0.6f);
        if (Anim != null) { Anim.Release(); Anim.Fire(); }
        Vector3 hand = transform.position + transform.forward * 1.5f + Vector3.up * 2.5f;
        for (int i = 0; i < count; i++) MortarShell.Launch(hand, spots[i], 1f, 2.2f);
        GameAudio.Play(Sfx.Throw, 0.9f);
        yield return Wait(1f);
        Telegraphing = false;
        Attacks++;
        for (int i = 0; i < count; i++)
        {
            if (marks[i] != null) Destroy(marks[i]);
            CoopMode.DamageHeroesWithin(spots[i], 2.4f, RockDamage, HeroDamageCause.Meteor);
        }
        ScreenShake.AddAt(target.position, 0.3f);
    }

    // Рёв: Громила встаёт на дыбы, волна отбрасывает героя, рядом выбегают враги, а ближние ускоряются.
    private IEnumerator Roar()
    {
        if (ai != null) ai.MovementLocked = true;
        Vector3 centre = Ground(transform.position);
        Telegraphing = true;
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Swell, 2f);
        float left = 0.8f;
        while (left > 0f)
        {
            if (Running) left -= Time.deltaTime;
            Circle(centre, 3.5f, Mathf.Lerp(0.1f, 0.35f, 1f - left / 0.8f));
            yield return null;
            if (Health == null || Health.isDead) { warning.enabled = false; yield break; }
        }
        warning.enabled = false;
        Telegraphing = false;
        Attacks++;
        if (Anim != null) Anim.Fire();
        EnemyFx.Ring(centre, 7f, Accent, 0.5f, 0.4f);
        EnemyFx.Ring(centre, 4f, Color.white, 0.35f, 0.2f);
        ScreenShake.AddAt(centre, 0.55f);
        GameAudio.Play(Sfx.Explosion, 0.8f);
        CoopMode.DamageHeroesWithin(centre, 3.8f, Mathf.RoundToInt(SlamDamage * 0.6f), HeroDamageCause.BossSlam);
        foreach (EnemyAI enemy in EnemyAI.All)
            if (enemy != null && enemy != ai && WeaponTargets.Flat(enemy.transform.position, centre) < 14f) enemy.Boost(1.45f, 4f);
        WaveManager waves = FindObjectOfType<WaveManager>();
        int count = Enraged ? 8 : 5;
        for (int i = 0; waves != null && i < count; i++)
        {
            float a = i * Mathf.PI * 2f / count;
            Vector3 spot = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4.5f;
            if (ArenaLayout.Instance != null && !ArenaLayout.Instance.IsWalkable(spot, 0.5f)) continue;
            EnemyFx.Puff(spot + Vector3.up * 0.4f, new Color(0.7f, 0.62f, 0.48f), 0.7f, 0.5f, Vector3.up);
            waves.SpawnMinionAt("Enemy_Fast", spot, 0.8f);
        }
        yield return Wait(0.4f);
    }

    // Двойной удар: сначала круг перед собой, сразу за ним — под героем.
    private IEnumerator DoubleSlam()
    {
        Transform target = CoopMode.NearestAlive(transform.position);
        if (target == null) yield break;
        FaceTo(target.position);
        Vector3 front = transform.position + transform.forward * 3.2f;
        yield return SlamAt(Ground(front), 3.4f, 0.75f);
        if (target != null) yield return SlamAt(Ground(target.position), 3.4f, 0.55f);
    }

    private IEnumerator SlamAt(Vector3 centre, float radius, float telegraph)
    {
        if (ai != null) ai.MovementLocked = true;
        FaceTo(centre);
        Telegraphing = true;
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Windup, 2f);
        float left = telegraph;
        while (left > 0f)
        {
            if (Running) left -= Time.deltaTime;
            Circle(centre, radius, Mathf.Lerp(0.12f, 0.45f, 1f - left / telegraph));
            yield return null;
            if (Health == null || Health.isDead) { warning.enabled = false; yield break; }
        }
        warning.enabled = false;
        Telegraphing = false;
        Attacks++;
        if (Anim != null) { Anim.Release(); Anim.Fire(); }
        CoopMode.DamageHeroesWithin(centre, radius + 0.3f, SlamDamage, HeroDamageCause.BossSlam);
        EnemyFx.Explosion(centre, radius * 0.8f, 0.6f, true);
        EnemyFx.Ring(centre, radius, new Color(1f, 0.55f, 0.25f), 0.4f, 0.4f);
        GameAudio.Play(Sfx.Explosion, 0.8f);
        ScreenShake.AddAt(centre, 0.4f);
        yield return Wait(0.2f);
    }

    // Отдельная метка-круг на земле (камни: их несколько одновременно).
    private GameObject Mark(Vector3 centre, float radius)
    {
        var go = new GameObject("Mini boss mark", typeof(LineRenderer));
        LineRenderer line = go.GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = 40;
        line.widthMultiplier = 0.14f;
        line.sharedMaterial = warningMaterial;
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

    private Vector3 Ground(Vector3 p) { return new Vector3(p.x, GroundHeight.At(p) + 0.08f, p.z); }

    // Удар по земле: стоит, красный круг вокруг себя, затем урон всем героям внутри, взрыв и тряска.
    private IEnumerator Slam()
    {
        if (ai != null) ai.MovementLocked = true;
        Vector3 centre = Ground(transform.position);
        Telegraphing = true;
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Windup, 2f);
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
        if (Anim != null) { Anim.Release(); Anim.Fire(); }
        CoopMode.DamageHeroesWithin(centre, SlamRadius + 0.3f, SlamDamage, HeroDamageCause.BossSlam);
        EnemyFx.Explosion(centre, SlamRadius * 0.8f, 0.7f, true);
        EnemyFx.Ring(centre, SlamRadius, new Color(1f, 0.55f, 0.25f), 0.45f, 0.45f);
        GameAudio.Play(Sfx.Explosion, 0.9f);
        ScreenShake.AddAt(centre, 0.5f);
        yield return Wait(0.35f);
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
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Windup, 2f);
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
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Dash, 1.5f);
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
        yield return Wait(0.4f);
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
            ExperiencePickup.Spawn(at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.4f, 15, merge: false);
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
