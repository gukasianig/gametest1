using System.Collections;
using UnityEngine;


public class EnemySpawner : MonoBehaviour
{
    public EnemySpawnData[] enemies; // 
    
    public float timeBetweenWaves = 3f;
    public Transform player;
    public float spawnDistance = 20f;
    

    void Start()
    {
       // StartCoroutine(SpawnAll());
    }

    public IEnumerator SpawnWave(EnemySpawnData[] waveEnemies)
    {
        foreach (var enemy in waveEnemies)
        {
            yield return StartCoroutine(SpawnEnemyType(enemy));
        }
    }


    IEnumerator SpawnAll()
{
    foreach (var enemy in enemies)
    {
        StartCoroutine(SpawnEnemyType(enemy));
    }

    yield break;
}

    IEnumerator SpawnEnemyType(EnemySpawnData data)
    {
        for (int i = 0; i < data.count; i++)
        {
            SpawnEnemy(data);
            yield return new WaitForSeconds(data.spawnRate);
        }
    }

    void SpawnEnemy(EnemySpawnData data)
    {
        if (player == null) return;

        Vector2 dir = Random.insideUnitCircle.normalized;

        Vector3 spawnPos = new Vector3(
            player.position.x + dir.x * spawnDistance,
            0.5f,
            player.position.z + dir.y * spawnDistance
        );

        GameObject enemy = Instantiate(data.enemyPrefab, spawnPos, Quaternion.identity);
        enemy.transform.localScale = Vector3.one;

        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.target = player;
            //ai.damage = data.damage;
        }

        // Health hp = enemy.GetComponent<Health>();
        // if (hp != null)
        // {
        //      hp.SetHealth(data.hp);
        // }
    }
}