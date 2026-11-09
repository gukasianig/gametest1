using UnityEngine;
using System.Collections.Generic;

public class EnemyAI : MonoBehaviour
{
    private static readonly HashSet<EnemyAI> active = new HashSet<EnemyAI>();
    public static int ActiveCount => active.Count;
    public static IEnumerable<EnemyAI> All => active;
    private Health cachedHealth;
    private bool heightKnown;
    private float baseHeight;
    public Health Health => cachedHealth != null ? cachedHealth : (cachedHealth = GetComponent<Health>());

    // Сетка соседей (09.11.2026, FPS): раньше каждый враг ~10 раз за кадр перебирал ВСЕХ врагов (отталкивание +
    // 9 направлений обхода) — при 180 врагах ~300 тыс. проверок и 7–8 мс кадра. Теперь раз в кадр враги раскладываются
    // по клеткам 4 м, и каждый смотрит только тех, кто ближе дальности, на которой они вообще влияют друг на друга.
    private const float NeighborCell = 4f;
    // Счётчики профилировщика (видны в Profiler и в Assets/Editor/PerfProbe).
    private static readonly Unity.Profiling.ProfilerMarker PathMarker = new Unity.Profiling.ProfilerMarker("EnemyAI.Path");
    private static readonly Unity.Profiling.ProfilerMarker CrowdMarker = new Unity.Profiling.ProfilerMarker("EnemyAI.Crowd");
    private static readonly Unity.Profiling.ProfilerMarker ResolveMarker = new Unity.Profiling.ProfilerMarker("EnemyAI.Resolve");
    private static readonly Dictionary<long, List<EnemyAI>> cells = new Dictionary<long, List<EnemyAI>>();
    private static readonly Stack<List<EnemyAI>> spareCells = new Stack<List<EnemyAI>>();
    private static int cellsFrame = -1;
    private static float maxSpeed, maxBody;
    private readonly List<EnemyAI> neighbors = new List<EnemyAI>(32);

    private static long CellKey(int x, int z) { return ((long)x << 32) ^ (uint)z; }

    private static void RebuildCells()
    {
        if (cellsFrame == Time.frameCount) return;
        cellsFrame = Time.frameCount;
        foreach (List<EnemyAI> list in cells.Values) { list.Clear(); spareCells.Push(list); }
        cells.Clear();
        maxSpeed = 0f;
        maxBody = 0f;
        foreach (EnemyAI enemy in active)
        {
            Vector3 p = enemy.transform.position;
            enemy.gridPosition = p;
            long key = CellKey(Mathf.FloorToInt(p.x / NeighborCell), Mathf.FloorToInt(p.z / NeighborCell));
            if (!cells.TryGetValue(key, out List<EnemyAI> list))
            {
                list = spareCells.Count > 0 ? spareCells.Pop() : new List<EnemyAI>(8);
                cells[key] = list;
            }
            list.Add(enemy);
            maxSpeed = Mathf.Max(maxSpeed, enemy.speed);
            maxBody = Mathf.Max(maxBody, enemy.bodyRadius);
        }
    }

    // Те же границы, что в отталкивании и Score: дальше этой дистанции другой враг ни на что не влияет.
    private void GatherNeighbors(Vector3 position)
    {
        RebuildCells();
        float reach = Mathf.Max(Mathf.Max(separationRadius, bodyRadius + maxBody + 0.12f) * CrowdPacking,
            (bodyRadius + maxBody + 0.12f) * CrowdPacking + (speed + maxSpeed) * 0.65f) + 0.1f;
        // В плотной толпе соседей в этом радиусе — сотня; уворачиваться имеет смысл только от ближайших.
        // Не дальше 5 м (разгон Charger раздувал maxSpeed) и не больше MaxNeighbors самых близких.
        reach = Mathf.Min(reach, 5f);
        float limit = reach * reach;
        neighbors.Clear();
        int found = 0;
        int x0 = Mathf.FloorToInt((position.x - reach) / NeighborCell), x1 = Mathf.FloorToInt((position.x + reach) / NeighborCell);
        int z0 = Mathf.FloorToInt((position.z - reach) / NeighborCell), z1 = Mathf.FloorToInt((position.z + reach) / NeighborCell);
        for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
            {
                if (!cells.TryGetValue(CellKey(x, z), out List<EnemyAI> list)) continue;
                foreach (EnemyAI other in list)
                {
                    if (other == this || other == null) continue;
                    float dx = other.gridPosition.x - position.x, dz = other.gridPosition.z - position.z;
                    float d = dx * dx + dz * dz;
                    if (d > limit) continue;
                    // Вставка в отсортированный массив K ближайших.
                    int slot = found < MaxNeighbors ? found++ : MaxNeighbors;
                    if (slot == MaxNeighbors && d >= nearDistance[MaxNeighbors - 1]) continue;
                    if (slot == MaxNeighbors) slot = MaxNeighbors - 1;
                    while (slot > 0 && nearDistance[slot - 1] > d) { nearDistance[slot] = nearDistance[slot - 1]; nearEnemy[slot] = nearEnemy[slot - 1]; slot--; }
                    nearDistance[slot] = d;
                    nearEnemy[slot] = other;
                }
            }
        for (int i = 0; i < found; i++) neighbors.Add(nearEnemy[i]);
    }

