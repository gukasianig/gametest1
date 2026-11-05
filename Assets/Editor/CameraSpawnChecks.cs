using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Optional targeted check: create Logs/camera-spawn-check.request, then enter Play.
[InitializeOnLoad]
public static class CameraSpawnChecks
{
    private const string Request = "Logs/camera-spawn-check.request";
    static CameraSpawnChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Request))
                EditorApplication.delayCall += Run;
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Run()
    {
        File.Delete(Request);
        Camera camera = Camera.main;
        TopDownCamera follow = camera.GetComponent<TopDownCamera>();
        Transform originalTarget = follow.target;
        Vector3 originalOffset = follow.offset;
        Vector3 originalPosition = camera.transform.position;
        Quaternion originalRotation = camera.transform.rotation;
        UnityEngine.Random.State randomState = UnityEngine.Random.state;
        float originalAspect = camera.aspect;
        GameObject probe = new GameObject("Camera Check Target");
        try
        {
            originalTarget.GetComponent<StartMenu>().SendMessage("BeginGame", 1);
            Require(Time.timeScale > 0f, "Could not start the run.");
            Require(originalTarget.GetComponent<Rigidbody>().interpolation == RigidbodyInterpolation.Interpolate,
                "Player interpolation is disabled.");
            follow.target = probe.transform;
            foreach (Vector3 direction in new[] { Vector3.back, new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f) })
            {
                Quaternion expected = Quaternion.LookRotation(-follow.offset, Vector3.up);
                for (int i = 0; i < 120; i++)
                {
                    probe.transform.position += direction.normalized * 0.08f;
                    follow.SendMessage("LateUpdate");
                    Require(Quaternion.Angle(camera.transform.rotation, expected) < 0.01f,
                        "Camera direction changed during backwards/diagonal following.");
                }
            }
            follow.target = originalTarget;
            WaveManager waves = UnityEngine.Object.FindObjectOfType<WaveManager>();
            int checkedPositions = 0;
            foreach (float height in new[] { follow.minHeight, originalOffset.y, follow.maxHeight })
            foreach (float aspect in new[] { 1f, 16f / 9f })
            {
                camera.aspect = aspect;
                Vector3 offset = new Vector3(originalOffset.x, height, originalOffset.z);
                camera.transform.SetPositionAndRotation(originalTarget.position + offset,
                    Quaternion.LookRotation(-offset, Vector3.up));
                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
                foreach (EnemyType enemy in waves.enemies)
                for (int sample = 0; sample < 8; sample++)
                {
                    Require(OffscreenSpawn.TryFindPosition(camera, originalTarget, enemy.prefab, waves.groundHeight, out Vector3 point),
                        "No offscreen position found for " + enemy.name);
                    foreach (Renderer renderer in enemy.prefab.GetComponentsInChildren<Renderer>())
                    {
                        Bounds bounds = renderer.bounds;
                        bounds.center += point - enemy.prefab.transform.position;
                        Require(!GeometryUtility.TestPlanesAABB(planes, bounds), "Visible enemy bounds at spawn.");
                    }
                    Require(new Vector2(point.x - originalTarget.position.x, point.z - originalTarget.position.z).magnitude >= 10f,
                        "Enemy spawned too close to the player.");
                    checkedPositions++;
                }
            }
            RunStatistics stats = RunStatistics.Instance;
            int kills = stats.Kills;
            long damage = stats.DamageDealt;
            GameObject enemyProbe = new GameObject("Statistics Check Enemy");
            enemyProbe.tag = "Enemy";
            enemyProbe.transform.position = originalTarget.position + Vector3.right * 100f;
            Health health = enemyProbe.AddComponent<Health>();
            health.experienceReward = 0;
            health.SetHealth(100);
            health.TakeDamage(17);
            health.TakeDamage(1000);
            health.TakeDamage(1000);
            Require(stats.Kills == kills + 1 && stats.DamageDealt == damage + 100,
                "Kill/damage count includes overkill or duplicate death.");
            stats.BeginRun();
            string report = "PASS: stable camera direction for backward/left/right diagonals (360 steps); " +
                "Rigidbody interpolation; " + checkedPositions + " offscreen prefab positions across 3 zooms/2 aspects; " +
                "actual damage and single kill. Real held-key motion and UI still require observation.";
            File.WriteAllText("Logs/camera-spawn-check.txt", report);
            Debug.Log(report);
        }
        catch (Exception exception)
        {
            File.WriteAllText("Logs/camera-spawn-check.txt", "FAIL: " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            follow.target = originalTarget;
            follow.offset = originalOffset;
            camera.aspect = originalAspect;
            camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
            UnityEngine.Random.state = randomState;
            UnityEngine.Object.Destroy(probe);
        }
    }
}
