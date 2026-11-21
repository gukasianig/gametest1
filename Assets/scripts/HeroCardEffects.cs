using System.Collections.Generic;
using UnityEngine;

// Карточки 19.11.2026: формулы новых карточек и эффекты вокруг героя (ответный импульс, импульсная аура,
// взрывной рывок, «Последний шанс», уклонение). Добавляется к герою из PlayerProgression.Start.
public class HeroCardEffects : MonoBehaviour
{
    // --- Формулы (их же показывают карточки).
    public static int SplitFragments(int rank) { return rank > 0 ? 1 + rank : 0; }      // 2/3/4 осколка
    public const float SplitShare = 0.35f;                                                  // урон осколка от пули
    public static float BurnShare(int rank) { return 0.05f * rank; }                        // урона попадания в секунду, 3 с
    public static float PoisonShare(int rank) { return 0.035f * rank; }                     // за стак, до 3 стаков по 4 с
    public static float FrostSlow(int rank) { return rank > 0 ? 0.1f + 0.08f * rank : 0f; } // замедление на 2 с
    public const float ShockShare = 0.25f, ShockInterval = 0.5f;
    public static float RetaliateShare(int rank) { return rank > 0 ? 1f + 0.75f * rank : 0f; }
    public const float RetaliateCooldown = 4f, RetaliateRadius = 4f;
    public static float AuraShare(int rank) { return rank > 0 ? 0.4f + 0.25f * rank : 0f; }
    public static float AuraRadius(int rank) { return 3f + 0.25f * rank; }
    public const float AuraInterval = 2f;
    public static int ChargedEvery(int rank) { return rank > 0 ? 7 - rank : 0; }            // каждый 6/5/4-й выстрел
    public const float ChargedDamage = 2.5f;
    public const float BlastDashShare = 2f, BlastDashRadius = 3.5f;
    public const float LastChanceHealth = 0.3f;

    private static readonly Color AuraColor = new Color(0.35f, 0.9f, 1f);
    private static readonly Color BlastColor = new Color(1f, 0.6f, 0.2f);
    private static readonly List<Health> targets = new List<Health>();

