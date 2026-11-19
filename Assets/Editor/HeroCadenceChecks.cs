using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HeroCadenceChecks
{
    private const string Request = "Logs/hero-cadence.request";
    private const string Report = "Logs/hero-cadence-results.txt";
    private static CheckRunner runner;
    private static double started;
    private static int errors;
    private static byte[] languageBackup;
    private static bool hadLanguageSettings;
    private static readonly List<string> results = new List<string>();
    private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    static HeroCadenceChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Request)) EditorApplication.delayCall += Begin;
        };
    }

    public static void RunBatch()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Request, "batch");
        EditorSceneManager.OpenScene("Assets/Scenes/GreenArenaPlay.unity");
        EditorApplication.isPlaying = true;
    }

    private static void Begin()
    {
        File.Delete(Request);
        results.Clear();
        started = EditorApplication.timeSinceStartup;
        errors = 0;
        hadLanguageSettings = File.Exists(GameText.SettingsPath);
        languageBackup = hadLanguageSettings ? File.ReadAllBytes(GameText.SettingsPath) : null;
        Application.logMessageReceived += OnLog;
        runner = new CheckRunner(Checks());
        EditorApplication.update += Tick;
    }

    private static void OnLog(string text, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors++;
            results.Add("RUNTIME " + text);
        }
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 180) throw new Exception("Hero cadence checks timed out");
            if (!runner.MoveNext()) Finish(errors == 0, "Hero cadence checks complete; runtime errors=" + errors);
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    private static void Finish(bool ok, string detail)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        if (hadLanguageSettings) File.WriteAllBytes(GameText.SettingsPath, languageBackup);
        else if (File.Exists(GameText.SettingsPath)) File.Delete(GameText.SettingsPath);
        results.Add((ok ? "PASS " : "FAIL ") + detail);
        File.WriteAllLines(Report, results);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static void Require(bool ok, string detail) { if (!ok) throw new Exception(detail); }
    private static void Pass(string detail) { results.Add("PASS " + detail); File.WriteAllLines(Report, results); }
    private static IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }

    private static Health Dummy(Vector3 position)
    {
        var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = "Aim check target";
        item.tag = "Enemy";
        item.transform.position = position;
        var health = item.AddComponent<Health>();
        health.SetHealth(100000);
        health.experienceReward = 0;
        item.AddComponent<EnemyAI>().MovementLocked = true;
        return health;
    }

    private static float AimError(Transform muzzle, Transform target)
    {
        var fx = muzzle.GetComponentInParent<WeaponFireAnimation>();
        return Vector3.Angle(fx.BarrelForward, target.GetComponent<Collider>().bounds.center - muzzle.position);
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        MetaProgress.Data.ranks.Add(new MetaEntry { key = "workshop:project:generator", value = 2 });
        var shooter=Object.FindObjectOfType<AutoShoter>();
        var menu=shooter.GetComponent<StartMenu>();menu.ShowWeapons();
        typeof(StartMenu).GetMethod("BeginGame",PrivateInstance).Invoke(menu,new object[]{0});
        yield return Wait(.4f);
        Object.FindObjectOfType<WaveManager>().StopSpawning();
        foreach(var enemy in Object.FindObjectsOfType<EnemyAI>())Object.Destroy(enemy.gameObject);
        yield return null;
        var movement=shooter.GetComponent<PlayMovement>();
        var hero=shooter.GetComponent<HeroLocomotion>();
        var animation=shooter.GetComponentInChildren<Animation>();
        shooter.range=30;shooter.SetGrenadeUnlocked(false);
        var target=Dummy(shooter.transform.position+Vector3.forward*20);
        movement.CheckInput=Vector2.up;
        yield return Wait(.9f);
        var state=animation[hero.CurrentPose];
        Require(hero.CurrentPose=="Run" && state.speed>.8f && state.speed<1.15f,"Normal cadence="+state.speed+" pose="+hero.CurrentPose);
        Require(Mathf.Abs(movement.EffectiveSpeed-5)<.01f,"Movement speed changed");
        float cycle=state.normalizedTime,time=Time.time;
        yield return Wait(.8f);
        float cyclesPerSecond=(state.normalizedTime-cycle)/(Time.time-time);
        Require(cyclesPerSecond>.9f && cyclesPerSecond<2.2f,"Actual run cycle rate="+cyclesPerSecond);
        Pass("Normal: animation speed="+state.speed+", cycles/s="+cyclesPerSecond+", movement="+movement.EffectiveSpeed);
        target.transform.position=shooter.transform.position+Vector3.forward*20;
        Require(shooter.GetComponent<HeroAbilities>().TryOverdrive(),"Overdrive failed");
        yield return Wait(.8f);
        state=animation[hero.CurrentPose];
        Require(state.speed<=1.351f && Mathf.Abs(movement.EffectiveSpeed-6.25f)<.01f,"Overdrive cadence or movement incorrect");
        Pass("Overdrive cadence="+state.speed+", movement="+movement.EffectiveSpeed);
        target.transform.position=shooter.transform.position+Vector3.forward*20;
        movement.SpeedMultiplier=2;
        yield return Wait(.8f);
        Require(animation[hero.CurrentPose].speed<=1.351f && movement.EffectiveSpeed>10,"Fast upgrades uncapped cadence or movement nerf");
        Pass("Fast movement remains boosted; animation capped at "+animation[hero.CurrentPose].speed);
        movement.CheckInput=Vector2.zero;
        yield return Wait(.6f);
        Require(hero.CurrentPose=="Idle" || hero.CurrentPose.StartsWith("Turn"),"Did not leave run after stopping");
        Pass("Stop returns to non-travel pose");
    }
}
