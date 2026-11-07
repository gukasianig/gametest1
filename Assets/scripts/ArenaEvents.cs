using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum ArenaEventKind { MeteorShower, MagneticStorm, RepairField, SupplyDrop, HoldPoint, Ambush, ToxicPools }

// Random map events: every minute or so something changes on the arena for a short time.
// Meteor shower (telegraphed blasts hurt everyone), magnetic storm (faster enemies, double experience),
// repair field (a zone that heals the player) and supply drop (a cluster of experience crystals).
public class ArenaEvents : MonoBehaviour
{
    // Read by EnemyAI and Health; both stay at 1 outside the storm.
    public static float EnemySpeedFactor { get; private set; } = 1f;
    public static float ExperienceFactor { get; private set; } = 1f;
    public static ArenaEvents Instance { get; private set; }

    [Min(5f)] public float firstEventAt = 45f;
    [Min(5f)] public float minGap = 50f;
    [Min(5f)] public float maxGap = 80f;
    [Min(5f)] public float eventSeconds = 22f;
    public float stopBeforeBossSeconds = 25f;

    public bool Active { get; private set; }
    public ArenaEventKind Current { get; private set; }
    public int StartedCount { get; private set; }
    public float Remaining { get { return Mathf.Max(0f, endsAt - clock); } }

    private float clock, nextAt, endsAt, tick;
    private bool hasLast;
    private ArenaEventKind last;
    private Transform player;
    private PlayerHealth playerHealth;
    private readonly List<GameObject> props = new List<GameObject>();
    private readonly List<Health> buffer = new List<Health>();
    private Vector3 fieldCenter;
    private const float FieldRadius = 4.5f;

    private void Awake()
    {
        Instance = this;
        ResetFactors();
        nextAt = firstEventAt;
    }

    private void OnDestroy()
    {
        ClearProps();
        ResetFactors();
        if (hudCanvas != null) Destroy(hudCanvas.gameObject);
        if (Instance == this) Instance = null;
    }

    private static void ResetFactors()
    {
        EnemySpeedFactor = 1f;
        ExperienceFactor = 1f;
    }

    private void Update()
    {
        GameSession session = GameSession.Instance;
        if (session == null || session.State != GameRunState.Playing) return;
        if (session.Boss != null || session.BossWarning) { if (Active) End(false); return; }
        clock += Time.deltaTime;
        if (Active)
        {
            Tick();
            if (clock >= endsAt) End(true);
            return;
        }
        if (clock >= nextAt && session.Elapsed < session.survivalSeconds - stopBeforeBossSeconds) Begin(Pick());
        else if (clock >= nextAt) nextAt = float.MaxValue;
    }

    private ArenaEventKind Pick()
    {
        ArenaEventKind kind;
        int guard = 0;
        do kind = (ArenaEventKind)Random.Range(0, 7);
        while (hasLast && kind == last && ++guard < 8);
        return kind;
    }

    // Public so checks can start a chosen event without waiting for the timer.
    public void Begin(ArenaEventKind kind)
    {
        if (Active) End(false);
        FindPlayer();
        Active = true;
        Current = kind;
        last = kind;
        hasLast = true;
        StartedCount++;
        endsAt = clock + eventSeconds;
        tick = 0f;
        string title = "";
        switch (kind)
        {
            case ArenaEventKind.MeteorShower:
                title = "МЕТЕОРИТНЫЙ ДОЖДЬ — уходи с красных кругов!";
                break;
            case ArenaEventKind.MagneticStorm:
                EnemySpeedFactor = 1.3f;
                ExperienceFactor = 2f;
                title = "МАГНИТНАЯ БУРЯ — враги быстрее, опыт ×2";
                break;
            case ArenaEventKind.RepairField:
                fieldCenter = FindSpot(14f, 26f, FieldRadius);
                SpawnField(fieldCenter);
                title = "РЕМОНТНАЯ ЗОНА — встань в зелёный круг";
                break;
            case ArenaEventKind.SupplyDrop:
                SupplyDrop();
                title = "СБРОС ПРИПАСОВ — забери кристаллы у маяка";
                endsAt = clock + 12f;
                break;
            case ArenaEventKind.HoldPoint:
                holdCentre = FindSpot(16f, 28f, HoldRadius);
                holdProgress = 0f;
                holdWon = false;
                props.Add(Flat("Hold point", holdCentre, HoldRadius * 2f, new Color(0.55f, 0.45f, 0.1f), 0.04f));
                holdFill = Flat("Hold fill", holdCentre, 0.1f, new Color(1f, 0.8f, 0.2f), 0.06f);
                props.Add(holdFill);
                title = "УДЕРЖИ ТОЧКУ — стой в золотом круге " + Mathf.RoundToInt(holdSeconds) + " с";
                endsAt = clock + 32f;
                break;
            case ArenaEventKind.Ambush:
                StartCoroutine(AmbushRoutine());
                title = "ЗАСАДА — враги окружают!";
                endsAt = clock + 6f;
                break;
            case ArenaEventKind.ToxicPools:
                SpawnPools();
                title = "ТОКСИЧНЫЕ ЛУЖИ — вредят и вам, и врагам";
                break;
        }
        GameSession session = GameSession.Instance;
        if (session != null) session.Announce(title, 4f);
    }

