using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Optional check of the Mortar, Teleporter, Buffer and Armored enemies.
// Batch: -batchmode -executeMethod NewEnemyChecks.RunBatch (without -quit). Report: Logs/new-enemy-results.txt.
[InitializeOnLoad]
public static class NewEnemyChecks
{
    private const string Request = "Logs/new-enemy.request";
    private const string Report = "Logs/new-enemy-results.txt";
    private static CheckRunner routine;
    private static readonly List<string> lines = new List<string>();
    private static double started;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static NewEnemyChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Request)) EditorApplication.delayCall += Begin;
            if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Tick;
        };
    }

    public static void RunBatch()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Request, "batch");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.isPlaying = true;
    }

    private static void Begin()
    {
        File.Delete(Request);
        lines.Clear();
        started = EditorApplication.timeSinceStartup;
        routine = new CheckRunner(Checks());
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 120) throw new Exception("Check timed out.");
            if (!routine.MoveNext()) Finish(true, "New enemy checks complete.");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Pass(string text) { lines.Add("PASS: " + text); File.WriteAllLines(Report, lines); }
    private static void Require(bool condition, string text) { if (!condition) throw new Exception(text); }

    private static void Finish(bool success, string text)
    {
        EditorApplication.update -= Tick;
        lines.Add((success ? "PASS: " : "FAIL: ") + text);
        File.WriteAllLines(Report, lines);
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        double cap = EditorApplication.timeSinceStartup + 60;
        while (Time.time < end && EditorApplication.timeSinceStartup < cap) yield return null;
    }

    private static EnemyAbility Spawn(EnemyRole role, Vector3 position, Transform player)
    {
        GameObject root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy_" + role + ".prefab"), position, Quaternion.identity);
        root.GetComponent<EnemyAI>().target = player;
        return root.GetComponent<EnemyAbility>();
    }

    private static void Trigger(EnemyAbility actor, string method)
    {
        actor.GetComponent<EnemyAI>().enabled = false;
        typeof(EnemyAbility).GetField("nextAction", Private).SetValue(actor, Time.time + 1000f);
        actor.StartCoroutine((IEnumerator)typeof(EnemyAbility).GetMethod(method, Private).Invoke(actor, null));
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        GameObject player = PlayerProgression.Instance.gameObject;
        player.GetComponent<StartMenu>().SendMessage("BeginGame", 1);
        player.GetComponent<AutoShoter>().enabled = false;
        player.GetComponent<PlayMovement>().enabled = false;
        player.GetComponent<Rigidbody>().velocity = Vector3.zero;
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        health.IncreaseMaxHealth(2000);
        WaveManager waves = UnityEngine.Object.FindObjectOfType<WaveManager>();
        waves.StopSpawning();
        Require(GameSession.Instance.State == GameRunState.Playing, "Run did not start.");
        Transform hero = player.transform;
        Vector3 center = hero.position;

        // 1. Registered in the wave table with the right roles and gates.
        foreach (EnemyRole role in new[] { EnemyRole.Mortar, EnemyRole.Teleporter, EnemyRole.Buffer, EnemyRole.Armored })
        {
            EnemyType type = Array.Find(waves.enemies, e => e.prefab != null && e.prefab.GetComponent<EnemyAbility>()?.role == role);
            Require(type != null, "Not registered in the wave table: " + role);
            Require(type.minWave >= 6 && type.minSurvivalSeconds >= 60f, role + " is available too early.");
        }
        Pass("Mortar, Teleporter, Buffer, Armored are registered with wave and time gates.");

        // 2. Mortar: nothing before the shell lands, 22 damage if the hero stays, nothing if the hero leaves.
        EnemyAbility mortar = Spawn(EnemyRole.Mortar, center + Vector3.right * 10f, hero);
        yield return Wait(0.3f);
        Require(mortar.Ready && mortar.GetComponent<EnemyAI>().PreferredRange == 15f, "Mortar not configured.");
        int hp = health.CurrentHP;
        Trigger(mortar, "Mortar");
        yield return Wait(0.5f);
        Require(mortar.Telegraphing && health.CurrentHP == hp, "Mortar ring missing or damage came early.");
        yield return Wait(1.6f);
        Require(health.CurrentHP == hp - EnemyAbility.MortarDamage, "Mortar damage: " + (hp - health.CurrentHP));
        hp = health.CurrentHP;
        mortar.StopAllCoroutines();
        typeof(EnemyAbility).GetMethod("EndAction", Private).Invoke(mortar, new object[] { 0f });
        Trigger(mortar, "Mortar");
        yield return Wait(0.3f);
        Vector3 start = hero.position;
        hero.position = start + Vector3.forward * 6f;
        yield return Wait(1.8f);
        Require(health.CurrentHP == hp, "Mortar hit a hero who left the ring: " + (hp - health.CurrentHP));
        hero.position = start;
        UnityEngine.Object.Destroy(mortar.gameObject);
        Pass("Mortar: ring first, 22 damage when the hero stays, no damage when the hero steps out.");

        // 3. Teleporter: blinks next to the hero after a short warning.
        EnemyAbility teleporter = Spawn(EnemyRole.Teleporter, center + Vector3.right * 14f, hero);
        yield return Wait(0.3f);
        float before = Vector3.Distance(teleporter.transform.position, hero.position);
        Trigger(teleporter, "Teleport");
        yield return Wait(0.3f);
        Require(teleporter.Telegraphing, "Teleport destination ring missing.");
        yield return Wait(1.0f);
        float after = Vector3.Distance(teleporter.transform.position, hero.position);
        Require(before > 12f && after < 5f, "Teleport distance " + before + " -> " + after);
        UnityEngine.Object.Destroy(teleporter.gameObject);
        Pass("Teleporter: ring at the destination, then blinks from " + before.ToString("F1") + " m to " + after.ToString("F1") + " m.");

        // 4. Buffer: allies in the ring get the speed boost, far ones do not.
        EnemyType weakType = Array.Find(waves.enemies, e => e.name == "Weak") ?? waves.enemies[0];
        EnemyAbility buffer = Spawn(EnemyRole.Buffer, center + Vector3.right * 12f, hero);
        GameObject near = UnityEngine.Object.Instantiate(weakType.prefab, buffer.transform.position + Vector3.forward * 3f, Quaternion.identity);
        GameObject far = UnityEngine.Object.Instantiate(weakType.prefab, buffer.transform.position + Vector3.forward * 15f, Quaternion.identity);
        near.GetComponent<EnemyAI>().MovementLocked = true;
        far.GetComponent<EnemyAI>().MovementLocked = true;
        yield return Wait(0.3f);
        Trigger(buffer, "Buff");
        yield return Wait(1.2f);
        Require(near.GetComponent<EnemyAI>().Boosted, "Nearby ally not boosted.");
        Require(!far.GetComponent<EnemyAI>().Boosted, "Distant ally was boosted.");
        yield return Wait(4.2f);
        Require(!near.GetComponent<EnemyAI>().Boosted, "Boost did not expire.");
        UnityEngine.Object.Destroy(buffer.gameObject);
        UnityEngine.Object.Destroy(near);
        UnityEngine.Object.Destroy(far);
        Pass("Buffer: ally within 7 m boosted, distant one not, boost expires after 4 s.");

        // 5. Armored: half damage, at least 1.
        EnemyAbility armored = Spawn(EnemyRole.Armored, center + Vector3.left * 12f, hero);
        armored.GetComponent<EnemyAI>().MovementLocked = true;
        yield return Wait(0.3f);
        Health armoredHealth = armored.GetComponent<Health>();
        armoredHealth.SetHealth(1000);
        armoredHealth.TakeDamage(20);
        Require(armoredHealth.CurrentHealth == 990, "Armored took " + (1000 - armoredHealth.CurrentHealth) + " from 20.");
        armoredHealth.TakeDamage(1);
        Require(armoredHealth.CurrentHealth == 989, "Armored took less than 1 from 1.");
        UnityEngine.Object.Destroy(armored.gameObject);
        Pass("Armored: 20 damage becomes 10, minimum 1.");
    }
}
