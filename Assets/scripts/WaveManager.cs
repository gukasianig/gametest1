using System.Collections;
using UnityEngine;
using TMPro;


public class WaveManager : MonoBehaviour
{
    public EnemySpawner spawner;
    public TextMeshProUGUI waveText;
    public EnemySpawnData[] wave1;
    public EnemySpawnData[] wave2;
    public EnemySpawnData[] wave3;

    public float timeBetweenWaves = 5f;

    void Start()
    {
        StartCoroutine(RunWaves());
    }

    IEnumerator RunWaves()
    {   
        waveText.text = "wave 1";
        yield return StartCoroutine(spawner.SpawnWave(wave1));
        yield return WaitForWaveClear();

        waveText.text = "wave Cleared!";

        yield return new WaitForSeconds(timeBetweenWaves);

        waveText.text = "Wave 2";
        yield return StartCoroutine(spawner.SpawnWave(wave2));
        yield return WaitForWaveClear();

        waveText.text = "wave Cleared!";


        yield return new WaitForSeconds(timeBetweenWaves);

        waveText.text = "Wave 3";
        yield return StartCoroutine(spawner.SpawnWave(wave3));
        yield return WaitForWaveClear();
        waveText.text = "wave Cleared!";
    }

    IEnumerator WaitForWaveClear()
    {
       while (true)
    {
        int count = GameObject.FindGameObjectsWithTag("Enemy").Length;
        Debug.Log("Enemies left: " + count);

        if (count == 0)
            break;

        yield return null;
    }
    }
}