    private const int MaxNeighbors = 14;
    private float lastSteer;
    private bool lastSteerStop;
    private int lastSteerFrame = -10;
    private int steerPhase => GetInstanceID() & 1;
    private static readonly float[] nearDistance = new float[MaxNeighbors];
    private static readonly EnemyAI[] nearEnemy = new EnemyAI[MaxNeighbors];
    private Vector3 gridPosition;   // позиция на начало кадра (из сетки): дешевле, чем transform.position у соседа

    public static int CountWithin(Vector3 center, float radius)
    {
        float limit = radius * radius;
        int count = 0;
        foreach (EnemyAI enemy in active)
        {
            Vector3 offset = enemy.transform.position - center;
            offset.y = 0f;
            if (offset.sqrMagnitude <= limit) count++;
        }
        return count;
    }
    public float speed = 3f;
    public float attackDistance = 1.5f;
    public int damage = 10;
    public float attackCooldown = 1f;
    public float stopDistance = 1.5f;
    // Плотность толпы: доля «личного пространства» (сумма радиусов тел, separationRadius), которую враги держат между собой.
    // 09.11.2026 владелец попросил стоять плотнее: 1 → 0,75 (радиус бокса считается по диагонали и завышен).
    public const float CrowdPacking = 0.75f;
    public float separationRadius = 1.2f;
    public float separationStrength = 2f;
    public float attackRange = 1.5f;
    public Transform target;
    public bool UsesMelee { get; set; } = true;
    public bool MovementLocked { get; set; }
    public float PreferredRange { get; set; }
    // Speed boost from a Buffer enemy: multiplier until the given game time.
    private float boostFactor = 1f, boostUntil;
    public void Boost(float factor, float seconds) { boostFactor = factor; boostUntil = Time.time + seconds; }
    public bool Boosted => Time.time < boostUntil;
    private float lastAttackTime;
    private Vector3 waypoint;
    private float nextPathTime;
    private float bodyRadius = 0.5f;
    private Vector3 velocity;
    private float preferredSide = 1f;
    private static readonly float[] steeringAngles = { 0f, 25f, -25f, 50f, -50f, 75f, -75f, 100f, -100f };

    private void OnEnable() { active.Add(this); velocity = Vector3.zero; }
    private void OnDisable() { active.Remove(this); velocity = Vector3.zero; }

