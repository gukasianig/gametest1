using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Королева роя (12.11.2026; 19.11.2026 — прыжок, кислотные лужи, спираль, позы атак): BossController.
public partial class BossController
{
    public const int QueenMinionLimit = 60;
    private readonly List<Health> queenMinions = new List<Health>();
    public int QueenMinionsAlive
    {
        get
        {
            queenMinions.RemoveAll(h => h == null || h.isDead);
            return queenMinions.Count;
        }
    }
    private static readonly Color Acid = new Color(0.55f, 1f, 0.3f);

    // Рой: зелёные метки вокруг королевы, враги выбегают 3 порциями через 0,9 с (по 6, в ярости по 8),
    // затем 2 с передышки — расчистить проход и пострелять в саму королеву (21.11.2026; раньше 18/26 разом).
    public const int BroodWaves = 3;
    public const float BroodWaveInterval = 0.9f, BroodRecovery = 2f;
    public static int BroodWaveSize(bool enraged) { return enraged ? 8 : 6; }

    private IEnumerator QueenBrood()
    {
        WaveManager waves = FindObjectOfType<WaveManager>();
        int count = Mathf.Min(BroodWaveSize(Enraged) * BroodWaves, QueenMinionLimit - QueenMinionsAlive);
        if (waves == null || count <= 0) { yield return QueenAcid(); yield break; }   // роя достаточно — плевок
        var spots = new Vector3[count];
        var marks = new GameObject[count];
        for (int i = 0; i < count; i++)
        {
            float a = i * Mathf.PI * 2f / count + Random.Range(-0.2f, 0.2f);
            Vector3 spot = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(3.5f, 5f);
            if (ArenaLayout.Instance != null && !ArenaLayout.Instance.IsWalkable(spot, 0.5f)) spot = transform.position;
            spots[i] = Ground(spot);
            marks[i] = Ring(spots[i], 0.9f, Acid, 0.12f);
        }
        Telegraphing = true;
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Cast, 1.5f);
        yield return Wait(0.9f);
        Telegraphing = false;
        if (Anim != null) { Anim.Release(); Anim.Fire(); }
        // Метки перемешаны по кругу: каждая порция выбегает с разных сторон, а не одним сектором.
        // Порция k берёт каждую третью метку (k, k+3, k+6…).
        for (int wave = 0; wave < BroodWaves && wave < count; wave++)
        {
            if (wave > 0)
            {
                yield return Wait(BroodWaveInterval);
                if (Health == null || Health.isDead) break;
            }
            for (int i = wave; i < count; i += BroodWaves) SpawnBroodAt(waves, spots, marks, i);
            GameAudio.Play(Sfx.Deploy, 0.6f);
        }
        foreach (GameObject mark in marks) if (mark != null) Destroy(mark);
        recoveryBonus = BroodRecovery;
    }

    private void SpawnBroodAt(WaveManager waves, Vector3[] spots, GameObject[] marks, int i)
    {
        if (marks[i] != null) Destroy(marks[i]);
        marks[i] = null;
        EnemyFx.Blobs(spots[i] + Vector3.up * 0.3f, Acid, 6, 3f, 0.12f, 0.4f, spots[i].y);
        if (QueenMinionsAlive >= QueenMinionLimit || waves == null) return;
        GameObject minion = waves.SpawnMinionAt("Enemy_Fast", spots[i], 0.75f);
        if (minion != null && minion.TryGetComponent(out Health minionHealth)) queenMinions.Add(minionHealth);
    }

    // Кислотный залп: веер из 7 (в ярости 9) кислотных сгустков.
    private IEnumerator QueenAcid()
    {
        if (target == null) yield break;
        Vector3 direction = ToTarget();
        Face(direction);
        int side = Enraged ? 4 : 3;
        DrawFan(direction, side, 10f, 18f);
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Aim, 1.5f);
        yield return Wait(0.9f);
        warning.enabled = false;
        Telegraphing = false;
        if (Anim != null) { Anim.Release(); Anim.Fire(); }
        Volley(direction, side, 10f, 0f, EnemyProjectile.Style.Acid);
        GameAudio.Play(Sfx.Throw, 0.8f);
    }

    // Прыжок: круг под героем, королева взлетает по дуге и обрушивается; при приземлении — удар и выводок.
    private IEnumerator QueenPounce()
    {
        if (target == null) yield break;
        Vector3 spot = Ground(target.position);
        if (ArenaLayout.Instance != null && !ArenaLayout.Instance.IsWalkable(spot, Radius)) spot = Ground(transform.position + ToTarget() * 6f);
        Face(spot - transform.position);
        Circle(spot, 4f);
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Windup, 1.5f);
        yield return Wait(Enraged ? 0.7f : 0.9f);
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Dash, 1f);
        Vector3 from = transform.position;
        float t = 0f;
        while (t < 1f)
        {
            if (Health == null || Health.isDead) { airborne = 0f; yield break; }
            if (Running) t = Mathf.Min(1f, t + Time.deltaTime / 0.55f);
            Vector3 p = Vector3.Lerp(from, spot, t);
            transform.position = new Vector3(p.x, transform.position.y, p.z);
            airborne = 5f * 4f * t * (1f - t);
            yield return null;
        }
        airborne = 0f;
        warning.enabled = false;
        Telegraphing = false;
        if (Anim != null) { Anim.Release(); Anim.Fire(); }
        CoopMode.DamageHeroesWithin(spot, 4.3f, slamDamage, HeroDamageCause.BossSlam);
        EnemyFx.Explosion(spot, 3.5f, 0.6f, true);
        EnemyFx.Blobs(spot + Vector3.up * 0.4f, Acid, 18, 6f, 0.16f, 0.6f, spot.y);
        EnemyFx.Ring(spot, 4f, Acid, 0.45f, 0.4f);
        EnemyFx.GroundSplat(spot, 2.5f, Acid * 0.6f, 3f);
        ScreenShake.AddAt(spot, 0.55f);
        GameAudio.Play(Sfx.Explosion, 0.9f);
        WaveManager waves = FindObjectOfType<WaveManager>();
        int brood = Mathf.Min(Enraged ? 6 : 4, QueenMinionLimit - QueenMinionsAlive);
        for (int i = 0; waves != null && i < brood; i++)
        {
            float a = i * Mathf.PI * 2f / Mathf.Max(1, brood);
            GameObject minion = waves.SpawnMinionAt("Enemy_Fast", spot + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 3f, 0.75f);
            if (minion != null && minion.TryGetComponent(out Health minionHealth)) queenMinions.Add(minionHealth);
        }
        yield return Wait(0.4f);
    }

    // Кислотные лужи: сгустки по дуге в круги вокруг героя, на месте падения — лужа на 6 с.
    private IEnumerator QueenAcidPools()
    {
        if (target == null) yield break;
        int count = Enraged ? 6 : 4;
        var spots = new Vector3[count];
        var marks = new GameObject[count];
        for (int i = 0; i < count; i++)
        {
            Vector2 around = i == 0 ? Vector2.zero : Random.insideUnitCircle * 6f;
            spots[i] = Ground(target.position + new Vector3(around.x, 0f, around.y));
            marks[i] = Ring(spots[i], 2.4f, Acid, 0.12f);
        }
        Face(ToTarget());
        Telegraphing = true;
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Aim, 1.5f);
        yield return Wait(0.6f);
        if (Anim != null) { Anim.Release(); Anim.Fire(); }
        Vector3 mouth = transform.position + transform.forward * 2f + Vector3.up;
        for (int i = 0; i < count; i++) MortarShell.Launch(mouth, spots[i], 0.9f, 2.4f);
        GameAudio.Play(Sfx.Throw, 0.9f);
        yield return Wait(0.9f);
        Telegraphing = false;
        for (int i = 0; i < count; i++)
        {
            if (marks[i] != null) Destroy(marks[i]);
            CoopMode.DamageHeroesWithin(spots[i], 2.4f, projectileDamage, HeroDamageCause.Acid);
            BossHazard.Spawn(spots[i], 2.4f, 6f, Mathf.Max(1, projectileDamage / 3), Acid, HeroDamageCause.Acid);
        }
    }

    // Спираль: королева кружит кислотные сгустки двумя (в ярости тремя) рукавами.
    private IEnumerator QueenSpiral()
    {
        if (target == null) yield break;
        int arms = Enraged ? 3 : 2;
        Circle(Ground(transform.position), 3f);
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Cast, 3f);
        yield return Wait(0.6f);
        warning.enabled = false;
        Telegraphing = false;
        float angle = Random.Range(0f, 360f);
        for (int shot = 0; shot < 20; shot++)
        {
            for (int arm = 0; arm < arms; arm++)
            {
                Vector3 direction = Quaternion.Euler(0f, angle + arm * 360f / arms, 0f) * Vector3.forward;
                EnemyProjectile.Create(transform.position + direction * 2f, direction, target, Mathf.RoundToInt(projectileDamage * 0.7f),
                    EnemyProjectile.Style.Acid, HeroDamageCause.BossProjectile);
            }
            if (Anim != null && shot % 4 == 0) Anim.Fire();
            if (shot % 3 == 0) GameAudio.Play(Sfx.Throw, 0.4f);
            angle += 17f;
            yield return Wait(0.08f);
        }
        if (Anim != null) Anim.Release();
    }
}
