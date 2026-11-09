using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class EnemyType
{
    public string name;
    public GameObject prefab;

    public int baseCost = 5;   // цена
    public int weight = 10;    // шанс выпадения

    public int minWave = 1;    // с какой волны доступен
    [Min(0f)] public float minSurvivalSeconds = 0f;
}


public class WaveManager : MonoBehaviour
{

    public EnemyType[] enemies;
    public Transform[] spawnPoints;
    public WaveSettings settings = new WaveSettings();

    public int wave = 0;
    public float timeBetweenWaves = 3f;
    [Min(0f)] public float firstWaveDelay = 2f;
    public float groundHeight = 0f;
    private Transform player;
    private Camera gameplayCamera;
    private readonly HashSet<EnemyType> introduced = new HashSet<EnemyType>();
    public bool Suspended { get; private set; }
    public void StopSpawning() { Suspended = true; StopAllCoroutines(); }

    // Rhythm of pressure and events (see WaveDirector). Turn off to get the plain wave behaviour.
    public bool useDirector = true;
    public WaveDirector Director { get; } = new WaveDirector();

    void Start()
    {
        if (settings == null || enemies == null)
        {
            Debug.LogError("WaveManager requires settings and enemies.", this);
            enabled = false;
            return;
        }
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) player = playerObject.transform;
        gameplayCamera = Camera.main;
        Director.PhaseChanged += OnPhaseChanged;
        StartCoroutine(WaveLoop());
    }

    void Update()
    {
        if (!useDirector || Suspended) return;
        GameSession session = GameSession.Instance;
        if (session == null || session.State != GameRunState.Playing) return;
        Director.Evaluate(session.Elapsed);
    }

    void OnPhaseChanged(DirectorPhase phase, int cycle)
    {
        if (Suspended || !useDirector) return;
        GameSession session = GameSession.Instance;
        if (phase == DirectorPhase.Relief)
        {
            if (session != null) session.Announce("Передышка", 2f);
            return;
        }
        if (phase != DirectorPhase.Peak) return;
        DirectorEvent directorEvent = WaveDirector.EventForCycle(cycle);
        if (session != null)
            session.Announce(directorEvent == DirectorEvent.Elite ? "ЭЛИТА НА АРЕНЕ" :
                directorEvent == DirectorEvent.Horde ? "ОРДА!" : "ПИК ВОЛНЫ", 2.5f);
        if (directorEvent == DirectorEvent.Elite) StartCoroutine(EliteEvent(WaveDirector.EliteCount(cycle)));
        else if (directorEvent == DirectorEvent.Horde) StartCoroutine(HordeEvent(WaveDirector.HordeCount(cycle)));
    }

    IEnumerator EliteEvent(int count)
    {
        for (int i = 0; i < count; i++)
        {
            SpawnElite();
            yield return new WaitForSeconds(1.5f);
        }
    }

    // A burst of the cheapest available type; the hard cap on live enemies still applies.
    public IEnumerator HordeEvent(int count)
    {
        List<EnemyType> available = GetAvailableEnemies();
        EnemyType cheapest = null;
        foreach (EnemyType type in available) if (cheapest == null || type.baseCost < cheapest.baseCost) cheapest = type;
        if (cheapest == null) yield break;
        // A failed search for an off-screen position is retried, so the burst is not silently cut short.
        int made = 0, attempts = 0;
        while (made < count && attempts < count * 3 && EnemyAI.ActiveCount < settings.AbsoluteLimitForWave(wave))
        {
            attempts++;
            if (SpawnEnemy(cheapest)) made++;
            yield return new WaitForSeconds(0.25f);
        }
    }

    // One clearly marked, tougher enemy (3x health, 1.3x size, 4x experience, crown). Skips support/suicide types.
    public GameObject SpawnElite()
    {
        if (EnemyAI.ActiveCount >= settings.AbsoluteLimitForWave(wave)) return null;
        List<EnemyType> pool = GetAvailableEnemies().FindAll(type =>
        {
            EnemyAbility ability = type.prefab.GetComponent<EnemyAbility>();
            return ability == null || (ability.role != EnemyRole.Bomber && ability.role != EnemyRole.Healer && ability.role != EnemyRole.Summoner && ability.role != EnemyRole.Buffer);
        });
        if (pool.Count == 0) return null;
        EnemyType chosen = GetRandomEnemy(pool);
        GameObject enemy = SpawnEnemyObject(chosen);
        if (enemy == null) return null;
        enemy.name = "Elite " + chosen.name;
        Health health = enemy.GetComponent<Health>();
        if (health != null)
        {
            health.SetHealth(health.maxHealth * 3);
            health.experienceReward *= 4;
        }
        enemy.transform.localScale *= 1.3f;
        float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
        foreach (Renderer renderer in enemy.GetComponentsInChildren<Renderer>())
        {
            bottom = Mathf.Min(bottom, renderer.bounds.min.y);
            top = Mathf.Max(top, renderer.bounds.max.y);
        }
        if (!float.IsInfinity(bottom)) enemy.transform.position += Vector3.up * (groundHeight - bottom);
        GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crown.name = "Elite crown";
        Destroy(crown.GetComponent<Collider>());
        crown.transform.SetParent(enemy.transform, true);
        crown.transform.localScale = Vector3.one * 0.4f;
        crown.transform.position = new Vector3(enemy.transform.position.x, (float.IsInfinity(top) ? 2f : top - bottom + groundHeight) + 0.5f, enemy.transform.position.z);
        crown.GetComponent<Renderer>().sharedMaterial = ArenaLayout.Instance != null ?
            ArenaLayout.Instance.Material(new Color(1f, 0.82f, 0.15f)) : crown.GetComponent<Renderer>().sharedMaterial;
        return enemy;
    }

    float EffectiveAliveLimit()
    {
        float multiplier = useDirector ? Director.LimitMultiplier : 1f;
        return Mathf.Max(1, Mathf.CeilToInt(settings.AliveLimitForWave(wave) * multiplier));
    }

    IEnumerator WaveLoop()
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, firstWaveDelay));
        while (true)
        {
            wave++;
            yield return StartCoroutine(SpawnWave());
            yield return new WaitForSeconds(Mathf.Max(0.1f, timeBetweenWaves));
        }
    }

    IEnumerator SpawnWave()
    {
        int budget = settings.BudgetForWave(wave);

        int spawned = 0;

        List<EnemyType> available = GetAvailableEnemies();

        while (budget > 0 && available.Count > 0 && spawned < settings.maxEnemiesPerWave)
        {
            // Keep a backlog from becoming an instant crowd when enemies are not killed.
            if (!HasRoom())
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }
            EnemyType type = SelectEnemy(available, budget);

            if (type == null) break;

            if (!SpawnEnemy(type)) break;

            introduced.Add(type);

            budget -= type.baseCost;
            spawned++;
            if (budget > 0 && spawned < settings.maxEnemiesPerWave)
                yield return new WaitForSeconds(settings.SpawnIntervalForWave(wave) * (useDirector ? Director.IntervalMultiplier : 1f));
        }

        Debug.Log($"Wave {wave} | Spawned: {spawned}");
    }

    // Pressure counts enemies near the player; far stragglers do not stall waves, but a hard cap still bounds the total.
    public bool HasRoom()
    {
        if (EnemyAI.ActiveCount >= settings.AbsoluteLimitForWave(wave)) return false;
        float limit = EffectiveAliveLimit();
        if (player == null) return EnemyAI.ActiveCount < limit;
        return EnemyAI.CountWithin(player.position, settings.pressureRadius) < limit;
    }

    List<EnemyType> GetAvailableEnemies()
    {
        float elapsed = GameSession.Instance != null ? GameSession.Instance.Elapsed : Time.timeSinceLevelLoad;
        return AvailableAt(wave, elapsed);
    }

    private List<EnemyType> AvailableAt(int waveNumber, float elapsed)
    {
        List<EnemyType> list = new List<EnemyType>();

        foreach (var e in enemies)
        {
            if (e != null && e.prefab != null && e.baseCost > 0 && e.weight > 0 && waveNumber >= e.minWave && elapsed >= e.minSurvivalSeconds)
                list.Add(e);
        }

        return list;
    }

    private EnemyType SelectEnemy(List<EnemyType> available, int budget)
    {
        // Introduce each authored type once before returning to weighted variety.
        // It still has to pass the time/wave gates and fit the existing wave budget.
        List<EnemyType> affordable = available.FindAll(type => type.baseCost <= budget);
        foreach (EnemyType type in affordable)
            if (!introduced.Contains(type)) return type;
        return affordable.Count > 0 ? GetRandomEnemy(affordable) : null;
    }

    EnemyType GetRandomEnemy(List<EnemyType> list)
    {
        int totalWeight = 0;

        foreach (var e in list)
            totalWeight += e.weight;

        int rand = Random.Range(0, totalWeight);

        foreach (var e in list)
        {
            if (rand < e.weight)
                return e;

            rand -= e.weight;
        }

        return list[0];
    }

    bool SpawnEnemy(EnemyType type) { return SpawnEnemyObject(type) != null; }

    // Ambush event: the cheap enemy types appear at the given points (already checked as walkable by the caller).
    // Respects the hard cap on live enemies; returns how many were created.
    public int SpawnAt(IList<Vector3> points)
    {
        List<EnemyType> available = GetAvailableEnemies();
        available.RemoveAll(type => type.baseCost > 6);
        if (available.Count == 0) available = GetAvailableEnemies();
        if (available.Count == 0) return 0;
        int made = 0;
        foreach (Vector3 point in points)
        {
            if (EnemyAI.ActiveCount >= settings.AbsoluteLimitForWave(wave)) break;
            EnemyType type = GetRandomEnemy(available);
            if (SpawnEnemyAtPosition(type, new Vector3(point.x, HeightFor(type.prefab), point.z)) != null) made++;
        }
        return made;
    }

    // Test arena: creates `count` enemies of one authored type on a ring around the centre. Ignores waves and gates.
    public int SpawnTest(int typeIndex, int count, Vector3 centre)
    {
        if (enemies == null || typeIndex < 0 || typeIndex >= enemies.Length) return 0;
        EnemyType type = enemies[typeIndex];
        if (type == null || type.prefab == null) return 0;
        int made = 0;
        for (int i = 0; i < count; i++)
        {
            Vector3 position = Vector3.zero;
            bool found = false;
            for (int attempt = 0; attempt < 20 && !found; attempt++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(9f, 14f) + (attempt / 5) * 3f;
                position = new Vector3(centre.x + Mathf.Cos(angle) * radius, HeightFor(type.prefab), centre.z + Mathf.Sin(angle) * radius);
                found = ArenaLayout.Instance == null || ArenaLayout.Instance.IsWalkable(position, 1.5f);
            }
            if (!found) continue;
            if (SpawnEnemyAtPosition(type, position) != null) made++;
        }
        return made;
    }

    // Same rule as OffscreenSpawn: the prefab's lowest point sits on the ground.
    float HeightFor(GameObject prefab)
    {
        float bottom = float.PositiveInfinity;
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>()) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
        return groundHeight + (float.IsPositiveInfinity(bottom) ? 0.5f : prefab.transform.position.y - bottom);
    }

    GameObject SpawnEnemyObject(EnemyType type)
    {
        if (gameplayCamera == null) gameplayCamera = Camera.main;
        if (!OffscreenSpawn.TryFindPosition(gameplayCamera, player, type.prefab,
            groundHeight, out Vector3 position)) return null;
        return SpawnEnemyAtPosition(type, position);
    }

    GameObject SpawnEnemyAtPosition(EnemyType type, Vector3 position)
    {
        GameObject enemy = Instantiate(type.prefab, position, Quaternion.identity);
        if (ArenaLayout.Instance != null && enemy.GetComponent<EnemyAbility>() == null)
            ArenaLayout.Instance.Tint(enemy, type.minWave >= 6 ? new Color(0.55f, 0.25f, 0.5f) :
                type.minWave >= 3 ? new Color(0.95f, 0.48f, 0.13f) : new Color(0.7f, 0.22f, 0.22f));

        ModelSkins.ApplyEnemy(enemy, type.prefab.name);

        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        if (ai != null && player != null) ai.target = player;
        if (ai != null) ai.speed *= RunModifiers.EnemySpeed;

        // Initialize scaled health before the first frame (wave growth and the run modifier).
        Health h = enemy.GetComponent<Health>();
        if (h != null)
        {
            h.SetHealth(Mathf.RoundToInt(h.maxHealth * RunModifiers.EnemyHealth * CombatStats.EnemyHealthCurse *
                (1f + Mathf.Max(0, wave - 1) * settings.healthGrowthPerWave)));
        }
        return enemy;
    }
}