    private void Start()
    {
        Collider shape = GetComponent<Collider>();
        if (shape != null)
        {
            Vector3 extent = shape.bounds.extents;
            bodyRadius = shape is BoxCollider ? new Vector2(extent.x, extent.z).magnitude : Mathf.Max(extent.x, extent.z);
        }
        else bodyRadius = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) * 0.5f;
    }

    private void Update()
    {
        if (target == null || Time.timeScale <= 0f || MovementLocked) { velocity = Vector3.zero; return; }
        Move(Time.deltaTime);
    }

    private void Move(float deltaTime)
    {
        Vector3 position = transform.position;
        Vector3 toPlayer = target.position - position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        Vector3 direction = Vector3.zero;
        if (PreferredRange > 0f && distance < PreferredRange * 0.65f)
            direction = -toPlayer.normalized;
        else if (distance > Mathf.Max(stopDistance, PreferredRange))
        {
            if (ArenaLayout.Instance != null)
            {
                if (Time.time >= nextPathTime || (waypoint - position).sqrMagnitude < 0.3f)
                {
                    using (PathMarker.Auto()) waypoint = ArenaLayout.Instance.NextWaypoint(position, target.position, bodyRadius);
                    nextPathTime = Time.time + 0.6f;
                }
                direction = waypoint - position;
                direction.y = 0f;
                direction.Normalize();
            }
            else direction = toPlayer.normalized;
        }

        CrowdMarker.Begin();
        GatherNeighbors(position);
        Vector3 separation = Vector3.zero;
        foreach (EnemyAI other in neighbors)
        {
            if (other == this) continue;
            Vector3 away = position - other.gridPosition;
            away.y = 0f;
            float gap = away.magnitude;
            float personalSpace = Mathf.Max(separationRadius, bodyRadius + other.bodyRadius + 0.12f) * CrowdPacking;
            if (gap >= personalSpace) continue;
            // Coincident spawn points also need a deterministic escape direction.
            if (gap < 0.001f) away = GetInstanceID() < other.GetInstanceID() ? Vector3.right : Vector3.left;
            else away /= gap;
            separation += away * (1f - gap / personalSpace);
        }
        // Repulsion must not reverse pursuit, as the previous inverse-distance sum did.
        Vector3 desired = Vector3.ClampMagnitude(direction + Vector3.ClampMagnitude(separation * separationStrength, 0.65f), 1f) * speed * (Boosted ? boostFactor : 1f) * SlowEffect.MovementFactor(this) * ArenaEvents.EnemySpeedFactor * CombatStats.EnemySpeedCurse;
        Vector3 chosen = AvoidCrowd(position, desired, deltaTime);
        CrowdMarker.End();
        Vector3 next = position + chosen * deltaTime;
        if (ArenaLayout.Instance != null) using (ResolveMarker.Auto()) next = ArenaLayout.Instance.ResolveMovement(position, next, bodyRadius);
        if (!heightKnown) { baseHeight = position.y; heightKnown = true; }   // высота спавна задана от плоскости 0 (WaveManager.groundHeight)
        velocity = deltaTime > 0f ? (next - position) / deltaTime : Vector3.zero;
        velocity.y = 0f;
        next.y = baseHeight + GroundHeight.At(next);   // на рельефе (GreenArenaPlay) враг идёт по земле; на плоских аренах высота 0
        transform.position = next;
        if (velocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(velocity), 10f * deltaTime);
        if (UsesMelee && distance <= attackRange) Attack();
    }

    private Vector3 AvoidCrowd(Vector3 position, Vector3 desired, float deltaTime)
    {
        Vector3 forward = desired.normalized;
        // Прямо и без помех от соседей — заведомо лучший вариант (максимум за «вперёд», штраф 0): остальные 8 направлений
        // не считаем. В редкой толпе это большинство врагов.
        if (ArenaLayout.Instance == null || ArenaLayout.Instance.PathClear(position, position + desired * deltaTime, bodyRadius))
            if (Score(position, desired, deltaTime) >= 0f) return desired;
        // Полный перебор направлений — через кадр (половина врагов в чётных кадрах, половина в нечётных); в промежутке
        // держим найденный поворот, если путь по нему свободен. Глазу незаметно, обход толпы вдвое дешевле.
        if (((Time.frameCount + steerPhase) & 1) == 1 && lastSteerFrame == Time.frameCount - 1)
        {
            Vector3 held = lastSteer == 0f ? desired : Quaternion.AngleAxis(lastSteer, Vector3.up) * desired;
            if (!lastSteerStop && (ArenaLayout.Instance == null || ArenaLayout.Instance.PathClear(position, position + held * deltaTime, bodyRadius)))
                return held;
        }
        lastSteerFrame = Time.frameCount;
        Vector3 best = Vector3.zero;
        float bestScore = Score(position, Vector3.zero, deltaTime) - 0.2f;
        float bestAngle = 0f;
        foreach (float angle in steeringAngles)
        {
            Vector3 candidate = Quaternion.AngleAxis(angle, Vector3.up) * desired;
            Vector3 next = position + candidate * deltaTime;
            if (ArenaLayout.Instance != null && !ArenaLayout.Instance.PathClear(position, next, bodyRadius)) continue;
            float side = Mathf.Sign(angle);
            float score = Vector3.Dot(candidate / Mathf.Max(0.01f, speed), forward) * 2.5f + Score(position, candidate, deltaTime);
            if (angle != 0f && side != preferredSide) score -= 0.3f;
            if (score <= bestScore) continue;
            bestScore = score;
            bestAngle = angle;
            best = candidate;
        }
        lastSteer = bestAngle;
        lastSteerStop = best == Vector3.zero;
        float turn = Vector3.Cross(forward, best).y;
        if (Mathf.Abs(turn) > 0.05f) preferredSide = Mathf.Sign(turn);
        return best;
    }

    private float Score(Vector3 position, Vector3 candidate, float deltaTime)
    {
        float penalty = 0f;
        foreach (EnemyAI other in neighbors)
        {
            if (other == this) continue;
            Vector3 offset = other.gridPosition - position;
            offset.y = 0f;
            float clearance = (bodyRadius + other.bodyRadius + 0.12f) * CrowdPacking;
            if (offset.sqrMagnitude > Mathf.Pow(clearance + (speed + other.speed) * 0.65f, 2f)) continue;
            Vector3 relative = candidate - other.velocity;
            float time = relative.sqrMagnitude > 0.001f ?
                Mathf.Clamp(Vector3.Dot(offset, relative) / relative.sqrMagnitude, 0f, 0.65f) : 0f;
            float predictedGap = (offset - relative * time).magnitude;
            float risk = Mathf.Clamp01(1f - predictedGap / clearance);
            penalty += risk * risk * 12f;
            // Never move deeper into an existing overlap; allow movement out of it.
            float nextGap = (offset - candidate * deltaTime).magnitude;
            if (nextGap < Mathf.Min(offset.magnitude, clearance - 0.1f) - 0.001f) penalty += 30f;
        }
        return -penalty;
    }

    private void Attack()
    {
        if (Time.time - lastAttackTime < attackCooldown) return;
        lastAttackTime = Time.time;
        PlayerHealth player = target.GetComponent<PlayerHealth>();
        if (player != null) player.TakeDamage(damage);
    }
}
