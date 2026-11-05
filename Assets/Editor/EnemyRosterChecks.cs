using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Short opt-in roster check. No waiting for 75 seconds or editing the player's run.
[InitializeOnLoad]
public static class EnemyRosterChecks
{
    private const string Request = "Logs/enemy-roster.request";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static double nextPoll;
    static EnemyRosterChecks() { EditorApplication.update += Poll; }

    private static void Poll()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.5;
        if (!File.Exists(Request) || PlayerProgression.Instance == null || ArenaLayout.Instance == null) return;
        File.Delete(Request);
        GameObject probe = new GameObject("Enemy roster check");
        var created = new List<GameObject>();
        try
        {
            WaveManager source = UnityEngine.Object.FindObjectOfType<WaveManager>();
            WaveManager waves = probe.AddComponent<WaveManager>();
            waves.enemies = Array.FindAll(source.enemies, entry => entry.prefab.GetComponent<EnemyAbility>() == null);
            waves.settings = source.settings;
            waves.groundHeight = source.groundHeight;
            typeof(WaveManager).GetField("player", Private).SetValue(waves, PlayerProgression.Instance.transform);
            typeof(WaveManager).GetField("gameplayCamera", Private).SetValue(waves, Camera.main);
            var available = typeof(WaveManager).GetMethod("AvailableAt", Private);
            var select = typeof(WaveManager).GetMethod("SelectEnemy", Private);
            var spawn = typeof(WaveManager).GetMethod("SpawnEnemy", Private);
            var introduced = (HashSet<EnemyType>)typeof(WaveManager).GetField("introduced", Private).GetValue(waves);
            Func<int, float, List<EnemyType>> at = (wave, seconds) =>
                (List<EnemyType>)available.Invoke(waves, new object[] { wave, seconds });
            Require(at(1, 0).Count == 1 && at(3, 24.99f).Count == 1 && at(2, 25).Count == 1, "Fast appeared early.");
            Require(at(3, 25).Count == 2 && at(6, 74.99f).Count == 2 && at(5, 75).Count == 2, "Fast/tank gate mismatch.");
            List<EnemyType> all = at(6, 75);
            Require(all.Count == 3, "All three authored types are not available.");
            foreach (EnemyType entry in at(3, 25)) introduced.Add(entry);
            EnemyType tank = all.Find(entry => entry.minWave == 6);
            Require((EnemyType)select.Invoke(waves, new object[] { all, 25 }) == tank, "Tank debut not guaranteed.");
            Require((EnemyType)select.Invoke(waves, new object[] { all, 19 }) != tank, "Tank exceeded budget.");
            Require(select.Invoke(waves, new object[] { all, 0 }) == null, "Zero budget selected an enemy.");
            foreach (EnemyType entry in all)
            {
                EnemyAI prefabAI = entry.prefab.GetComponent<EnemyAI>();
                Health prefabHP = entry.prefab.GetComponent<Health>();
                int expectedHP = entry.minWave == 6 ? 300 : entry.minWave == 3 ? 30 : 100;
                float expectedSpeed = entry.minWave == 6 ? 1.5f : entry.minWave == 3 ? 5f : 3f;
                Require(prefabHP.maxHealth == expectedHP && Mathf.Approximately(prefabAI.speed, expectedSpeed), "Authored stats changed.");
                var before = new HashSet<EnemyAI>(UnityEngine.Object.FindObjectsOfType<EnemyAI>());
                waves.wave = entry.minWave;
                Require((bool)spawn.Invoke(waves, new object[] { entry }), "Offscreen spawn failed.");
                EnemyAI spawned = null;
                foreach (EnemyAI actor in UnityEngine.Object.FindObjectsOfType<EnemyAI>())
                    if (!before.Contains(actor)) { spawned = actor; created.Add(actor.gameObject); }
                Require(spawned != null && spawned.target == PlayerProgression.Instance.transform, "Spawned actor missing target.");
                float bottom = float.PositiveInfinity;
                foreach (Renderer renderer in spawned.GetComponentsInChildren<Renderer>()) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
                Require(Mathf.Abs(bottom - waves.groundHeight) < 0.03f, "Enemy floats above floor.");
                int scaledHP = Mathf.RoundToInt(expectedHP * (1f + (waves.wave - 1) * waves.settings.healthGrowthPerWave));
                Require(spawned.GetComponent<Health>().CurrentHealth == scaledHP, "Wave health not initialized.");
            }
            File.WriteAllText("Logs/enemy-roster-report.txt", "PASS: Weak/Fast/Tank prefabs; authored HP 100/30/300 and speeds 3/5/1.5; wave/time gates 1+0s, 3+25s, 6+75s; guaranteed first introduction within budget; all three spawned grounded with player target and scaled HP.\nFocused Play setup/logic check; no long combat balance test.\n");
        }
        catch (Exception exception)
        {
            File.WriteAllText("Logs/enemy-roster-report.txt", "FAIL: " + exception + "\n");
            Debug.LogException(exception);
        }
        finally
        {
            foreach (GameObject actor in created) if (actor != null) UnityEngine.Object.DestroyImmediate(actor);
            UnityEngine.Object.DestroyImmediate(probe);
        }
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