    private HeroStats stats;
    private HeroStats Stats => stats ?? (stats = CombatStats.Of(this));
    private PlayerHealth health;
    private AutoShoter shooter;
    private PlayMovement movement;
    private float nextRetaliate, nextAura;
    private bool wasDashing;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        shooter = GetComponent<AutoShoter>();
        movement = GetComponent<PlayMovement>();
    }

    private void OnEnable() { if (health != null) health.Damaged += OnDamaged; }
    private void OnDisable() { if (health != null) health.Damaged -= OnDamaged; }

    // Урон одного «выстрела» героя: основа для импульсов (у дробовика учитывается часть дробин).
    private float ShotDamage
    {
        get
        {
            if (shooter == null) return 10f;
            return shooter.ExactDamage * (1f + 0.5f * Mathf.Max(0, shooter.CurrentPelletCount - 1));
        }
    }

    private void Update()
    {
        bool dashing = movement != null && movement.Dashing;
        if (!SpecialWeapons.Running || health == null || health.IsDead) { wasDashing = dashing; return; }
        int aura = Stats.PulseAura;
        if (aura > 0 && Time.time >= nextAura)
        {
            nextAura = Time.time + AuraInterval;
            Burst(AuraRadius(aura), ShotDamage * AuraShare(aura), 4f, AuraColor, false);
        }
        if (wasDashing && !dashing && Stats.BlastDash)
            Burst(BlastDashRadius, ShotDamage * BlastDashShare, 12f, BlastColor, true);
        wasDashing = dashing;
    }

    private void OnDamaged(int raw, int dealt)
    {
        int rank = Stats.Retaliate;
        if (rank <= 0 || health == null || health.CurrentHP <= 0 || Time.time < nextRetaliate) return;
        nextRetaliate = Time.time + RetaliateCooldown;
        Burst(RetaliateRadius, ShotDamage * RetaliateShare(rank), 14f, BlastColor, false);
    }

    // Удар по кругу: урон (через оружие героя — крит и стихии работают) и отбрасывание.
    private void Burst(float radius, float damage, float push, Color color, bool explosion)
    {
        Vector3 centre = transform.position;
        WeaponTargets.Collect(targets);
        int amount = Mathf.Max(1, Mathf.RoundToInt(damage));
        foreach (Health target in targets)
        {
            if (target == null || target.isDead) continue;
            if (WeaponTargets.Flat(centre, target.transform.position) > radius + WeaponTargets.BodyRadius(target) * 0.5f) continue;
            if (push > 0f) Push(target, centre, push);
            if (damage > 0f)
            {
                if (shooter != null && shooter.ElementAttack != null) shooter.ElementAttack.Hit(target, amount);
                else target.TakeDamage(amount);
            }
        }
        Vector3 feet = new Vector3(centre.x, GroundHeight.At(centre) + 0.05f, centre.z);
        EnemyFx.Ring(feet, radius, color, 0.4f, 0.18f);
        EnemyFx.LightFlash(feet + Vector3.up, color, radius + 2f, 1.2f, 0.2f);
        if (explosion) EnemyFx.Explosion(feet, radius * 0.6f, 0f, false);
        GameAudio.Play(Sfx.Zap, 0.35f);
    }

    private static void Push(Health target, Vector3 from, float force)
    {
        EnemyAI enemy = target.GetComponent<EnemyAI>();
        if (enemy == null) return;
        Vector3 away = Vector3.ProjectOnPlane(target.transform.position - from, Vector3.up);
        if (away.sqrMagnitude < 0.001f) away = Vector3.forward;
        float resistance = target.name.StartsWith("Elite") ? 0.5f : 1f;
        enemy.ApplyHeroImpulse(away.normalized * (force * resistance), 0.3f);
    }

    // --- Стихийные карточки: вызываются из WeaponElementAttack.Hit после основного урона.
    public static void ElementHit(HeroStats stats, Health target, Vector3 position, int damage, ref float nextChain)
    {
        if (stats == null || target == null || damage <= 0) return;
        if (stats.ShockRank > 0 && Time.time >= nextChain)
        {
            nextChain = Time.time + ShockInterval;
            WeaponElementAttack.ChainFrom(target, position, damage, stats.ShockRank, ShockShare);
        }
        if (target.isDead) return;
        if (stats.BurnRank > 0)
            ElementStatus.Apply(target, WeaponElement.Fire, damage * BurnShare(stats.BurnRank) * (stats.BurningShot ? 1.75f : 1f));
        if (stats.PoisonRank > 0) ElementStatus.Apply(target, WeaponElement.Poison, damage * PoisonShare(stats.PoisonRank));
        if (stats.FrostRank > 0) SlowEffect.Apply(target, 1f - FrostSlow(stats.FrostRank), 2f);
    }

    // --- Защитные карточки (PlayerHealth.TakeDamage).
    public static void DodgeFx(PlayerHealth hero)
    {
        Vector3 at = hero.transform.position + Vector3.up * 1.1f;
        EnemyFx.Puff(at, new Color(0.75f, 0.95f, 1f), 0.25f, 0.35f, Vector3.up * 1.2f);
        EnemyFx.Ring(new Vector3(at.x, GroundHeight.At(at) + 0.05f, at.z), 1.1f, new Color(0.75f, 0.95f, 1f), 0.25f, 0.1f);
    }

    public static void LastChanceFx(PlayerHealth hero)
    {
        Vector3 centre = hero.transform.position;
        Vector3 feet = new Vector3(centre.x, GroundHeight.At(centre) + 0.05f, centre.z);
        Color gold = new Color(1f, 0.82f, 0.3f);
        EnemyFx.Ring(feet, 5f, gold, 0.6f, 0.3f);
        EnemyFx.LightFlash(feet + Vector3.up * 1.5f, gold, 8f, 3f, 0.5f);
        foreach (EnemyAI enemy in EnemyAI.All)
        {
            if (enemy == null || enemy.Health == null || enemy.Health.isDead) continue;
            if (WeaponTargets.Flat(centre, enemy.transform.position) <= 5f) Push(enemy.Health, centre, 16f);
        }
        GameAudio.Play(Sfx.LevelUp, 0.9f, 0f);
        GameSession session = GameSession.Instance;
        if (session != null) session.Announce(GameText.Source("cards.lastchance.announce"), 2f);
    }
}