    public void End(bool announce)
    {
        if (!Active) return;
        Active = false;
        StopAllCoroutines();
        ResetFactors();
        ClearProps();
        poolCentres.Clear();
        bool lost = Current == ArenaEventKind.HoldPoint && !holdWon;
        nextAt = clock + Random.Range(minGap, maxGap);
        GameSession session = GameSession.Instance;
        if (announce && session != null) session.Announce(lost ? "Точка потеряна" : "Событие закончилось", 2f);
    }

    // --- On-screen line under the timer: the running event, its time left and the hold-point progress.
    private Canvas hudCanvas;
    private Text hudText;

    public static string Title(ArenaEventKind kind)
    {
        switch (kind)
        {
            case ArenaEventKind.MeteorShower: return "МЕТЕОРИТНЫЙ ДОЖДЬ";
            case ArenaEventKind.MagneticStorm: return "МАГНИТНАЯ БУРЯ";
            case ArenaEventKind.RepairField: return "РЕМОНТНАЯ ЗОНА";
            case ArenaEventKind.SupplyDrop: return "СБРОС ПРИПАСОВ";
            case ArenaEventKind.HoldPoint: return "УДЕРЖИ ТОЧКУ";
            case ArenaEventKind.Ambush: return "ЗАСАДА";
            default: return "ТОКСИЧНЫЕ ЛУЖИ";
        }
    }

    private void LateUpdate()
    {
        GameSession session = GameSession.Instance;
        bool show = Active && session != null && session.State == GameRunState.Playing;
        if (hudText == null)
        {
            if (!show) return;
            hudCanvas = GameUi.Canvas("Event HUD", 78);
            RectTransform box = GameUi.Box(hudCanvas.transform, "Event", new Vector2(0.3f, 0.855f), new Vector2(0.7f, 0.905f), GameUi.Background);
            hudText = GameUi.Label(box, "", Vector2.zero, Vector2.one, 20, GameUi.Accent);
        }
        hudCanvas.enabled = show || (Active && session != null && session.State == GameRunState.Paused);
        if (!Active) return;
        string line = Title(Current) + "  " + Mathf.CeilToInt(Remaining) + " с";
        if (Current == ArenaEventKind.HoldPoint)
            line = holdWon ? "ТОЧКА УДЕРЖАНА" : "УДЕРЖИ ТОЧКУ  " + Mathf.FloorToInt(holdProgress) + " / " + Mathf.RoundToInt(holdSeconds) + " с  •  осталось " + Mathf.CeilToInt(Remaining) + " с";
        hudText.text = line;
    }

