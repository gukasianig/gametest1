using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Бесконечный режим (12.11.2026): кнопка закрыта до первой победы; круг — Громила на середине, босс в конце;
// после босса новый круг и враги сильнее; поражение — итоги и рекорды. Кадр Logs/endless-result.png.
// Пакетный запуск: -batchmode -executeMethod EndlessChecks.RunBatch (без -quit). Отчёт: Logs/endless-results.txt.
[InitializeOnLoad]
public static class EndlessChecks
{
    private const string Request = "Logs/endless.request";
    private const string Report = "Logs/endless-results.txt";
    private static CheckRunner routine;
    private static readonly List<string> lines = new List<string>();
    private static double started;

    static EndlessChecks()
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
            if (!routine.MoveNext()) Finish(true, "Проверки бесконечного режима пройдены.");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Pass(string text) { lines.Add("ПРОЙДЕНО: " + text); File.WriteAllLines(Report, lines); }
    private static void Require(bool condition, string text) { if (!condition) throw new Exception(text); }

    private static void Finish(bool success, string text)
    {
        EditorApplication.update -= Tick;
        GameSession.EndlessMode = false;
        lines.Add((success ? "ПРОЙДЕНО: " : "ОШИБКА: ") + text);
        File.WriteAllLines(Report, lines);
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static IEnumerator WaitReal(double seconds)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < end) yield return null;
    }

    private static void SetElapsed(GameSession session, float value)
    {
        typeof(GameSession).GetProperty("Elapsed").GetSetMethod(true).Invoke(session, new object[] { value });
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        GameObject hero = PlayerProgression.Instance.gameObject;
        yield return WaitReal(1.6);

        // 1. Кнопка в меню: до первой победы закрыта, после — включает режим.
        GameObject locked = GameObject.Find("Бесконечный  · после победы");
        Require(locked != null && !MetaProgress.EndlessUnlocked, "Нет закрытой кнопки «Бесконечный».");
        locked.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Require(!GameSession.EndlessMode, "Закрытая кнопка включила бесконечный режим.");
        MetaProgress.Data.victories = 1;
        locked.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Require(GameSession.EndlessMode && CoopMode.Players == 1, "После победы кнопка не включила режим.");
        Pass("Кнопка «Бесконечный»: до первой победы закрыта, после — открывает выбор оружия в бесконечном режиме.");

        hero.GetComponent<StartMenu>().SendMessage("BeginGame", (object)1);
        WaveManager waves = UnityEngine.Object.FindObjectOfType<WaveManager>();
        yield return WaitReal(0.4);
        GameSession session = GameSession.Instance;
        hero.GetComponent<AutoShoter>().enabled = false;
        PlayerHealth health = hero.GetComponent<PlayerHealth>();
        health.IncreaseMaxHealth(50000);
        Require(session.State == GameRunState.Playing && session.survivalSeconds > 100000f && GameSession.EndlessRound == 0, "Бесконечный бой не начался.");

        // 2. Середина круга — Громила; конец — случайный босс; волны не останавливаются.
        SetElapsed(session, GameSession.EndlessCycle * 0.5f);
        double by = EditorApplication.timeSinceStartup + 3;
        while (MiniBoss.Active == null && EditorApplication.timeSinceStartup < by) yield return null;
        Require(MiniBoss.Active != null, "Громила не появился на середине круга.");
        int miniHp = MiniBoss.Active.Health.maxHealth;
        MiniBoss.Active.Health.TakeDamage(int.MaxValue);
        yield return WaitReal(0.3);
        SetElapsed(session, GameSession.EndlessCycle - 9f);
        yield return WaitReal(0.2);
        SetElapsed(session, GameSession.EndlessCycle + 0.5f);
        by = EditorApplication.timeSinceStartup + 4;
        while (session.Boss == null && EditorApplication.timeSinceStartup < by) yield return null;
        Require(session.Boss != null && !session.BossWarning && !waves.Suspended, "Босс круга не появился или волны остановились.");
        string title = session.Boss.Title;
        session.Boss.Health.TakeDamage(int.MaxValue);
        yield return WaitReal(0.5);
        Require(GameSession.EndlessRound == 1 && session.Boss == null && session.State == GameRunState.Playing,
            "После босса круг не сменился: круг " + GameSession.EndlessRound + ", " + session.State);
        Require(UnityEngine.Object.FindObjectOfType<SmallChest>() != null, "После босса круга нет сундука.");
        Pass("Круг 1: Громила на середине (HP " + miniHp + "), в конце «" + title + "», волны идут; после победы — сундук, круг 2, враги ×" +
             GameSession.EndlessFactor.ToString("0.0") + ".");

        // 3. Следующий Громила сильнее.
        SetElapsed(session, session.Elapsed + GameSession.EndlessCycle * 0.5f + 0.5f);
        by = EditorApplication.timeSinceStartup + 3;
        while (MiniBoss.Active == null && EditorApplication.timeSinceStartup < by) yield return null;
        Require(MiniBoss.Active != null && MiniBoss.Active.Health.maxHealth > miniHp, "Громила второго круга не сильнее первого.");
        Pass("Круг 2: Громила сильнее (HP " + miniHp + " → " + MiniBoss.Active.Health.maxHealth + ").");

        // 4. Поражение: итоги и рекорды бесконечного режима.
        int earnedBefore = MetaProgress.Currency;
        // Кристаллы после босса могли дать уровень: закрываем окна карточек (пока они открыты, урон не проходит).
        PlayerProgression progression = PlayerProgression.Instance;
        for (int i = 0; i < 20 && (progression.Choosing || session.State != GameRunState.Playing); i++)
        {
            if (progression.Choosing) progression.SelectOfferedUpgrade(0);
            yield return WaitReal(0.1);
        }
        health.TakeDamage(health.CurrentHP + 1000);   // не int.MaxValue: умножение на множитель урона испытаний переполняется
        yield return WaitReal(0.5);
        Require(session.State == GameRunState.Defeat, "Поражение не завершило забег: " + session.State + ", HP " + health.CurrentHP +
            ", неуязвим " + health.Invulnerable + ", повержен " + health.IsDead + ", окно " + progression.Choosing + ", скорость " + Time.timeScale);
        Require(MetaProgress.Data.bestEndlessRound == 1 && MetaProgress.Data.endlessRuns == 1 && MetaProgress.Data.bestEndlessScore > 0 &&
                MetaProgress.Currency > earnedBefore, "Рекорды/награда бесконечного режима не записаны.");
        CaptureUi("endless-result");
        Pass("Поражение: экран «БЕСКОНЕЧНЫЙ РЕЖИМ», рекорд круга 1, счёт " + MetaProgress.Data.bestEndlessScore + ", детали +" +
             (MetaProgress.Currency - earnedBefore) + "; кадр Logs/endless-result.png.");
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
