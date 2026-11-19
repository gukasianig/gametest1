using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Мини-босс «Громила» (11.11.2026): появляется на середине забега, полоса HP, удар по земле и рывок, награда — сундук.
// Пакетный запуск: -batchmode -executeMethod MiniBossChecks.RunBatch (без -quit). Отчёт: Logs/mini-boss-results.txt, кадр Logs/mini-boss.png.
[InitializeOnLoad]
public static class MiniBossChecks
{
    private const string Request = "Logs/mini-boss.request";
    private const string Report = "Logs/mini-boss-results.txt";
    private static CheckRunner routine;
    private static readonly List<string> lines = new List<string>();
    private static double started;

    static MiniBossChecks()
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
        EditorSceneManager.OpenScene("Assets/Scenes/GreenArenaPlay.unity");
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
            if (EditorApplication.timeSinceStartup - started > 100) throw new Exception("Проверка не уложилась во время.");
            if (!routine.MoveNext()) Finish(true, "Проверки мини-босса пройдены.");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Pass(string text) { lines.Add("ПРОЙДЕНО: " + text); File.WriteAllLines(Report, lines); }
    private static void Require(bool condition, string text) { if (!condition) throw new Exception(text); }

    private static void Finish(bool success, string text)
    {
        EditorApplication.update -= Tick;
        lines.Add((success ? "ПРОЙДЕНО: " : "ОШИБКА: ") + text);
        File.WriteAllLines(Report, lines);
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static IEnumerator WaitReal(double seconds)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < end) yield return null;
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        GameObject hero = PlayerProgression.Instance.gameObject;
        yield return WaitReal(1.6);
        hero.GetComponent<StartMenu>().SendMessage("BeginGame", (object)1);
        WaveManager waves = UnityEngine.Object.FindObjectOfType<WaveManager>();
        waves.StopSpawning();
        yield return WaitReal(0.4);
        foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>()) UnityEngine.Object.Destroy(enemy.gameObject);
        hero.GetComponent<AutoShoter>().enabled = false;
        GameSession session = GameSession.Instance;
        PlayerHealth health = hero.GetComponent<PlayerHealth>();
        health.IncreaseMaxHealth(5000);

        // 1. До середины забега Громилы нет, на середине — появляется с полосой HP.
        yield return WaitReal(0.5);
        Require(MiniBoss.Active == null, "Громила появился раньше середины забега.");
        typeof(GameSession).GetProperty("Elapsed").GetSetMethod(true).Invoke(session, new object[] { session.survivalSeconds * MiniBoss.SpawnShare });
        double by = EditorApplication.timeSinceStartup + 3;
        while (MiniBoss.Active == null && EditorApplication.timeSinceStartup < by) yield return null;
        MiniBoss brute = MiniBoss.Active;
        Require(brute != null, "Громила не появился на середине забега.");
        yield return WaitReal(0.3);
        GameObject bar = GameObject.Find("Boss HP");
        Require(bar != null && bar.activeInHierarchy, "Нет полосы здоровья Громилы.");
        Require(brute.Health.maxHealth == Mathf.RoundToInt(MiniBoss.BaseHealth * RunModifiers.BossHealth) && brute.Health.experienceReward == 0, "Здоровье/опыт Громилы.");
        Pass("На середине забега появился Громила (HP " + brute.Health.maxHealth + ", размер ×" + MiniBoss.Scale + ") и полоса «" + MiniBoss.Title + "».");

        // 2. Атаки: удар по земле и рывок с предупреждением.
        brute.transform.position = hero.transform.position + hero.transform.forward * 6f;
        bool sawWarning = false;
        int hpBefore = health.CurrentHP;
        by = EditorApplication.timeSinceStartup + 16;
        bool captured = false;
        while (brute != null && brute.Attacks < 2 && EditorApplication.timeSinceStartup < by)
        {
            if (brute.Telegraphing)
            {
                sawWarning = true;
                if (!captured) { yield return WaitReal(0.4); CaptureUi("mini-boss"); captured = true; }
            }
            yield return null;
        }
        Require(brute != null && brute.Attacks >= 2 && sawWarning, "Громила не провёл две атаки с предупреждением: " + (brute != null ? brute.Attacks : -1));
        Pass("Громила провёл удар по земле и рывок с предупреждением; HP героя " + hpBefore + " → " + health.CurrentHP + "; кадр Logs/mini-boss.png.");

        // 3. Награда: сундук и кристаллы, полоса скрыта, второй не появится.
        int crystalsBefore = UnityEngine.Object.FindObjectsOfType<ExperiencePickup>().Length;
        brute.Health.TakeDamage(int.MaxValue);
        yield return WaitReal(0.5);
        Require(UnityEngine.Object.FindObjectOfType<SmallChest>() != null, "После Громилы нет сундука.");
        int crystals = UnityEngine.Object.FindObjectsOfType<ExperiencePickup>().Length - crystalsBefore;
        Require(crystals >= 10, "После Громилы мало кристаллов: " + crystals);
        Require(!bar.activeInHierarchy, "Полоса Громилы не скрылась.");
        yield return WaitReal(1.5);
        Require(MiniBoss.Active == null, "Появился второй Громила.");
        Pass("Победа над Громилой: сундук и " + crystals + " кристаллов, полоса скрыта, второй не появился.");
    }

    private static void CaptureUi(string name)
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        var switched = new List<Canvas>();
        foreach (Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            switched.Add(canvas);
        }
        var texture = new RenderTexture(1280, 720, 24);
        RenderTexture previous = camera.targetTexture;
        camera.targetTexture = texture;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = texture;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        image.Apply();
        File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = previous;
        foreach (Canvas canvas in switched) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
        UnityEngine.Object.Destroy(image);
        texture.Release();
    }
}
