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
public static class RunResultsChecks
{
    private const string Request = "Logs/run-results.request";
    private const string Report = "Logs/run-results-results.txt";
    private static CheckRunner runner;
    private static double started;
    private static int errors;
    private static byte[] languageBackup;
    private static bool hadLanguageSettings;
    private static readonly List<string> results = new List<string>();
    private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    static RunResultsChecks()
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
            if (EditorApplication.timeSinceStartup - started > 180) throw new Exception("Run results checks timed out");
            if (!runner.MoveNext()) Finish(errors == 0, "Run results checks complete; runtime errors=" + errors);
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

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        MetaProgress.Data.currency = 100000;
        foreach (MetaUnlock unlock in MetaProgress.Unlocks) MetaProgress.Unlock(unlock.id);
        WeaponElements.Buy(1, WeaponElement.Heavy);
        WeaponElements.Equip(1, WeaponElement.Heavy);
        var shooter = Object.FindObjectOfType<AutoShoter>();
        var menu = shooter.GetComponent<StartMenu>();
        menu.ShowWeapons();
        typeof(StartMenu).GetMethod("BeginGame", PrivateInstance).Invoke(menu, new object[] { 1 });
        yield return null;
        var session = shooter.GetComponent<GameSession>();
        var stats = shooter.GetComponent<RunStatistics>();
        var hero = shooter.GetComponent<PlayerHealth>();
        var progression = shooter.GetComponent<PlayerProgression>();
        Require(session.State == GameRunState.Playing, "Run did not start");
        Pass("Run started");
        shooter.GetComponent<WaveManager>()?.StopSpawning();
        hero.maxHP = 1000;
        hero.Heal(1000);
        progression.Stats.Armor = .5f;
        hero.TakeDamage(40, HeroDamageCause.Contact);
        yield return Wait(.2f);
        hero.TakeDamage(20, HeroDamageCause.Fire);
        hero.GrantInvulnerability(.1f);
        hero.TakeDamage(500, HeroDamageCause.Meteor);
        Require(stats.Report.DamageTaken == 30 && stats.Report.incoming.Count == 2, "Armor or ignored damage tracking failed");
        Pass("Incoming damage excludes armor and invulnerability");
        yield return Wait(.15f);
        stats.BeginBoss(BossKind.SwarmQueen);
        float start = session.Elapsed;
        yield return Wait(.2f);
        session.TogglePause();
        float paused = session.Elapsed;
        double until = EditorApplication.timeSinceStartup + .2;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Require(session.Elapsed == paused, "Pause advanced session timer");
        session.TogglePause();
        stats.EndBoss(true);
        Require(Mathf.Abs(stats.Report.BossSeconds - (session.Elapsed - start)) < .01f && stats.Report.bosses[0].defeated, "Boss timer did not exclude pause");
        Pass("Boss timer excludes pause");
        var ids = new HashSet<string>();
        foreach (var upgrade in RunReport.Definitions(progression))
        {
            if (!ids.Add(upgrade.id) || upgrade.effect == UpgradeEffect.Evolution || upgrade.effect == UpgradeEffect.ChestXp || !progression.Available(upgrade, true)) continue;
            progression.TestGrant(upgrade);
        }
        stats.RecordDamage(125000);
        for (int i = 0; i < 126; i++) stats.RecordKill();
        typeof(GameSession).GetProperty("Elapsed").SetValue(session, 625f);
        stats.BeginBoss(BossKind.StormSniper);
        yield return Wait(.1f);
        typeof(GameSession).GetProperty("Elapsed").SetValue(session, 668.7f);
        int remaining = hero.CurrentHP;
        hero.TakeDamage(100000, HeroDamageCause.Lightning);
        Require(session.IsFinished, "Defeat did not finish run");
        Require(stats.Report.DamageTaken == 30 + remaining, "Overkill counted as lost HP");
        Require(stats.Report.heroes.Count == 1 && stats.Report.heroes[0].upgrades.Count > 8, "Build snapshot incomplete");
        Require(stats.Report.heroes[0].branch == WeaponElement.Heavy, "Branch snapshot missing");
        Require(stats.Report.hasDeathCause && stats.Report.deathCause == HeroDamageCause.Lightning, "Death source not saved");
        Require(!stats.Report.bosses[1].defeated, "Interrupted boss marked defeated");
        Require(MetaProgress.Data.lastRun.report == stats.Report, "Result report not attached to save");
        string json = JsonUtility.ToJson(MetaProgress.Data);
        var loaded = JsonUtility.FromJson<MetaSave>(json);
        Require(loaded.lastRun.report.DamageTaken == stats.Report.DamageTaken && loaded.lastRun.report.heroes[0].upgrades.Count == stats.Report.heroes[0].upgrades.Count, "Save roundtrip lost report");
        var old = JsonUtility.FromJson<MetaSave>("{\"hasLastRun\":true,\"lastRun\":{\"weapon\":0}}");
        Require(old.lastRun.weapon == 0 && (old.lastRun.report == null || old.lastRun.report.heroes == null || old.lastRun.report.heroes.Count == 0), "Legacy report compatibility");
        Pass("Real health hits: armor, invulnerability, overkill; boss pause/completion/interruption; branch/upgrades/death; current and legacy save");
        foreach (GameLanguage language in new[] { GameLanguage.English, GameLanguage.Russian })
        {
            GameText.SetLanguage(language);
            Canvas.ForceUpdateCanvases();
            Capture("run-results-" + (language == GameLanguage.English ? "en" : "ru"), hero.transform, false);
        }
        var root = (RectTransform)typeof(GameSession).GetField("result", PrivateInstance).GetValue(session);
        ScrollRect[] scrolls = root.GetComponentsInChildren<ScrollRect>();
        Require(scrolls.Length == 2 && scrolls[0].content.rect.height > scrolls[0].viewport.rect.height, "Long build is not scrollable");
        Require(scrolls[0].verticalScrollbar != null, "Visible scrollbar missing");
        foreach (var canvas in Object.FindObjectsOfType<Canvas>())
            Require(!canvas.isRootCanvas || canvas.sortingOrder >= 150 || !canvas.enabled, "Combat HUD still visible behind results");
        scrolls[0].verticalNormalizedPosition = 0;
        Canvas.ForceUpdateCanvases();
        Capture("run-results-scroll", hero.transform, false);
        Pass("RU/EN results, two masked scroll lists and long build");
        // Exercise the victory UI without awarding or writing another real run.
        root.gameObject.SetActive(false);
        session.Begin(1);
        typeof(GameSession).GetMethod("Finish", PrivateInstance).Invoke(session, new object[] { true });
        Require(session.State == GameRunState.Victory, "Victory result missing");
        Capture("run-results-victory", hero.transform, false);
        var endless = MetaProgress.RecordEndless(1, session.Elapsed, stats.Kills, progression.Level, 1000, 2, stats.DamageDealt, stats.Report);
        Require(MetaProgress.Data.lastRun.report == stats.Report && MetaProgress.Data.lastRun.reward == endless.reward, "Endless snapshot missing");
        Pass("Victory and reward screen");
    }

    private static void Capture(string name, Transform hero, bool closeup)
    {
        Camera camera = Camera.main;
        Vector3 position = camera.transform.position;
        Quaternion rotation = camera.transform.rotation;
        bool orthographic = camera.orthographic;
        float size = camera.orthographicSize;
        var canvases = new List<Canvas>();
        foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>())
        {
            if (!canvas.enabled || !canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvases.Add(canvas);
            if (closeup) canvas.enabled = false;
        }
        if (closeup)
        {
            camera.transform.position = hero.position + new Vector3(0, 5, -4);
            camera.transform.LookAt(hero.position + Vector3.up);
            camera.orthographic = true;
            camera.orthographicSize = 2f;
        }
        var texture = new RenderTexture(1280,720,24);
        RenderTexture previous = camera.targetTexture, active = RenderTexture.active;
        camera.targetTexture = texture;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = texture;
        var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1280,720),0,0);
        image.Apply();
        File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());
        camera.targetTexture = previous;
        RenderTexture.active = active;
        camera.transform.SetPositionAndRotation(position,rotation);
        camera.orthographic = orthographic;
        camera.orthographicSize = size;
        foreach (Canvas canvas in canvases) { canvas.enabled=true; canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null; }
        Object.Destroy(image);
        Object.Destroy(texture);
    }
}
