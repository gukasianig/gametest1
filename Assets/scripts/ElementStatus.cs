using System.Collections.Generic;
using UnityEngine;

// One bounded status component per target. Periodic damage never triggers on-hit effects again.
public class ElementStatus : MonoBehaviour
{
    private struct PoisonStack { public float dps, until; }
    private readonly List<PoisonStack> poison = new List<PoisonStack>(3);
    private Health health;
    private float fireDps, fireUntil, fraction, nextPoisonStack;

    public static void Apply(Health target, WeaponElement element, float dps)
    {
        if (target == null || target.isDead || dps <= 0f ||
            (element != WeaponElement.Fire && element != WeaponElement.Poison)) return;
        ElementStatus status = target.GetComponent<ElementStatus>();
        if (status == null) { status = target.gameObject.AddComponent<ElementStatus>(); status.health = target; }
        if (target.GetComponent<BossController>() != null) dps *= .5f;
        if (element == WeaponElement.Fire)
        {
            status.fireDps = Time.time < status.fireUntil ? Mathf.Max(status.fireDps, dps) : dps;
            status.fireUntil = Time.time + 3f;
        }
        else
        {
            status.poison.RemoveAll(stack => stack.until <= Time.time);
            // Rapid pellets refresh a stack instead of multiplying the same burst into three stacks.
            if (status.poison.Count > 0 && Time.time < status.nextPoisonStack)
            {
                int last = status.poison.Count - 1;
                status.poison[last] = new PoisonStack { dps = Mathf.Max(dps, status.poison[last].dps), until = Time.time + 4f };
            }
            else
            {
                if (status.poison.Count == 3) status.poison.RemoveAt(0);
                status.poison.Add(new PoisonStack { dps = dps, until = Time.time + 4f });
                status.nextPoisonStack = Time.time + .25f;
            }
        }
    }

    private void Update()
    {
        if (health == null || health.isDead) { Destroy(this); return; }
        if (!SpecialWeapons.Running) return;
        poison.RemoveAll(stack => stack.until <= Time.time);
        float dps = Time.time < fireUntil ? fireDps : 0f;
        foreach (PoisonStack stack in poison) dps += stack.dps;
        fraction += dps * Time.deltaTime;
        int amount = Mathf.FloorToInt(fraction);
        if (amount > 0) { fraction -= amount; health.TakeDamage(amount); }
        if (dps <= 0f && poison.Count == 0) Destroy(this);
    }
}
