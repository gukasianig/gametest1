using UnityEngine;

// Анимация выстрела модели оружия в руке героя (вешает ModelSkins.ApplyHeroWeapon на SkinModel).
// PlayLight — лёгкая отдача остальных оружий (AutoShoter.PlayWeaponRecoil): слабый отскок и редкий дымок у дула.
// Play — полная анимация ракетницы (SpecialWeapons.Fire): модель отскакивает назад и задирает ствол, затем
// пружинисто возвращается; боеголовка в жерле (часть «Warhead» модели make_rocket_launcher.py) пропадает
// вместе с вылетевшей ракетой и через паузу въезжает обратно; у жерла вспышка и дым, из заднего сопла — струя дыма.
// Без части «Warhead» (старые модели Kenney) работают только отскок и дым.
public class WeaponFireAnimation : MonoBehaviour
{
    private Transform socket, warhead;
    private Renderer[] warheadRenderers;
    private Vector3 restPosition, warheadRest;
    private Quaternion restRotation;
    private float fired = -10f, kick, reloadDelay;
    private bool reloading;

    private const float KickBack = 0.09f;      // м назад в пространстве сокета
    private const float KickPitch = 10f;       // градусов вверх
    private const float RiseTime = 0.04f, SettleTime = 0.35f, ReloadSlide = 0.18f;

    public void Init(Transform weaponSocket)
    {
        socket = weaponSocket;
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        warhead = FindDeep(transform, "Warhead");
        if (warhead != null)
        {
            warheadRest = warhead.localPosition;
            warheadRenderers = warhead.GetComponentsInChildren<Renderer>();
        }
    }

    // strength 0..1 — сила отскока; reload — через сколько секунд боеголовка начинает возвращаться.
    public void Play(float strength, float reload)
    {
        fired = Time.time;
        kick = Mathf.Clamp01(strength);
        reloadDelay = Mathf.Max(0.1f, reload);
        if (warhead != null)
        {
            SetWarheadVisible(false);
            reloading = true;
        }
        Effects();
    }

    // Лёгкая отдача обычного оружия: тот же отскок слабее, без перезарядки и света; редкий дымок у дула.
    public void PlayLight(float strength, bool smoke)
    {
        fired = Time.time;
        kick = Mathf.Clamp01(strength);
        if (!smoke || Time.time - lastPuff < 0.15f) return;
        lastPuff = Time.time;
        Vector3 flat = FlatForward();
        Vector3 muzzle = ModelSkins.TryGetMuzzle(out Vector3 m) ? m : transform.position + Forward() * 0.3f;
        EnemyFx.Puff(muzzle + flat * 0.05f, new Color(0.62f, 0.6f, 0.62f), Random.Range(0.08f, 0.13f), Random.Range(0.22f, 0.32f),
            flat * Random.Range(0.8f, 1.4f) + Vector3.up * 0.3f);
    }

    private float lastPuff = -10f;

    private Vector3 FlatForward()
    {
        Vector3 forward = Forward();
        Vector3 flat = new Vector3(forward.x, 0f, forward.z);
        return flat.sqrMagnitude < 0.01f ? forward : flat.normalized;
    }

    private void LateUpdate()
    {
        float t = Time.time - fired;
        float curve = 0f;
        if (t < RiseTime) curve = t / RiseTime;
        else if (t < RiseTime + SettleTime)
        {
            // Возврат с маленьким перелётом вперёд.
            float p = (t - RiseTime) / SettleTime;
            curve = Mathf.Cos(p * Mathf.PI * 1.25f) * (1f - p) * (1f - p);
        }
        transform.localPosition = restPosition + Vector3.back * (KickBack * kick * curve);
        transform.localRotation = Quaternion.AngleAxis(-KickPitch * kick * curve, Vector3.right) * restRotation;

        if (reloading && warhead != null && t >= reloadDelay)
        {
            float p = Mathf.Clamp01((t - reloadDelay) / ReloadSlide);
            SetWarheadVisible(true);
            Vector3 inside = warhead.parent != null ? warhead.parent.InverseTransformVector(Forward() * -0.12f) : Vector3.zero;
            float eased = 1f - (1f - p) * (1f - p);
            warhead.localPosition = warheadRest + inside * (1f - eased);
            if (p >= 1f) reloading = false;
        }
    }

    private void OnDisable()
    {
        transform.localPosition = restPosition;
        transform.localRotation = restRotation;
        if (warhead != null)
        {
            warhead.localPosition = warheadRest;
            SetWarheadVisible(true);
            reloading = false;
        }
    }

    private Vector3 Forward() { return socket != null ? socket.forward : transform.forward; }

    private void SetWarheadVisible(bool on)
    {
        if (warheadRenderers == null) return;
        foreach (Renderer r in warheadRenderers) if (r != null) r.enabled = on;
    }

    // Вспышка и дым у жерла, струя дыма из заднего конца модели.
    private void Effects()
    {
        Vector3 forward = Forward();
        Vector3 flat = FlatForward();
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        float half = Mathf.Abs(b.extents.x * forward.x) + Mathf.Abs(b.extents.y * forward.y) + Mathf.Abs(b.extents.z * forward.z);
        Vector3 muzzle = ModelSkins.TryGetMuzzle(out Vector3 m) ? m : b.center + forward * half;
        Vector3 rear = b.center - forward * half;

        Color fire = new Color(1f, 0.62f, 0.18f);
        Color smoke = new Color(0.46f, 0.44f, 0.46f);
        EnemyFx.LightFlash(muzzle, fire, 5f, 3f, 0.12f);
        EnemyFx.Puff(muzzle + flat * 0.15f, fire, 0.35f, 0.12f, flat * 2f);
        for (int i = 0; i < 3; i++)
            EnemyFx.Puff(muzzle + Random.insideUnitSphere * 0.08f, smoke, Random.Range(0.18f, 0.28f), Random.Range(0.35f, 0.55f),
                flat * Random.Range(1f, 2.2f) + Random.insideUnitSphere * 0.4f);
        // Реактивная струя назад: короткая огненная вспышка и клубы дыма.
        EnemyFx.Puff(rear - flat * 0.1f, fire, 0.28f, 0.1f, -flat * 3f);
        for (int i = 0; i < 5; i++)
            EnemyFx.Puff(rear - flat * Random.Range(0.05f, 0.25f) + Random.insideUnitSphere * 0.06f, smoke, Random.Range(0.22f, 0.36f),
                Random.Range(0.45f, 0.8f), -flat * Random.Range(2.5f, 5f) + Random.insideUnitSphere * 0.6f + Vector3.up * 0.3f);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
