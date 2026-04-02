using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject[] enemies;   // префабы врагов
    public Transform player;       // игрок

    public float spawnDistance = 20f;
    public float spawnRate = 2f;

    void Start()
    {
        InvokeRepeating(nameof(SpawnEnemy), 1f, spawnRate);
    }

    void SpawnEnemy()
    {
        if (player == null || enemies.Length == 0) return;

        // случайное направление
        Vector2 dir = Random.insideUnitCircle.normalized;

        Vector3 spawnPos = new Vector3(
            player.position.x + dir.x * spawnDistance,
            0.5f,
            player.position.z + dir.y * spawnDistance
        );

        int index = Random.Range(0, enemies.Length);

        GameObject enemy = Instantiate(enemies[index], spawnPos, Quaternion.identity);

        // добавляем движение к игроку
        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.target = player;
        }
    }
}