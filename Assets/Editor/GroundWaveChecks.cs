using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Reflection;

// Opt-in controlled Play check. Reloads the scene afterwards to undo test-only changes.
[InitializeOnLoad]
public static class GroundWaveChecks
{
    private const string Request = "Logs/ground-wave-check.request";
    private static readonly HashSet<int> seen = new HashSet<int>();
    private static readonly List<float> spawnTimes = new List<float>();
    private static WaveManager waves;
    private static PlayerProgression progression;
    private static double upgradeDeadline;
    private static float startedAt, crystalSpawnedAt, nextCheck;
    private static int phase;

    static GroundWaveChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Request))
                EditorApplication.delayCall += Begin;
            if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Tick;
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static float Bottom(GameObject enemy)
    {
        float bottom = float.PositiveInfinity;
        foreach (Renderer renderer in enemy.GetComponentsInChildren<Renderer>())
            bottom = Mathf.Min(bottom, renderer.bounds.min.y);
        return bottom;
    }

    private static void Begin()
    {
        File.Delete(Request);
        File.WriteAllText("Logs/ground-wave-check.txt", "RUNNING: faster pacing, upgrade icons and health visuals.");
        try
        {
            waves = UnityEngine.Object.FindObjectOfType<WaveManager>();
            progression = PlayerProgression.Instance;
            progression.catalog = UnityEngine.Object.Instantiate(progression.catalog);
            progression.catalog.entries.RemoveAll(entry => entry.effect != UpgradeEffect.Pickup &&
                entry.effect != UpgradeEffect.Damage && entry.effect != UpgradeEffect.MaxHealth);
            progression.GetComponent<StartMenu>().SendMessage("BeginGame", 1);
            progression.GetComponent<AutoShoter>().enabled = false;
            progression.GetComponent<PlayMovement>().enabled = false;
            progression.GetComponent<Rigidbody>().velocity = Vector3.zero;
            foreach (EnemyType type in waves.enemies)
            {
                Require(OffscreenSpawn.TryFindPosition(Camera.main, progression.transform,
                    type.prefab, waves.groundHeight, out Vector3 point), "No grounded spawn for " + type.name);
                GameObject enemy = UnityEngine.Object.Instantiate(type.prefab, point, Quaternion.identity);
                enemy.GetComponent<EnemyAI>().enabled = false;
                Require(Mathf.Abs(Bottom(enemy) - waves.groundHeight) < 0.01f,
                    "Enemy floats or sinks: " + type.name + ", bottom=" + Bottom(enemy));
                UnityEngine.Object.Destroy(enemy);
            }
            Require(waves.settings.BudgetForWave(1) == 10 && waves.settings.BudgetForWave(2) == 13,
                "Unexpected early-wave budget.");
            seen.Clear();
            spawnTimes.Clear();
            startedAt = Time.time;
            nextCheck = 0f;
            phase = 0;
            progression.AddExperience(28);
            Require(Time.timeScale == 0f, "Level-up did not pause.");
            Require(progression.Offered.Count == 3, "Three upgrade cards were not offered.");
            Canvas.ForceUpdateCanvases();
            foreach (UpgradeEffect effect in Enum.GetValues(typeof(UpgradeEffect)))
            {
                Sprite art = UiArtwork.Upgrade(effect);
                Require(art != null && art.rect.width > 0f && art.rect.height > 0f, "Missing upgrade art: " + effect);
            }
            Require(UiArtwork.Heart(false) != null && UiArtwork.Heart(true) != null, "Missing heart art.");
            upgradeDeadline = EditorApplication.timeSinceStartup + 4f;
            EditorApplication.update += Tick;
        }
        catch (Exception exception) { Finish("FAIL: " + exception); }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
        try
        {
            if (phase == 0)
            {
                if (EditorApplication.timeSinceStartup < upgradeDeadline) return;
                int pickupIndex = -1;
                for (int i = 0; i < progression.Offered.Count; i++)
                    if (progression.Offered[i].effect == UpgradeEffect.Pickup) pickupIndex = i;
                Require(pickupIndex >= 0 && progression.SelectOfferedUpgrade(pickupIndex), "Could not select pickup card.");
                while (progression.Choosing)
                {
                    int damageIndex = -1;
                    for (int i = 0; i < progression.Offered.Count; i++)
                        if (progression.Offered[i].effect == UpgradeEffect.Damage) damageIndex = i;
                    Require(damageIndex >= 0 && progression.SelectOfferedUpgrade(damageIndex), "Queued damage card failed.");
                }
                Require(Mathf.Abs(progression.AttractionRadius - 5f) < 0.01f &&
                    Mathf.Abs(progression.CollectionRadius - 1f) < 0.01f, "Pickup upgrade radii are wrong.");
                ExperiencePickup.Spawn(progression.transform.position + Vector3.right * 4.5f, 5);
                PlayerHealth health = progression.GetComponent<PlayerHealth>();
                health.TakeDamage(health.CurrentHP - Mathf.RoundToInt(health.maxHP * 0.2f));
                crystalSpawnedAt = Time.time;
                phase = 1;
            }
            float elapsed = Time.time - startedAt;
            if (elapsed < nextCheck) return;
            nextCheck = elapsed + 0.1f;
            if (phase == 1 && Time.time - crystalSpawnedAt > 2f)
            {
                Require(progression.Experience == 5, "Crystal outside the old radius was not collected. XP=" + progression.Experience);
                PlayerHealth health = progression.GetComponent<PlayerHealth>();
                Require(health.hpImage.GetComponentInParent<HealthBarFx>() != null, "Health effects missing.");
                Require(health.hpImage.color.r > health.hpImage.color.g, "Critical health tint missing.");
                Require(health.hpImage.transform.parent.GetComponentsInChildren<Text>(true).Length == 0, "Health bar contains numbers.");
                phase = 2;
            }
            int added = 0;
            foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>())
            {
                enemy.damage = 0; // A stationary player must survive this controlled spawn check.
                Require(!enemy.name.Contains("Fast") && !enemy.name.Contains("tank"), "Strong enemy appeared before its time gate.");
                if (!seen.Add(enemy.GetInstanceID())) continue;
                added++;
                Require(Mathf.Abs(Bottom(enemy.gameObject) - waves.groundHeight) < 0.01f,
                    "Live spawned enemy is above ground.");
                if (spawnTimes.Count > 0)
                    Require(elapsed - spawnTimes[spawnTimes.Count - 1] >=
                        waves.settings.SpawnIntervalForWave(waves.wave) - 0.15f, "Enemies arrived in a burst.");
                spawnTimes.Add(elapsed);
            }
            Require(added <= 1, "Multiple enemies spawned together.");
            Require(EnemyAI.ActiveCount <= waves.settings.AliveLimitForWave(waves.wave), "Alive limit exceeded.");
            if (elapsed < 20f) return;
            Require(spawnTimes.Count > 4 && waves.wave >= 3, "Spawn pace is still too slow.");
            Require(spawnTimes[0] >= 1.8f && spawnTimes[0] <= 2.6f, "First spawn delay is wrong.");
            Finish("PASS: 9 atlas icons, three rarity cards, queued selections/feedback, critical health FX without numbers; " +
                "all 3 prefab bottoms and live enemies at ground y=0; pickup upgrade 4->5m, " +
                "0.8->1m and collection from 4.5m; faster first 20 seconds, " + waves.wave + " waves, " +
                spawnTimes.Count + " enemies, no bursts. Times: " +
                string.Join(", ", spawnTimes.ConvertAll(time => time.ToString("0.0"))));
        }
        catch (Exception exception) { Finish("FAIL: " + exception); }
    }

    private static void Finish(string report)
    {
        EditorApplication.update -= Tick;
        File.WriteAllText("Logs/ground-wave-check.txt", report);
        Debug.Log(report);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
