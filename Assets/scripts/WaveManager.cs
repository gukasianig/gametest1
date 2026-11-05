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
        StartCoroutine(WaveLoop());
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
                yield return new WaitForSeconds(settings.SpawnIntervalForWave(wave));
        }

        Debug.Log($"Wave {wave} | Spawned: {spawned}");
    }

    // Pressure counts enemies near the player; far stragglers do not stall waves, but a hard cap still bounds the total.
    public bool HasRoom()
    {
        if (EnemyAI.ActiveCount >= settings.AbsoluteLimitForWave(wave)) return false;
        if (player == null) return EnemyAI.ActiveCount < settings.AliveLimitForWave(wave);
        return EnemyAI.CountWithin(player.position, settings.pressureRadius) < settings.AliveLimitForWave(wave);
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

    bool SpawnEnemy(EnemyType type)
    {
        if (gameplayCamera == null) gameplayCamera = Camera.main;
        if (!OffscreenSpawn.TryFindPosition(gameplayCamera, player, type.prefab,
            groundHeight, out Vector3 position)) return false;
        GameObject enemy = Instantiate(type.prefab, position, Quaternion.identity);
        if (ArenaLayout.Instance != null && enemy.GetComponent<EnemyAbility>() == null)
            ArenaLayout.Instance.Tint(enemy, type.minWave >= 6 ? new Color(0.55f, 0.25f, 0.5f) :
                type.minWave >= 3 ? new Color(0.95f, 0.48f, 0.13f) : new Color(0.7f, 0.22f, 0.22f));

        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        if (ai != null && player != null) ai.target = player;

        // Initialize scaled health before the first frame.
        Health h = enemy.GetComponent<Health>();
        if (h != null)
        {
            h.SetHealth(Mathf.RoundToInt(h.maxHealth *
                (1f + Mathf.Max(0, wave - 1) * settings.healthGrowthPerWave)));
        }
        return true;
    }
}
