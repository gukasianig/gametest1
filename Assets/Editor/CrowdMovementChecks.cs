using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Opt-in deterministic movement check in Play; no changes to scene assets or balance.
[InitializeOnLoad]
public static class CrowdMovementChecks
{
    private const string Request = "Logs/crowd-movement.request";
    private static readonly MethodInfo Move = typeof(EnemyAI).GetMethod("Move", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo Radius = typeof(EnemyAI).GetField("bodyRadius", BindingFlags.Instance | BindingFlags.NonPublic);
    private static double nextPoll;

    static CrowdMovementChecks() { EditorApplication.update += Poll; }

    private static void Poll()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.5;
        if (!File.Exists(Request) || PlayerProgression.Instance == null) return;
        File.Delete(Request);
        var spawned = new List<EnemyAI>();
        var hidden = new List<GameObject>();
        GameObject goal = new GameObject("Crowd check goal");
        try
        {
            foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>())
            {
                hidden.Add(enemy.gameObject);
                enemy.gameObject.SetActive(false);
            }
            goal.transform.position = new Vector3(60f, 0.5f, 32f);
            EnemyAI slow = Spawn("Assets/Enemy_tank.prefab", new Vector3(29f, 0.75f, 32f), goal.transform, spawned);
            EnemyAI fast = Spawn("Assets/Enemy_Fast.prefab", new Vector3(25f, 0.5f, 32f), goal.transform, spawned);
            float minimumGap = Simulate(spawned, 8f);
            if (fast.transform.position.x < slow.transform.position.x + 2f)
                throw new Exception("Fast enemy failed to overtake tank: fast=" + fast.transform.position + " tank=" + slow.transform.position);
            string result = "PASS: fast overtakes tank by " + (fast.transform.position.x - slow.transform.position.x).ToString("F2") + " m; minimum clearance " + minimumGap.ToString("F3") + " m.\n";
            foreach (EnemyAI enemy in spawned) UnityEngine.Object.DestroyImmediate(enemy.gameObject);
            spawned.Clear();
            goal.transform.position = new Vector3(60f, 0.5f, 32f);
            float startDistance = 0f;
            for (int row = 0; row < 4; row++)
            for (int column = 0; column < 4; column++)
            {
                string path = row % 3 == 0 ? "Assets/Enemy_tank.prefab" : row % 3 == 1 ? "Assets/Enemy_Fast.prefab" : "Assets/Enemy_Weak.prefab";
                EnemyAI enemy = Spawn(path, new Vector3(20f + column * 2.2f, 0.75f, 28f + row * 2.2f), goal.transform, spawned);
                startDistance += Vector3.Distance(enemy.transform.position, goal.transform.position);
            }
            minimumGap = Simulate(spawned, 8f);
            float endDistance = 0f;
            foreach (EnemyAI enemy in spawned) endDistance += Vector3.Distance(enemy.transform.position, goal.transform.position);
            float progress = (startDistance - endDistance) / spawned.Count;
            if (progress < 5f) throw new Exception("Crowd made insufficient progress: " + progress);
            result += "PASS: 16 mixed enemies advance " + progress.ToString("F2") + " m on average; minimum clearance " + minimumGap.ToString("F3") + " m; arena walkability preserved.\n";
            File.WriteAllText("Logs/crowd-movement-report.txt", result + "Fixed-step movement simulation in Play, not a full physics/gameplay regression.\n");
        }
        catch (Exception exception)
        {
            File.WriteAllText("Logs/crowd-movement-report.txt", "FAIL: " + exception + "\n");
            Debug.LogException(exception);
        }
        finally
        {
            foreach (EnemyAI enemy in spawned) if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy.gameObject);
            UnityEngine.Object.DestroyImmediate(goal);
            foreach (GameObject enemy in hidden) if (enemy != null) enemy.SetActive(true);
        }
    }

    private static EnemyAI Spawn(string path, Vector3 position, Transform goal, List<EnemyAI> all)
    {
        GameObject root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), position, Quaternion.identity);
        EnemyAI enemy = root.GetComponent<EnemyAI>();
        enemy.target = goal;
        enemy.SendMessage("Start");
        all.Add(enemy);
        return enemy;
    }

    private static float Simulate(List<EnemyAI> all, float seconds)
    {
        float minimum = float.PositiveInfinity;
        object[] step = { 0.02f };
        for (int frame = 0; frame < seconds / 0.02f; frame++)
        {
            foreach (EnemyAI enemy in all) Move.Invoke(enemy, step);
            for (int i = 0; i < all.Count; i++)
            {
                float radius = (float)Radius.GetValue(all[i]);
                if (!ArenaLayout.Instance.IsWalkable(all[i].transform.position, radius)) throw new Exception("Enemy entered arena obstacle.");
                for (int j = i + 1; j < all.Count; j++)
                {
                    Vector3 delta = all[i].transform.position - all[j].transform.position;
                    delta.y = 0f;
                    float gap = delta.magnitude - radius - (float)Radius.GetValue(all[j]);
                    minimum = Mathf.Min(minimum, gap);
                    if (gap < -0.12f) throw new Exception("Enemies overlap by " + (-gap).ToString("F3") + " m.");
                }
            }
        }
        return minimum;
    }
}
