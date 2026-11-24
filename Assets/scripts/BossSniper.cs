using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Грозовой снайпер (12.11.2026; 19.11.2026 — луч-маятник, ползущие молнии, тройной луч, позы атак): BossController.
public partial class BossController
{
    private float nextTeleport;
    private static readonly Color Storm = new Color(0.55f, 0.85f, 1f);

    // Держится в 12–18 м; если герой подошёл ближе 6 м — телепорт подальше (раз в 6 с).
    private void KeepDistance(Vector3 delta)
    {
        float distance = delta.magnitude;
        if (distance < 6f && Time.time >= nextTeleport) { StartCoroutine(Teleport()); return; }
        Vector3 goal;
        if (distance > 18f) goal = target.position;
        else if (distance < 12f) goal = transform.position - delta.normalized * 4f;
        else return;
        Vector3 desired = Vector3.MoveTowards(transform.position, new Vector3(goal.x, transform.position.y, goal.z), 2.8f * SlowEffect.MovementFactor(this) * Time.deltaTime);
        transform.position = ArenaLayout.Instance.ResolveMovement(transform.position, desired, Radius);
    }

    private IEnumerator Teleport()
    {
        nextTeleport = Time.time + (Enraged ? 4.5f : 6f);
        attacking = true;
        Vector3 from = transform.position;
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Blink, 0.6f);
        LightningBolt.Impact(Ground(from) + Vector3.up, Storm, 1.4f);
        GameAudio.Play(Sfx.Arc, 0.8f);
        yield return Wait(0.35f);
        Vector3 away = (from - target.position);
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
        Vector3 spot = from;
        for (int i = 0; i < 12; i++)
        {
            Vector3 candidate = target.position + Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * away * Random.Range(13f, 16f);
            if (ArenaLayout.Instance == null || ArenaLayout.Instance.IsWalkable(candidate, Radius)) { spot = candidate; break; }
        }
        spot.y = from.y;
        transform.position = spot;
        if (Anim != null) { Anim.Release(); Anim.PopIn(0f, 0.25f); }
        LightningBolt.Create(spot + Vector3.up * 14f, Ground(spot) + Vector3.up * 0.5f, Storm, 0.6f, 0.3f, 3);
        LightningBolt.Impact(Ground(spot) + Vector3.up, Storm, 1.4f);
        ScreenShake.AddAt(spot, 0.25f);
        attacking = false;
    }

    // Удары молний: 5 (в ярости 8) кругов, один под героем; через 1,2 с — молния с неба и урон в круге.
    private IEnumerator SniperStrikes()
    {
        if (target == null) yield break;
        const float radius = 2.3f;
        int count = Enraged ? 8 : 5;
        var spots = new Vector3[count];
        var marks = new GameObject[count];
        for (int i = 0; i < count; i++)
        {
            Vector2 around = i == 0 ? Vector2.zero : Random.insideUnitCircle * 6.5f;
            spots[i] = Ground(target.position + new Vector3(around.x, 0f, around.y));
            marks[i] = Ring(spots[i], radius, Storm, 0.14f);
        }
        Telegraphing = true;
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Cast, 2f);
        yield return Wait(Enraged ? 1f : 1.2f);
        Telegraphing = false;
        if (Anim != null) { Anim.Release(); Anim.Fire(); }
        for (int i = 0; i < count; i++)
        {
            if (marks[i] != null) Destroy(marks[i]);
            LightningBolt.Create(spots[i] + Vector3.up * 16f, spots[i] + Vector3.up * 0.2f, Storm, 0.7f, 0.3f, 3);
            LightningBolt.Impact(spots[i] + Vector3.up * 0.3f, Storm, 1.2f);
            CoopMode.DamageHeroesWithin(spots[i], radius, slamDamage, HeroDamageCause.Lightning);
        }
        GameAudio.Play(Sfx.Arc, 1f);
        ScreenShake.AddAt(target.position, 0.3f);
    }

    // Прицельный луч: 1 с следит за героем тонкой линией, 0,4 с замирает (толще), затем выстрел вдоль неё.
    private IEnumerator SniperBeam()
    {
        if (target == null) yield break;
        Vector3 direction = ToTarget();
        Telegraphing = true;
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Aim, 2f);
        float aim = 1.4f;
        while (aim > 0f)
        {
            if (Health == null || Health.isDead) yield break;
            if (Running)
            {
                aim -= Time.deltaTime;
                if (aim > 0.4f) direction = ToTarget();
                Face(direction);
                Line(transform.position, transform.position + direction * 32f, aim > 0.4f ? 0.12f : 0.5f);
            }
            yield return null;
        }
        warning.enabled = false;
        Telegraphing = false;
        if (Anim != null) { Anim.Release(); Anim.Fire(); }
        FireBeam(direction, dashDamage + 10);
    }

    private void FireBeam(Vector3 direction, int damage)
    {
        Vector3 start = transform.position + Vector3.up * 0.5f, end = start + direction * 32f;
        BeamFlash.Create(start, end, Storm, 0.9f, 0.25f);
        BeamFlash.Create(start, end, Color.white, 0.35f, 0.3f);
        LightningBolt.Impact(end, Storm, 1f);
        GameAudio.Play(Sfx.Rail, 1f);
        ScreenShake.AddAt(start, 0.2f);
        foreach (PlayerHealth hero in CoopMode.Heroes)
            if (hero != null && !hero.IsDead && SpecialWeapons.SegmentDistance(start, end, hero.transform.position) < 1.4f)
                hero.TakeDamage(damage, HeroDamageCause.Beam);
    }

    // Вторая фаза: три луча веером, прицел общий.
    private IEnumerator SniperTripleBeam()
    {
        if (target == null) yield break;
        Vector3 direction = ToTarget();
        Face(direction);
        Telegraphing = true;
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Aim, 2f);
        var lines = new LineRenderer[3];
        for (int i = 0; i < 3; i++) lines[i] = TempLine(transform.position, transform.position + Quaternion.Euler(0f, (i - 1) * 20f, 0f) * direction * 32f, 0.14f, Storm, 2f);
        float aim = 1.1f;
        while (aim > 0f)
        {
            if (Health == null || Health.isDead) yield break;
            if (Running) aim -= Time.deltaTime;
            foreach (LineRenderer line in lines) if (line != null) line.widthMultiplier = aim > 0.35f ? 0.14f : 0.5f;
            yield return null;
        }
        foreach (LineRenderer line in lines) if (line != null) Destroy(line.gameObject);
        Telegraphing = false;
        if (Anim != null) { Anim.Release(); Anim.Fire(); }
        for (int i = 0; i < 3; i++) FireBeam(Quaternion.Euler(0f, (i - 1) * 20f, 0f) * direction, dashDamage);
        recoveryBonus = HeavyRecovery;
    }

    // Луч-маятник: метит линией, затем луч поворачивается на 200° вокруг снайпера (перепрыгнуть рывком или уйти дальше 26 м).
    private IEnumerator SniperSweep()
    {
        if (target == null) yield break;
        float sweep = 200f, length = 26f;
        float sign = Random.value < 0.5f ? 1f : -1f;
        Vector3 direction = Quaternion.Euler(0f, -sign * sweep * 0.5f, 0f) * ToTarget();
        Face(direction);
        Telegraphing = true;
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Aim, 4f);
        float left = 0.8f;
        while (left > 0f)
        {
            if (Health == null || Health.isDead) yield break;
            if (Running) left -= Time.deltaTime;
            Line(transform.position, transform.position + direction * length, 0.12f);
            yield return null;
        }
        Telegraphing = false;
        if (Anim != null) Anim.Fire();
        var hitAt = new Dictionary<PlayerHealth, float>();
        float duration = Enraged ? 1.7f : 2.2f, turned = 0f, nextSpark = 0f;
        GameAudio.Play(Sfx.Rail, 0.8f);
        while (turned < sweep)
        {
            if (Health == null || Health.isDead) yield break;
            if (Running)
            {
                float step = sweep / duration * Time.deltaTime;
                turned += step;
                direction = Quaternion.Euler(0f, sign * step, 0f) * direction;
                Face(direction);
                Vector3 start = transform.position + Vector3.up * 0.5f, end = start + direction * length;
                Line(transform.position, transform.position + direction * length, 0.75f);
                if ((nextSpark -= Time.deltaTime) <= 0f) { nextSpark = 0.1f; LightningBolt.Impact(Ground(end) + Vector3.up * 0.3f, Storm, 0.6f, false); }
                foreach (PlayerHealth hero in CoopMode.Heroes)
                {
                    if (hero == null || hero.IsDead || SpecialWeapons.SegmentDistance(start, end, hero.transform.position) > 1.2f) continue;
                    if (hitAt.TryGetValue(hero, out float last) && Time.time - last < 0.5f) continue;
                    hitAt[hero] = Time.time;
                    hero.TakeDamage(Mathf.RoundToInt(dashDamage * 0.6f), HeroDamageCause.Beam);
                }
            }
            yield return null;
        }
        warning.enabled = false;
        if (Anim != null) Anim.Release();
        recoveryBonus = HeavyRecovery;
    }

    // Ползущие молнии: цепочка кругов от снайпера к герою, удары бегут по ней одна за другой.
    private IEnumerator SniperCreep()
    {
        if (target == null) yield break;
        Vector3 direction = ToTarget();
        Face(direction);
        float distance = Mathf.Clamp(HorizontalDistance(target.position, transform.position) + 4f, 10f, 24f);
        int count = Mathf.RoundToInt(distance / 2.4f);
        int lines = Enraged ? 3 : 1;
        var spots = new List<Vector3>();
        var marks = new List<GameObject>();
        for (int l = 0; l < lines; l++)
        {
            Vector3 dir = Quaternion.Euler(0f, (l - (lines - 1) * 0.5f) * 25f, 0f) * direction;
            for (int i = 1; i <= count; i++)
            {
                Vector3 spot = Ground(transform.position + dir * (i * 2.4f));
                spots.Add(spot);
                marks.Add(Ring(spot, 1.6f, Storm, 0.1f));
            }
        }
        Telegraphing = true;
        if (Anim != null) Anim.Hold(ModelWalkAnimator.Pose.Cast, 4f);
        yield return Wait(0.7f);
        Telegraphing = false;
        if (Anim != null) Anim.Fire();
        for (int i = 0; i < count; i++)
        {
            for (int l = 0; l < lines; l++)
            {
                int k = l * count + i;
                if (marks[k] != null) Destroy(marks[k]);
                LightningBolt.Create(spots[k] + Vector3.up * 12f, spots[k] + Vector3.up * 0.2f, Storm, 0.5f, 0.2f, 2);
                LightningBolt.Impact(spots[k] + Vector3.up * 0.3f, Storm, 0.8f, false);
                CoopMode.DamageHeroesWithin(spots[k], 1.8f, Mathf.RoundToInt(slamDamage * 0.8f), HeroDamageCause.Lightning);
            }
            if (i % 2 == 0) GameAudio.Play(Sfx.Arc, 0.5f);
            yield return Wait(0.12f);
        }
        if (Anim != null) Anim.Release();
    }
}
