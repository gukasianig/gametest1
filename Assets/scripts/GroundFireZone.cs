using System.Collections.Generic;
using UnityEngine;

// Горящая земля: видимая часть — GroundFireVisual (набор GroundFireKit), здесь — размер, время жизни и урон.
// Урон считается по XZ-расстоянию до центра, коллайдеров нет: через огонь можно пройти.
public sealed class GroundFireZone : MonoBehaviour
{
    private const string PrefabPath = "Hazards/GroundFireVisual";
    private const float MinRadius = 1f, MaxRadius = 6f;
    private static GameObject prefab;
    private static bool prefabMissing;

    private readonly List<Health> buffer = new List<Health>();
    private GroundFireVisual visual;
    private System.Action<Health> onEnemy;
    private int playerDamage;
    private float tickSeconds = 0.5f, tick;
    private float growSpeed, targetRadius;
    private float burnUntil;
    private bool burning = true;
    private PlayerHealth player;

    public float Radius { get; private set; }
    public bool Burning => burning;

    // startRadius растёт до maxRadius за growSeconds; через lifetime секунд огонь гаснет (визуал догорает 2,5 с).
    // onEnemy вызывается раз в tickSeconds для каждого врага внутри; playerDamage > 0 — огонь жжёт и героя.
    public static GroundFireZone Spawn(Vector3 position, float startRadius, float maxRadius, float growSeconds,
        float lifetime, System.Action<Health> onEnemy, int playerDamage, float tickSeconds = 0.5f)
    {
        GameObject root = new GameObject("Ground Fire");
        // Неактивный корень: Awake визуала выполнится при включении, когда радиус уже задан.
        root.SetActive(false);
        root.transform.position = new Vector3(position.x, GroundHeight.At(position), position.z);
        GroundFireZone zone = root.AddComponent<GroundFireZone>();
        zone.Radius = Mathf.Clamp(startRadius, MinRadius, MaxRadius);
        zone.targetRadius = Mathf.Clamp(maxRadius, zone.Radius, MaxRadius);
        zone.growSpeed = growSeconds > 0.01f ? (zone.targetRadius - zone.Radius) / growSeconds : float.MaxValue;
        zone.burnUntil = Time.time + lifetime;
        zone.onEnemy = onEnemy;
        zone.playerDamage = playerDamage;
        zone.tickSeconds = Mathf.Max(0.1f, tickSeconds);
        zone.tick = zone.tickSeconds * 0.5f;
        zone.CreateVisual();
        root.SetActive(true);
        return zone;
    }

    private void CreateVisual()
    {
        if (prefab == null && !prefabMissing)
        {
            prefab = Resources.Load<GameObject>(PrefabPath);
            prefabMissing = prefab == null;
            if (prefabMissing) Debug.LogWarning("GroundFireZone: prefab Resources/" + PrefabPath + " not found; fire deals damage without a model.");
        }
        if (prefab == null) return;
        // Радиус задаётся до Awake (корень ещё выключен), чтобы первая сборка была сразу нужного размера.
        GameObject instance = Instantiate(prefab, transform.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
        visual = instance.GetComponent<GroundFireVisual>();
        if (visual != null) visual.SetRadius(Radius);
    }

    // Продлить горение и при желании подрастить огонь (огнемёт повторно поджигает то же место).
    public void Refresh(float lifetime, float growBy, float maxRadius)
    {
        if (!burning) return;
        burnUntil = Mathf.Max(burnUntil, Time.time + lifetime);
        targetRadius = Mathf.Clamp(Mathf.Max(targetRadius, Radius + growBy), MinRadius, Mathf.Min(maxRadius, MaxRadius));
        if (growSpeed <= 0f || float.IsInfinity(growSpeed)) growSpeed = 1.5f;
    }

    public void Extinguish()
    {
        if (!burning) return;
        burning = false;
        if (visual != null) visual.SetBurning(false);
        Destroy(gameObject, 2.7f);
    }

    private void Update()
    {
        if (!burning || Time.deltaTime <= 0f) return;
        if (Time.time >= burnUntil) { Extinguish(); return; }

        if (Radius < targetRadius)
        {
            Radius = Mathf.MoveTowards(Radius, targetRadius, growSpeed * Time.deltaTime);
            if (visual != null) visual.SetRadius(Radius);
        }

        GameSession session = GameSession.Instance;
        if (session != null && session.State != GameRunState.Playing) return;
        tick -= Time.deltaTime;
        if (tick > 0f) return;
        tick += tickSeconds;

        Vector3 centre = transform.position;
        if (onEnemy != null)
        {
            WeaponTargets.Collect(buffer);
            foreach (Health health in buffer)
                if (health != null && !health.isDead &&
                    WeaponTargets.Flat(centre, health.transform.position) <= Radius + WeaponTargets.BodyRadius(health) * 0.5f)
                    onEnemy(health);
        }
        if (playerDamage > 0)
        {
            if (player == null && PlayerProgression.Instance != null)
                player = PlayerProgression.Instance.GetComponent<PlayerHealth>();
            if (player != null && WeaponTargets.Flat(centre, player.transform.position) <= Radius)
                player.TakeDamage(playerDamage);
        }
    }
}
