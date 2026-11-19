using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Модификатор элиты «Взрывной» (10.11.2026, выбор владельца; отличается только цветом — оранжевая корона
// и пульсирующее оранжевое свечение). После смерти на месте тела растёт красно-оранжевый круг,
// через Delay секунд — взрыв: бьёт героя и врагов внутри. Добивать вплотную опасно.
public class EliteExplosive : MonoBehaviour
{
    public const float Radius = 3.5f, Delay = 1.2f, Chance = 0.5f;
    public const int HeroDamage = 25, EnemyDamage = 80;
    public static readonly Color Tone = new Color(1f, 0.35f, 0.1f);

    private Light glow;
    private Health health;

    private void Start()
    {
        health = GetComponent<Health>();
        if (health != null) health.Died += OnDied;
        GameObject lightObject = new GameObject("Explosive glow");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = Vector3.up;
        glow = lightObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = Tone;
        glow.range = 5f;
        glow.intensity = 2f;
        glow.shadows = LightShadows.None;
    }

    private void Update()
    {
        if (glow != null) glow.intensity = 1.2f + 1.8f * Mathf.Abs(Mathf.Sin(Time.time * 5f));
    }

    private void OnDestroy()
    {
        if (health != null) health.Died -= OnDied;
    }

    private void OnDied(Health dead)
    {
        GameObject host = new GameObject("Elite blast");
        host.AddComponent<EliteBlast>().Begin(dead.transform.position);
    }
}

// Отложенный взрыв «Взрывной» элиты: живёт отдельно от тела, которое уничтожается сразу после смерти.
public class EliteBlast : MonoBehaviour
{
    private static readonly List<Health> buffer = new List<Health>();
    public bool Exploded { get; private set; }

    public void Begin(Vector3 position)
    {
        transform.position = position;
        StartCoroutine(Run(position));
    }

    private IEnumerator Run(Vector3 position)
    {
        Vector3 ground = new Vector3(position.x, GroundHeight.At(position), position.z);
        Material fillMaterial = EnemyFx.SeeThrough(EliteExplosive.Tone, 0.28f);
        TelegraphFill fill = EnemyFx.Fill(ground, EliteExplosive.Radius, EliteExplosive.Delay, fillMaterial);
        EnemyFx.Ring(ground, EliteExplosive.Radius, EliteExplosive.Tone, EliteExplosive.Delay, 0.25f);
        yield return new WaitForSeconds(EliteExplosive.Delay);
        if (fill != null) Destroy(fill.gameObject);
        Destroy(fillMaterial);
        Exploded = true;
        GameSession session = GameSession.Instance;
        if (session == null || session.State == GameRunState.Playing)
        {
            WeaponTargets.Collect(buffer);
            foreach (Health health in buffer)
                if (health != null && !health.isDead &&
                    WeaponTargets.Flat(ground, health.transform.position) <= EliteExplosive.Radius + WeaponTargets.BodyRadius(health) * 0.5f)
                    health.TakeDamage(EliteExplosive.EnemyDamage);
            CoopMode.DamageHeroesWithin(ground, EliteExplosive.Radius, EliteExplosive.HeroDamage, HeroDamageCause.EliteExplosion);   // все герои (кооператив)
        }
        EnemyFx.Explosion(ground, EliteExplosive.Radius * 0.8f, 0.7f);
        GameAudio.Play(Sfx.Explosion, 0.8f);
        Destroy(gameObject);
    }
}