    private void FindPlayer()
    {
        if (player != null) return;
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found == null) return;
        player = found.transform;
        playerHealth = found.GetComponent<PlayerHealth>();
    }

    private void Tick()
    {
        FindPlayer();
        if (player == null) return;
        tick -= Time.deltaTime;
        if (Current == ArenaEventKind.MeteorShower)
        {
            if (tick > 0f) return;
            tick = 0.55f;
            Vector3 point = Random.value < 0.65f ? FindSpot(0f, 13f, 2f) : FindSpot(0f, 40f, 2f);
            Meteor.Create(point, this);
        }
        else if (Current == ArenaEventKind.HoldPoint)
        {
            if (holdWon) return;
            Vector3 offset = player.position - holdCentre;
            offset.y = 0f;
            if (offset.magnitude <= HoldRadius) holdProgress += Time.deltaTime;
            float share = Mathf.Clamp01(holdProgress / Mathf.Max(0.1f, holdSeconds));
            if (holdFill != null) holdFill.transform.localScale = new Vector3(HoldRadius * 2f * share, 0.01f, HoldRadius * 2f * share);
            if (share >= 1f) HoldSucceeded();
        }
        else if (Current == ArenaEventKind.ToxicPools)
        {
            if (tick > 0f) return;
            tick = 0.5f;
            TickPools();
        }
        else if (Current == ArenaEventKind.RepairField)
        {
            if (tick > 0f) return;
            tick = 0.5f;
            Vector3 offset = player.position - fieldCenter;
            offset.y = 0f;
            if (playerHealth != null && offset.magnitude <= FieldRadius) playerHealth.Heal(3);
        }
    }

    // A random walkable point around the player, between minDistance and maxDistance.
    private Vector3 FindSpot(float minDistance, float maxDistance, float radius)
    {
        Vector3 centre = player != null ? player.position : Vector3.zero;
        for (int i = 0; i < 24; i++)
        {
            Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(minDistance, maxDistance);
            Vector3 point = new Vector3(centre.x + circle.x, 0f, centre.z + circle.y);
            if (ArenaLayout.Instance == null || ArenaLayout.Instance.IsWalkable(point, radius)) return point;
        }
        return new Vector3(centre.x, 0f, centre.z);
    }

    private void SpawnField(Vector3 centre)
    {
        GameObject disc = Flat("Repair field", centre, FieldRadius * 2f, new Color(0.15f, 0.8f, 0.4f), 0.05f);
        props.Add(disc);
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "Repair beacon";
        Destroy(beam.GetComponent<Collider>());
        beam.transform.position = centre + Vector3.up * 3f;
        beam.transform.localScale = new Vector3(0.5f, 3f, 0.5f);
        Paint(beam, new Color(0.3f, 1f, 0.55f));
        props.Add(beam);
    }

    private void SupplyDrop()
    {
        Vector3 centre = FindSpot(18f, 30f, 3f);
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "Supply beacon";
        Destroy(beam.GetComponent<Collider>());
        beam.transform.position = centre + Vector3.up * 5f;
        beam.transform.localScale = new Vector3(0.6f, 5f, 0.6f);
        Paint(beam, new Color(1f, 0.8f, 0.2f));
        props.Add(beam);
        props.Add(Flat("Supply marker", centre, 6f, new Color(0.9f, 0.7f, 0.15f), 0.05f));
        int reward = 6 + Mathf.Min(10, StartedCount * 2);
        for (int i = 0; i < 14; i++)
        {
            Vector2 around = Random.insideUnitCircle * 2.6f;
            ExperiencePickup.Spawn(centre + new Vector3(around.x, 0f, around.y), reward);
        }
    }

    // --- Hold the point: stand in the circle for holdSeconds (the clock only runs inside) to earn a crystal burst and a repair kit.
    [Min(1f)] public float holdSeconds = 10f;
    public float HoldProgress { get { return holdProgress; } }
    private const float HoldRadius = 5f;
    private Vector3 holdCentre;
    private float holdProgress;
    private bool holdWon;
    private GameObject holdFill;

    private void HoldSucceeded()
    {
        holdWon = true;
        int reward = 4 + Mathf.Min(8, StartedCount);
        for (int i = 0; i < 12; i++)
        {
            Vector2 around = Random.insideUnitCircle * 3f;
            ExperiencePickup.Spawn(holdCentre + new Vector3(around.x, 0f, around.y), reward);
        }
        DropPickup.Spawn(holdCentre, PickupKind.Health);
        GameSession session = GameSession.Instance;
        if (session != null) session.Announce("ТОЧКА УДЕРЖАНА — награда!", 3f);
        endsAt = Mathf.Min(endsAt, clock + 1.5f);
    }

    // --- Ambush: red markers around the hero, then a ring of cheap enemies appears on them.
    public int AmbushCount { get; private set; }

    private IEnumerator AmbushRoutine()
    {
        FindPlayer();
        if (player == null) yield break;
        var points = new List<Vector3>();
        var markers = new List<GameObject>();
        int count = Mathf.Min(16, 8 + Mathf.Max(0, StartedCount - 1) * 2);
        for (int i = 0; i < count; i++)
        {
            float angle = (i + Random.value * 0.5f) * Mathf.PI * 2f / count;
            Vector3 point = player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(10f, 13f);
            point.y = 0f;
            if (ArenaLayout.Instance != null && !ArenaLayout.Instance.IsWalkable(point, 1f)) continue;
            points.Add(point);
            GameObject marker = Flat("Ambush marker", point, 2.2f, new Color(0.8f, 0.1f, 0.1f), 0.05f);
            markers.Add(marker);
            props.Add(marker);
        }
        float end = Time.time + 1.6f;
        while (Time.time < end) yield return null;
        foreach (GameObject marker in markers) if (marker != null) Destroy(marker);
        WaveManager waves = FindObjectOfType<WaveManager>();
        if (waves != null && GameSession.Instance != null && GameSession.Instance.State == GameRunState.Playing)
            AmbushCount += waves.SpawnAt(points);
    }

    // --- Toxic pools: green circles that hurt everything inside after a short warning.
    private readonly List<Vector3> poolCentres = new List<Vector3>();
    private float poolsActiveAt;
    private const float PoolRadius = 4f;
    public int PoolCount { get { return poolCentres.Count; } }

    private void SpawnPools()
    {
        poolCentres.Clear();
        poolsActiveAt = clock + 1.5f;
        for (int i = 0; i < 3; i++)
        {
            Vector3 centre = FindSpot(4f + i * 4f, 14f + i * 6f, PoolRadius);
            poolCentres.Add(centre);
            props.Add(Flat("Toxic pool", centre, PoolRadius * 2f, new Color(0.25f, 0.5f, 0.1f), 0.04f));
        }
    }

    private void TickPools()
    {
        if (clock < poolsActiveAt) return;
        WeaponTargets.Collect(buffer);
        foreach (Vector3 centre in poolCentres)
        {
            foreach (Health health in buffer)
                if (health != null && !health.isDead && WeaponTargets.Flat(centre, health.transform.position) <= PoolRadius)
                    health.TakeDamage(12);
            if (playerHealth != null && WeaponTargets.Flat(centre, player.position) <= PoolRadius) playerHealth.TakeDamage(5);
        }
    }

    // Damage used by meteors: enemies and the player take it, so staying in the circle is never free.
    public void Blast(Vector3 centre, float radius, int enemyDamage, int playerDamage)
    {
        WeaponTargets.Collect(buffer);
        foreach (Health health in buffer)
            if (health != null && WeaponTargets.Flat(centre, health.transform.position) <= radius + WeaponTargets.BodyRadius(health) * 0.5f)
                health.TakeDamage(enemyDamage);
        FindPlayer();
        if (playerHealth != null && WeaponTargets.Flat(centre, player.position) <= radius) playerHealth.TakeDamage(playerDamage);
        ExpandingSphere.Create(centre, radius, new Color(1f, 0.5f, 0.12f), 0.35f);
    }

    internal static GameObject Flat(string name, Vector3 centre, float diameter, Color colour, float height)
    {
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = name;
        Destroy(disc.GetComponent<Collider>());
        disc.transform.position = new Vector3(centre.x, height, centre.z);
        disc.transform.localScale = new Vector3(diameter, 0.01f, diameter);
        Paint(disc, colour);
        return disc;
    }

    internal static void Paint(GameObject item, Color colour)
    {
        Renderer renderer = item.GetComponent<Renderer>();
        if (renderer != null && ArenaLayout.Instance != null) renderer.sharedMaterial = ArenaLayout.Instance.Material(colour);
    }

    private void ClearProps()
    {
        foreach (GameObject item in props) if (item != null) Destroy(item);
        props.Clear();
    }
}

// One meteor: a red circle grows for a moment, then the blast hits.
public class Meteor : MonoBehaviour
{
    public const float Delay = 1.4f, Radius = 3.5f;
    private ArenaEvents owner;
    private Transform fill;
    private float age;

    public static void Create(Vector3 point, ArenaEvents owner)
    {
        GameObject ring = ArenaEvents.Flat("Meteor warning", point, Radius * 2f, new Color(0.5f, 0.08f, 0.08f), 0.04f);
        Meteor meteor = ring.AddComponent<Meteor>();
        meteor.owner = owner;
        GameObject inner = ArenaEvents.Flat("Meteor fill", point, Radius * 2f, new Color(1f, 0.25f, 0.12f), 0.06f);
        inner.transform.SetParent(ring.transform, true);
        meteor.fill = inner.transform;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Delay);
        if (fill != null)
        {
            float scale = Mathf.Max(0.05f, t) * Radius * 2f / Mathf.Max(0.01f, transform.localScale.x);
            fill.localScale = new Vector3(scale, 1f, scale);
        }
        if (age < Delay) return;
        if (owner != null) owner.Blast(transform.position, Radius, 60, 15);
        Destroy(gameObject);
    }
}
