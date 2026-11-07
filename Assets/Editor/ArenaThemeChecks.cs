using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Необязательная проверка тем арены: планировка, проходимость, производительность поиска пути, окружение, снимки, бой в «Пещере».
// Batch: -batchmode -executeMethod ArenaThemeChecks.RunBatch (без -quit). Отчёт: Logs/arena-theme-results.txt, кадры docs/art/Arena_*.png.
[InitializeOnLoad]
public static class ArenaThemeChecks
{
    private const string Request = "Logs/arena-theme.request";
    private const string Report = "Logs/arena-theme-results.txt";
    private static CheckRunner routine;
    private static readonly List<string> lines = new List<string>();
    private static double started;
    private static int errorLogs;

    static ArenaThemeChecks()
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
        errorLogs = 0;
        Application.logMessageReceived += OnLog;
        started = EditorApplication.timeSinceStartup;
        routine = new CheckRunner(Checks());
        EditorApplication.update += Tick;
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception) { errorLogs++; lines.Add("LOG " + type + ": " + message); }
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 240) throw new Exception("Check timed out.");
            if (!routine.MoveNext()) Finish(true, "Arena theme checks complete.");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Pass(string text) { lines.Add("PASS: " + text); File.WriteAllLines(Report, lines); }
    private static void Require(bool condition, string text) { if (!condition) throw new Exception(text); }

    private static void Finish(bool success, string text)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        ArenaThemes.Set(ArenaTheme.Factory, false);
        lines.Add((success ? "PASS: " : "FAIL: ") + text);
        File.WriteAllLines(Report, lines);
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        double cap = EditorApplication.timeSinceStartup + 60;
        while (Time.time < end && EditorApplication.timeSinceStartup < cap)
        {
            if (PlayerProgression.Instance != null && PlayerProgression.Instance.Choosing) PlayerProgression.Instance.SelectOfferedUpgrade(0);
            yield return null;
        }
    }

    private static void Shot(Camera cam, string path)
    {
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null; RenderTexture.active = null;
        UnityEngine.Object.Destroy(rt);
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        GameObject player = PlayerProgression.Instance.gameObject;
        Directory.CreateDirectory("docs/art");
        var wideGo = new GameObject("WideCam");
        Camera wide = wideGo.AddComponent<Camera>();
        wide.fieldOfView = 60f;
        wide.transform.position = new Vector3(0f, 175f, -95f);
        wide.transform.LookAt(Vector3.zero);

        foreach (ArenaTheme theme in new[] { ArenaTheme.Factory, ArenaTheme.Cave, ArenaTheme.Glacier })
        {
            ArenaThemes.Set(theme, false);
            ArenaThemes.Apply();
            yield return null;
            yield return null;
            ArenaLayout arena = ArenaLayout.Instance;
            Require(arena != null, theme + ": no ArenaLayout.");
            int count = arena.Obstacles.Count;
            Require(count >= 10 && count <= 36, theme + ": obstacle count " + count);
            Require(arena.IsWalkable(Vector3.zero, 1f), theme + ": centre not walkable.");
            foreach (Bounds b in arena.Obstacles)
                Require(Mathf.Max(Mathf.Abs(b.center.x) - b.extents.x, Mathf.Abs(b.center.z) - b.extents.z) > 9f, theme + ": obstacle too close to the centre " + b.center);
            Require(arena.transform.childCount > 40, theme + ": too few arena objects " + arena.transform.childCount);
            Require(RenderSettings.fog == (theme != ArenaTheme.Factory) || theme == ArenaTheme.Factory, theme + ": fog state.");

            // Поиск пути: 40 запросов из разных углов к центру укладываются в бюджет.
            var rng = new System.Random(5);
            var sw = Stopwatch.StartNew();
            int found = 0;
            for (int i = 0; i < 40; i++)
            {
                Vector3 from = new Vector3((float)(rng.NextDouble() * 2 - 1) * 95f, 0f, (float)(rng.NextDouble() * 2 - 1) * 95f);
                if (!arena.IsWalkable(from, 0.5f)) continue;
                Vector3 next = arena.NextWaypoint(from, Vector3.zero, 0.5f);
                if ((next - from).sqrMagnitude > 0.01f) found++;
            }
            sw.Stop();
            Require(found >= 20, theme + ": path found only " + found + " of 40.");
            Require(sw.ElapsedMilliseconds < 1500, theme + ": pathfinding too slow: " + sw.ElapsedMilliseconds + " ms for 40.");

            bool fogWas = RenderSettings.fog; RenderSettings.fog = false;
            Shot(wide, "docs/art/Arena_" + theme + "_wide.png");
            RenderSettings.fog = fogWas;
            Shot(Camera.main, "docs/art/Arena_" + theme + "_game.png");
            Pass(theme + ": obstacles=" + count + ", objects=" + arena.transform.childCount + ", paths " + found + "/40 in " + sw.ElapsedMilliseconds + " ms, snapshots saved.");
        }
        UnityEngine.Object.Destroy(wideGo);

        // Бой в «Пещере»: забег запускается и идёт без ошибок.
        ArenaThemes.Set(ArenaTheme.Cave, false);
        ArenaThemes.Apply();
        yield return null;
        player.GetComponent<StartMenu>().SendMessage("BeginGame", 1);
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        health.IncreaseMaxHealth(2000);
        Require(GameSession.Instance.State == GameRunState.Playing, "Run did not start in the cave.");
        yield return Wait(12f);
        Require(health.CurrentHP > 0, "Hero died in the cave.");
        Require(EnemyAI.ActiveCount > 0, "No enemies spawned in the cave.");
        Pass("12 s of combat in the cave: hero alive, " + EnemyAI.ActiveCount + " enemies active.");
        Require(errorLogs == 0, "Error logs during play: " + errorLogs);
        Pass("No Error/Exception logs during the run.");
    }
}
