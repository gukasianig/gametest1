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
        waveText.text= "test";
        StartCoroutine(RunWaves());
    }

   
    IEnumerator RunWaves() //волна 
    {   
        yield return StartCoroutine(ShowWaveText("Wave 1"));
        yield return StartCoroutine(spawner.SpawnWave(wave1));
        yield return WaitForWaveClear();


        yield return new WaitForSeconds(timeBetweenWaves);

        yield return StartCoroutine(ShowWaveText("Wave 2"));
        yield return StartCoroutine(spawner.SpawnWave(wave2));
        yield return WaitForWaveClear();



        yield return new WaitForSeconds(timeBetweenWaves);

        yield return StartCoroutine(ShowWaveText("Wave 3"));
        yield return StartCoroutine(spawner.SpawnWave(wave3));
        yield return WaitForWaveClear();
        
        yield return StartCoroutine(ShowWaveText("Wave Cleared!"));
    }
     IEnumerator ShowWaveText(string text)
    {
        waveText.text = text;
        waveText.gameObject.SetActive(true);

        yield return new WaitForSeconds(3f);

        waveText.gameObject.SetActive(false);
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
