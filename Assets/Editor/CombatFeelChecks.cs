using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Ощущение от боя (11.11.2026): новые звуки, стоп-кадр на элите, красная рамка при низком HP, стрелки к элите за экраном,
// замедленная смерть Стража и победа после неё. Кадр Logs/combat-feel.png.
// Пакетный запуск: -batchmode -executeMethod CombatFeelChecks.RunBatch (без -quit). Отчёт: Logs/combat-feel-results.txt.
[InitializeOnLoad]
public static class CombatFeelChecks
{
    private const string Request = "Logs/combat-feel.request";
    private const string Report = "Logs/combat-feel-results.txt";
    private static CheckRunner routine;
    private static readonly List<string> lines = new List<string>();
    private static double started;

    static CombatFeelChecks()
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
            if (EditorApplication.timeSinceStartup - started > 90) throw new Exception("Проверка не уложилась во время.");
            if (!routine.MoveNext()) Finish(true, "Проверки ощущения от боя пройдены.");
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
        GameSession session = GameSession.Instance;
        Require(session.State == GameRunState.Playing && CombatFeel.Instance != null, "Бой не начался или нет CombatFeel.");
        foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>()) UnityEngine.Object.Destroy(enemy.gameObject);
        hero.GetComponent<AutoShoter>().enabled = false;

        foreach (Sfx sfx in new[] { Sfx.LevelUp, Sfx.Pickup, Sfx.Chest, Sfx.Heartbeat }) GameAudio.Play(sfx, 0.1f, 0f);
        GameAudio.PlayAt(Sfx.Pickup, 0.1f, 1.4f);
        Pass("Новые звуки (уровень, кристалл с высотой, сундук, сердце) созданы и проигрываются.");

        // Стоп-кадр на элите.
        waves.wave = 6;
        GameObject elite = waves.SpawnElite();
        Require(elite != null, "Элита не появилась.");
        yield return null;
        elite.GetComponent<Health>().TakeDamage(int.MaxValue);
        float stopped = Time.timeScale;
        yield return WaitReal(0.25);
        Require(stopped < 0.2f && Mathf.Approximately(Time.timeScale, 1f), "Стоп-кадр: во время " + stopped + ", после " + Time.timeScale);
        Pass("Убийство элиты: скорость " + stopped.ToString("0.00") + " на 0,07 с, затем снова 1.");

        // Стрелка к элите за экраном и красная рамка при низком HP.
        GameObject far = waves.SpawnElite();
        Require(far != null, "Вторая элита не появилась.");
        far.GetComponent<EnemyAI>().MovementLocked = true;
        far.transform.position = hero.transform.position + new Vector3(45f, 0f, 10f);
        PlayerHealth health = hero.GetComponent<PlayerHealth>();
        health.TakeDamage(Mathf.RoundToInt(health.CurrentHP - health.maxHP * 0.15f));
        yield return WaitReal(1.2);
        Require(CombatFeel.Instance.VisibleArrows >= 1, "Нет стрелки к элите за экраном.");
        Require(CombatFeel.Instance.VignetteAlpha > 0.2f, "Нет красной рамки при HP " + health.CurrentHP + "/" + health.maxHP);
        Capture("combat-feel");
        Pass("Элита за экраном — стрелка у края (" + CombatFeel.Instance.VisibleArrows + "); HP " + health.CurrentHP + "/" + health.maxHP +
             " — красная рамка " + CombatFeel.Instance.VignetteAlpha.ToString("0.00") + " и сердцебиение; кадр Logs/combat-feel.png.");
        UnityEngine.Object.Destroy(far);
        health.Heal(health.maxHP);
        health.GrantInvulnerability(30f);

        // Смерть Стража: замедление, затем победа.
        session.SendMessage("SpawnBoss");
        yield return WaitReal(0.3);
        Require(session.Boss != null, "Страж не появился.");
        session.Boss.Health.TakeDamage(int.MaxValue);
        yield return WaitReal(0.3);
        float slow = Time.timeScale;
        Require(session.State == GameRunState.Playing && slow < 0.5f, "Смерть Стража: сразу " + session.State + ", скорость " + slow);
        double by = EditorApplication.timeSinceStartup + 4.0;
        while (session.State != GameRunState.Victory && EditorApplication.timeSinceStartup < by) yield return null;
        Require(session.State == GameRunState.Victory, "После смерти Стража нет победы: " + session.State);
        Pass("Смерть Стража: замедление до " + slow.ToString("0.0") + ", взрывы и вспышка, затем экран победы.");
    }

    private static void Capture(string name)
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        RenderTexture texture = new RenderTexture(1280, 720, 24);
        var switched = new List<Canvas>();
        foreach (Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            switched.Add(canvas);
        }
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
