using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Opt-in short real-coroutine check; reloads the scene to remove test-only changes.
[InitializeOnLoad]
public static class SpecialEnemyChecks
{
    private const string Request = "Logs/special-enemies.request";
    private static IEnumerator routine;
    private static double deadline;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> report = new List<string>();
    private static readonly List<GameObject> created = new List<GameObject>();
    static SpecialEnemyChecks() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying) return;
        if (!EditorApplication.isPlaying)
        {
            routine = null;
            if (!File.Exists(Request)) return;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            {
                File.Delete(Request);
                File.WriteAllText("Logs/special-enemies-report.txt", "FAIL: save or preserve unsaved scene changes before this check.\n");
                return;
            }
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            File.WriteAllText(Request, "run");
            EditorApplication.isPlaying = true;
            return;
        }
        if (routine == null)
        {
            if (!File.Exists(Request)) return;
            if (File.ReadAllText(Request).Trim() == "reload")
            {
                StartMenu menu = UnityEngine.Object.FindObjectOfType<StartMenu>();
                if (menu == null || !menu.WaitingForWeapon)
                {
                    File.Delete(Request);
                    File.WriteAllText("Logs/special-enemies-report.txt", "FAIL: return to menu before this check.\n");
                    return;
                }
                File.WriteAllText(Request, "run");
                EditorApplication.isPlaying = false;
                return;
            }
            if (PlayerProgression.Instance == null) return;
            File.Delete(Request);
            report.Clear();
            deadline = EditorApplication.timeSinceStartup + 45;
            routine = Run();
        }
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Special enemy check timed out.");
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception exception) { Finish(exception); }
    }
    private static void Finish(Exception exception)
    {
        report.Add(exception == null ? "PASS: combined role check complete." : "FAIL: " + exception);
        File.WriteAllLines("Logs/special-enemies-report.txt", report);
        routine = null;
        foreach (GameObject item in created) if (item != null) UnityEngine.Object.DestroyImmediate(item);
        created.Clear();
        Time.timeScale = 1f;
        if (GameSession.Instance != null) GameSession.Instance.Reload(false);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static EnemyAbility Spawn(EnemyRole role, Transform player)
    {
        string path = "Assets/Enemy_" + role + ".prefab";
        GameObject root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), player.position + Vector3.right * 6f, Quaternion.identity);
        created.Add(root);
        root.GetComponent<EnemyAI>().target = player;
        return root.GetComponent<EnemyAbility>();
    }
    private static void Trigger(EnemyAbility actor, string method)
    {
        actor.GetComponent<EnemyAI>().enabled = false;
        typeof(EnemyAbility).GetField("nextAction", Private).SetValue(actor, Time.time + 1000f);
        actor.StartCoroutine((IEnumerator)typeof(EnemyAbility).GetMethod(method, Private).Invoke(actor, null));
    }
    private static IEnumerator Run()
    {
        PlayerProgression progression = PlayerProgression.Instance;
        Require(GameSession.Instance.State == GameRunState.Menu, "Start this check from the menu, not inside a run.");
        progression.GetComponent<StartMenu>().SendMessage("BeginGame", 1);
        Transform player = progression.transform;
        PlayerHealth playerHP = progression.GetComponent<PlayerHealth>();
        progression.GetComponent<AutoShoter>().enabled = false;
        progression.GetComponent<PlayMovement>().enabled = false;
        progression.GetComponent<Rigidbody>().velocity = Vector3.zero;
        WaveManager waves = UnityEngine.Object.FindObjectOfType<WaveManager>();
        waves.StopSpawning();
        Require(waves.enemies.Length == 8, "Expected 8 authored enemy types in the loaded scene.");
        foreach (EnemyRole role in Enum.GetValues(typeof(EnemyRole)))
            Require(Array.Exists(waves.enemies, e => e.prefab.GetComponent<EnemyAbility>()?.role == role), "Role missing from waves: " + role);
        report.Add("PASS: all 8 types registered, 5 distinct ability prefabs.");

        EnemyAbility shooter = Spawn(EnemyRole.Shooter, player);
        while (!shooter.Ready) yield return null;
        Require(shooter.GetComponent<EnemyAI>().PreferredRange == 9f && !shooter.GetComponent<EnemyAI>().UsesMelee, "Ranged behavior not configured.");
        int before = playerHP.CurrentHP;
        Trigger(shooter, "Shoot");
        Require(shooter.Telegraphing, "Shooter warning missing.");
        GameSession.Instance.TogglePause();
        double until = EditorApplication.timeSinceStartup + 0.35;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Require(shooter.Telegraphing && playerHP.CurrentHP == before, "Shot advanced during pause.");
        GameSession.Instance.TogglePause();
        float end = Time.time + 1.8f;
        while (Time.time < end) yield return null;
        Require(playerHP.CurrentHP == before - 10, "Ranged projectile failed to hit exactly once.");
        UnityEngine.Object.DestroyImmediate(shooter.gameObject);
        report.Add("PASS: shooter distance behavior, visible warning, pause freeze, real projectile hit (10 damage).");

        EnemyAbility charger = Spawn(EnemyRole.Charger, player);
        while (!charger.Ready) yield return null;
        before = playerHP.CurrentHP;
        Vector3 chargeStart = charger.transform.position;
        Trigger(charger, "Charge");
        Require(charger.Telegraphing, "Charge warning missing.");
        end = Time.time + 1.9f;
        while (Time.time < end) yield return null;
        Require(playerHP.CurrentHP == before - 18 && Vector3.Distance(chargeStart, charger.transform.position) > 5f, "Charge movement/damage failed.");
        UnityEngine.Object.DestroyImmediate(charger.gameObject);
        report.Add("PASS: charger warning, actual dash and one 18-damage hit.");

        EnemyAbility healer = Spawn(EnemyRole.Healer, player);
        GameObject ally = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy_Weak.prefab"), player.position + Vector3.right * 8f, Quaternion.identity);
        created.Add(ally);
        ally.GetComponent<EnemyAI>().enabled = false;
        while (!healer.Ready) yield return null;
        Health allyHP = ally.GetComponent<Health>();
        allyHP.TakeDamage(40);
        Trigger(healer, "HealAllies");
        end = Time.time + 1f;
        while (Time.time < end) yield return null;
        Require(allyHP.CurrentHealth == 72 && healer.GetComponent<Health>().CurrentHealth == healer.GetComponent<Health>().maxHealth, "Healing/cap mismatch.");
        Require(allyHP.Heal(999) == 28 && allyHP.CurrentHealth == 100, "Heal exceeds max health.");
        UnityEngine.Object.DestroyImmediate(healer.gameObject);
        UnityEngine.Object.DestroyImmediate(ally);
        report.Add("PASS: healer restores 12 HP to neighbor and healing is capped.");

        EnemyAbility bomber = Spawn(EnemyRole.Bomber, player);
        bomber.transform.position = player.position + Vector3.right * 2.5f;
        while (!bomber.Ready) yield return null;
        before = playerHP.CurrentHP;
        Trigger(bomber, "Explode");
        Require(bomber.Telegraphing, "Bomb warning missing.");
        end = Time.time + 1.5f;
        while (Time.time < end) yield return null;
        Require(bomber == null && playerHP.CurrentHP == before - 25, "Bomb damage or self-destruction failed.");
        report.Add("PASS: bomber radius warning, delayed 25 damage and self-destruction.");

        EnemyAbility splitter = Spawn(EnemyRole.Splitter, player);
        while (!splitter.Ready) yield return null;
        splitter.GetComponent<Health>().TakeDamage(9999);
        yield return null;
        var fragments = new List<EnemyAI>();
        foreach (EnemyAI actor in UnityEngine.Object.FindObjectsOfType<EnemyAI>())
            if (actor.name == "Splitter fragment") { fragments.Add(actor); created.Add(actor.gameObject); }
        Require(fragments.Count == 0, "Splitter spawned while wave manager is suspended.");
        // Unsuspend only this controlled phase; the original spawn coroutine remains stopped.
        typeof(WaveManager).GetField("<Suspended>k__BackingField", Private).SetValue(waves, false);
        splitter = Spawn(EnemyRole.Splitter, player);
        while (!splitter.Ready) yield return null;
        splitter.GetComponent<Health>().TakeDamage(9999);
        yield return null;
        foreach (EnemyAI actor in UnityEngine.Object.FindObjectsOfType<EnemyAI>())
            if (actor.name == "Splitter fragment") { fragments.Add(actor); created.Add(actor.gameObject); }
        Require(fragments.Count == 2 && EnemyAI.ActiveCount <= waves.settings.AliveLimitForWave(waves.wave), "Split count exceeds population cap.");
        foreach (EnemyAI fragment in fragments)
            Require(fragment.GetComponent<Health>().maxHealth == 20 && fragment.GetComponent<Health>().experienceReward == 0 && fragment.GetComponent<EnemyAbility>() == null, "Recursive/rewarding fragments.");
        report.Add("PASS: splitter blocked during boss preparation; two non-recursive, zero-XP fragments within alive cap.");
    }
}
