using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Замер позднего пика: врагов на поле и времени кадра при максимальном давлении (волна 25, 9-я минута, игрок стоит и не стреляет).
// Меню: Tools/Checks/Late peak perf. Batch: -batchmode -executeMethod LatePeakPerfChecks.RunBatch (без -quit).
// Отчёт: Logs/late-peak-perf.txt. Время кадра в редакторе завышено; это сравнительная оценка, а не FPS билда.
[InitializeOnLoad]
public static class LatePeakPerfChecks
{
    private const string Request = "Logs/late-peak-perf.request";
    private const string Report = "Logs/late-peak-perf.txt";
    private const float SampleSeconds = 60f; // игровых секунд при timeScale 4
    private const float TimeScale = 4f;
    private static CheckRunner routine;
    private static readonly List<string> lines = new List<string>();
    private static double started;

    static LatePeakPerfChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Request)) EditorApplication.delayCall += Begin;
            if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Tick;
        };
    }

    [MenuItem("Tools/Checks/Late peak perf")]
    public static void RunMenu()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Request, "menu");
        EditorSceneManager.OpenScene("Assets/Scenes/GreenArenaPlay.unity");
        EditorApplication.isPlaying = true;
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
        string mode = File.ReadAllText(Request);
        File.Delete(Request);
        lines.Clear();
        started = EditorApplication.timeSinceStartup;
        routine = new CheckRunner(Measure(mode == "batch"));
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 260) throw new Exception("Timed out.");
            if (!routine.MoveNext()) Finish(true, "Late peak measurement complete.", true);
        }
        catch (Exception exception) { Finish(false, exception.ToString(), true); }
    }

    private static void Finish(bool success, string text, bool exit)
    {
        EditorApplication.update -= Tick;
        lines.Add((success ? "PASS: " : "FAIL: ") + text);
        File.WriteAllLines(Report, lines);
        if (exit && Environment.CommandLine.Contains("-batchmode")) EditorApplication.Exit(success ? 0 : 1);
        else EditorApplication.isPlaying = false;
    }

    private static IEnumerator Measure(bool batch)
    {
        yield return null;
        MetaProgress.UseMemory();
        GameObject player = PlayerProgression.Instance.gameObject;
        player.GetComponent<StartMenu>().SendMessage("BeginGame", 1);
        player.GetComponent<AutoShoter>().enabled = false;
        player.GetComponent<PlayMovement>().enabled = false;
        player.GetComponent<PlayerHealth>().IncreaseMaxHealth(1000000);
        WaveManager waves = UnityEngine.Object.FindObjectOfType<WaveManager>();
        GameSession session = GameSession.Instance;
        if (session.State != GameRunState.Playing) throw new Exception("Run did not start.");

        // Поздняя стадия: волна 25 и 540 с (все типы врагов открыты).
        waves.wave = 25;
        typeof(GameSession).GetProperty("Elapsed", BindingFlags.Public | BindingFlags.Instance)
            .SetValue(session, 540f, null);

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Time.timeScale = TimeScale;
        var frameMs = new List<float>();
        int lastFrame = -1;
        int peakAll = 0, peakNear = 0;
        float peakAt = 0f;
        float end = Time.time + SampleSeconds;
        double cap = EditorApplication.timeSinceStartup + 170;
        float warm = Time.time + 20f; // даём толпе набраться
        while (Time.time < end && EditorApplication.timeSinceStartup < cap)
        {
            // Выбор карточки/пауза сбрасывают timeScale в 1 — возвращаем ускорение, когда игра идёт.
            if (Time.timeScale > 0f && Time.timeScale < TimeScale && session.State == GameRunState.Playing) Time.timeScale = TimeScale;
            if (Time.frameCount == lastFrame) { yield return null; continue; }
            lastFrame = Time.frameCount;
            int all = EnemyAI.ActiveCount;
            int near = EnemyAI.CountWithin(player.transform.position, waves.settings.pressureRadius);
            if (all > peakAll) { peakAll = all; peakAt = Time.time; }
            if (near > peakNear) peakNear = near;
            if (Time.time > warm) frameMs.Add(Time.unscaledDeltaTime * 1000f);
            yield return null;
        }

        frameMs.Sort();
        float sum = 0f;
        foreach (float ms in frameMs) sum += ms;
        float avg = frameMs.Count > 0 ? sum / frameMs.Count : 0f;
        float p95 = frameMs.Count > 0 ? frameMs[(int)(frameMs.Count * 0.95f)] : 0f;
        float p99 = frameMs.Count > 0 ? frameMs[(int)(frameMs.Count * 0.99f)] : 0f;
        float worst = frameMs.Count > 0 ? frameMs[frameMs.Count - 1] : 0f;
        Time.timeScale = 1f;
        lines.Add("Врагов в конце=" + EnemyAI.ActiveCount + ", игровое время замера=" + (SampleSeconds - Mathf.Max(0f, end - Time.time)).ToString("0") + " с");
        lines.Add("Лимиты волны 25: давление=" + waves.settings.AliveLimitForWave(25) + ", всего=" + waves.settings.AbsoluteLimitForWave(25));
        lines.Add("Пик врагов всего=" + peakAll + " (в " + (peakAt - (end - SampleSeconds)).ToString("0") + " игр. с), пик в радиусе давления=" + peakNear);
        lines.Add("Кадры=" + frameMs.Count + " среднее=" + avg.ToString("0.0") + " мс (" + (avg > 0 ? 1000f / avg : 0).ToString("0") +
            " FPS), p95=" + p95.ToString("0.0") + " мс, p99=" + p99.ToString("0.0") + " мс, худший=" + worst.ToString("0.0") + " мс");
        lines.Add("Редактор" + (batch ? " (batch, без окна)" : "") + "; значения завышены против билда. Не геймплейная проверка.");
    }
}
