using System.Collections.Generic;
using UnityEngine;

// Ветка «Липкий» пистолета (12.11.2026): попадание лепит на врага заряд, который взрывается через StickyDelay секунд.
// Ранг 2: если цель с зарядом погибла, заряды перескакивают на ближайшего врага. Ранг 3: до трёх зарядов на цели.
public class StickyCharge : MonoBehaviour
{
    private struct Charge { public float at; public int damage; }

    private readonly List<Charge> charges = new List<Charge>(3);
    private Health host;
    private int maxCharges = 1;
    private bool transfers;
    private bool transferred;
    private float nextBlink;
    private static readonly Color Glow = new Color(1f, .35f, .2f);
    private const float TransferRadius = 6f;

    public static void Attach(Health target, int hitDamage, int rank, HeroStats stats)
    {
        if (target == null || target.isDead) return;
        StickyCharge sticky = target.GetComponent<StickyCharge>();
        if (sticky == null) sticky = target.gameObject.AddComponent<StickyCharge>();
        sticky.Bind(target);
        sticky.maxCharges = WeaponElements.StickyMaxCharges(rank);
        sticky.transfers = rank >= 2;
        if (sticky.charges.Count >= sticky.maxCharges) return;
        int blast = Mathf.Max(1, Mathf.RoundToInt(hitDamage * WeaponElements.StickyShare));
        sticky.charges.Add(new Charge { at = Time.time + WeaponElements.StickyDelay, damage = blast });
    }

    private void Bind(Health target)
    {
        if (host == target) return;
        if (host != null) host.Died -= OnHostDied;
        host = target;
        host.Died += OnHostDied;
    }

    private void OnHostDied(Health target)
    {
        Transfer();
        Destroy(this);
    }

    private void OnDestroy()
    {
        if (host != null) host.Died -= OnHostDied;
    }

    private void Update()
    {
        if (host == null) { Destroy(this); return; }
        if (host.isDead) { Transfer(); Destroy(this); return; }
        if (Time.time >= nextBlink)
        {
            nextBlink = Time.time + .25f;
            EnemyFx.Puff(host.transform.position + Vector3.up * 1.4f, Glow, .12f * charges.Count + .06f, .2f, Vector3.up * .4f);
        }
        for (int i = charges.Count - 1; i >= 0; i--)
        {
            if (Time.time < charges[i].at) continue;
            int damage = charges[i].damage;
            charges.RemoveAt(i);
            Detonate(damage);
            if (host == null || host.isDead) { Transfer(); Destroy(this); return; }
        }
        if (charges.Count == 0) Destroy(this);
    }

    private void Detonate(int damage)
    {
        Vector3 center = host.transform.position;
        float radius = WeaponElements.StickyRadius;
        var targets = new List<Health>();
        WeaponTargets.Collect(targets);
        foreach (Health candidate in targets)
        {
            if (candidate == null || candidate.isDead) continue;
            if (WeaponTargets.Flat(center, candidate.transform.position) <= radius + WeaponTargets.BodyRadius(candidate) * 0.5f)
                candidate.TakeDamage(damage);
        }
        EnemyFx.Explosion(new Vector3(center.x, GroundHeight.At(center), center.z), radius * 0.8f, 0f, false);
        GameAudio.Play(Sfx.Explosion, 0.35f);
    }

    // Оставшиеся заряды переходят на ближайшего живого врага с тем же временем взрыва.
    private void Transfer()
    {
        if (transferred || !transfers || charges.Count == 0 || host == null) return;
        transferred = true;
        Vector3 from = host.transform.position;
        var targets = new List<Health>();
        WeaponTargets.Collect(targets);
        Health best = null;
        float bestDistance = TransferRadius;
        foreach (Health candidate in targets)
        {
            if (candidate == null || candidate == host || candidate.isDead) continue;
            StickyCharge existing = candidate.GetComponent<StickyCharge>();
            if (existing != null && existing.charges.Count >= Mathf.Max(existing.maxCharges, maxCharges)) continue;
            float distance = WeaponTargets.Flat(from, candidate.transform.position);
            if (distance < bestDistance) { bestDistance = distance; best = candidate; }
        }
        if (best == null) return;
        StickyCharge next = best.GetComponent<StickyCharge>();
        if (next == null) next = best.gameObject.AddComponent<StickyCharge>();
        next.Bind(best);
        next.maxCharges = Mathf.Max(next.maxCharges, maxCharges);
        next.transfers = true;
        foreach (Charge charge in charges)
            if (next.charges.Count < next.maxCharges) next.charges.Add(charge);
        charges.Clear();
        EnemyFx.LightFlash(best.transform.position + Vector3.up, Glow, 3f, 1.5f, .15f);
    }
}
