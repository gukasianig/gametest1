using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grenade : MonoBehaviour
{
    public const float VisualLength = .6f;
    public float explosionRadius = 3f;
    public int damage = 20;
    public GameObject explosionEffect;

    public float delay = 2f;

    void Start()
    {
        ProjectileModels.Skin(gameObject, "grenade", VisualLength);
        Invoke(nameof(Explode), delay);
        // Заметность: яркий след и мигающий красный огонёк запала (раньше мелкий снаряд сливался с землёй).
        TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.35f;
        trail.widthMultiplier = 0.25f;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        trail.sharedMaterial = EnemyFx.Mat(new Color(1f, 0.6f, 0.2f));
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fuse = new GameObject("Grenade fuse").AddComponent<Light>();
        fuse.transform.SetParent(transform, false);
        fuse.type = LightType.Point;
        fuse.color = new Color(1f, 0.3f, 0.15f);
        fuse.range = 3f;
        fuse.shadows = LightShadows.None;
    }

    private Light fuse;
    private float age;

    void Explode()
    {
        GameAudio.Play(Sfx.Explosion);
        // Общий эффект взрыва вместо старого explosionEffect (растянутой сферы); поле оставлено ради сцены.
        Vector3 point = transform.position;
        EnemyFx.Explosion(new Vector3(point.x, GroundHeight.At(point), point.z), explosionRadius * 0.8f, 0.5f);

    Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);

    var damagedTargets = new HashSet<Health>();
    foreach (Collider col in hits)
    {
        Health hp = col.GetComponentInParent<Health>();
        if (hp != null && hp.CompareTag("Enemy") && damagedTargets.Add(hp))
        {
            hp.TakeDamage(damage);
        }
    }

    Destroy(gameObject);
    }
    void Update()
    {
        transform.Rotate(300 * Time.deltaTime, 0, 0);
        age += Time.deltaTime;
        // Мигание учащается к взрыву.
        if (fuse != null) fuse.intensity = 1.5f + 2.5f * Mathf.Max(0f, Mathf.Sin(age * Mathf.Lerp(10f, 40f, Mathf.Clamp01(age / Mathf.Max(0.1f, delay)))));
    }
}

