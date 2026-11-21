using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Случайный финальный босс (12.11.2026): выбор без повторов подряд, Королева роя (рой, кислотный залп, ярость)
// и Грозовой снайпер (молнии, прицельный луч, телепорт при сближении). Кадры Logs/boss-queen.png, boss-sniper.png.
// Пакетный запуск: -batchmode -executeMethod BossVariantChecks.RunBatch (без -quit). Отчёт: Logs/boss-variants-results.txt.
[InitializeOnLoad]
public static class BossVariantChecks
{
    private const string Request = "Logs/boss-variants.request";
    private const string Report = "Logs/boss-variants-results.txt";
    private static CheckRunner routine;
    private static readonly List<string> lines = new List<string>();
    private static double started;

    static BossVariantChecks()
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
            if (EditorApplication.timeSinceStartup - started > 120) throw new Exception("Проверка не уложилась во время.");
            if (!routine.MoveNext()) Finish(true, "Проверки финальных боссов пройдены.");
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

    private static IEnumerator Spawn(GameSession session, BossKind kind)
    {
        typeof(GameSession).GetProperty("NextBoss").GetSetMethod(true).Invoke(session, new object[] { kind });
        session.SendMessage("SpawnBoss");
        yield return WaitReal(0.4);
        Require(session.Boss != null && session.Boss.Kind == kind, "Босс " + kind + " не появился.");
        Require(session.Boss.transform.Find("SkinModel") != null, kind + ": нет модели.");
        Require(session.Boss.GetComponentInChildren<ModelWalkAnimator>() != null, kind + ": нет анимации ног.");
        GameObject bar = GameObject.Find("Boss HP");
        Require(bar != null && bar.activeInHierarchy && bar.GetComponentInChildren<UnityEngine.UI.Text>().text == session.Boss.Title,
            kind + ": полоса не показывает имя.");
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
        PlayerHealth health = hero.GetComponent<PlayerHealth>();
        health.IncreaseMaxHealth(20000);
        GameSession session = GameSession.Instance;

        // 0. Случайный выбор: все три встречаются, подряд один и тот же не выпадает.
        var seen = new HashSet<BossKind>();
        BossKind previous = BossController.RandomKind();
        for (int i = 0; i < 60; i++)
        {
            BossKind next = BossController.RandomKind();
            Require(next != previous, "Один и тот же босс выпал два раза подряд: " + next);
            seen.Add(next);
            previous = next;
        }
        Require(seen.Count == 3, "Встретились не все боссы: " + seen.Count);
        Pass("Случайный выбор: за 60 раз встретились все 3 босса, подряд одинаковых нет.");

        // 1. Королева роя.
        yield return Spawn(session, BossKind.SwarmQueen);
        BossController queen = session.Boss;
        queen.transform.position = hero.transform.position + hero.transform.forward * 9f + Vector3.up * (queen.transform.position.y - hero.transform.position.y);
        double by = EditorApplication.timeSinceStartup + 40;
        int minions = 0;
        bool acid = false, captured = false;
        // 19.11.2026: атаки идут в случайном порядке — ждём и рой, и любую кислотную атаку (залп, спираль, лужи).
        while ((queen.AttackIndex < 3 || minions < 6 || !acid) && EditorApplication.timeSinceStartup < by)
        {
            minions = Math.Max(minions, Array.FindAll(UnityEngine.Object.FindObjectsOfType<EnemyAI>(), a => a.name == "Swarm minion").Length);
            if (!acid && (UnityEngine.Object.FindObjectsOfType<EnemyProjectile>().Length > 0 || UnityEngine.Object.FindObjectsOfType<BossHazard>().Length > 0)) acid = true;
            if (!captured && minions > 0) { Capture("boss-queen"); captured = true; }
            yield return null;
        }
        Require(minions >= 6 && acid, "Королева: рой " + minions + ", кислота " + acid);
        queen.Health.SetHealth(queen.Health.maxHealth);
        queen.Health.TakeDamage(Mathf.RoundToInt(queen.Health.maxHealth * 0.6f));
        int index = queen.AttackIndex;
        by = EditorApplication.timeSinceStartup + 12;
        while (!queen.Enraged && EditorApplication.timeSinceStartup < by) yield return null;
        Require(queen.Enraged, "Королева не разозлилась ниже половины HP.");
        Pass("Королева роя: модель с анимацией ног, полоса «" + queen.Title + "», рой из " + minions + " врагов, кислотный залп, ярость ниже 50% HP; кадр Logs/boss-queen.png.");
        UnityEngine.Object.Destroy(queen.gameObject);
        foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>()) UnityEngine.Object.Destroy(enemy.gameObject);
        yield return WaitReal(0.5);

        // 2. Грозовой снайпер.
        yield return Spawn(session, BossKind.StormSniper);
        BossController sniper = session.Boss;
        int hpBefore = health.CurrentHP;
        by = EditorApplication.timeSinceStartup + 25;
        captured = false;
        while (sniper.AttackIndex < 3 && EditorApplication.timeSinceStartup < by)
        {
            if (!captured && sniper.Telegraphing) { yield return WaitReal(0.5); Capture("boss-sniper"); captured = true; }
            yield return null;
        }
        Require(sniper.AttackIndex >= 3, "Снайпер провёл мало атак: " + sniper.AttackIndex);
        Require(health.CurrentHP < hpBefore, "Молнии и луч снайпера не задели героя.");
        // Подходим вплотную — телепорт подальше.
        Vector3 close = sniper.transform.position - (sniper.transform.position - hero.transform.position).normalized * 3f;
        close.y = hero.transform.position.y;
        hero.GetComponent<Rigidbody>().position = close;
        hero.transform.position = close;
        by = EditorApplication.timeSinceStartup + 9;
        float distance = 0f;
        while (EditorApplication.timeSinceStartup < by)
        {
            distance = WeaponTargets.Flat(sniper.transform.position, hero.transform.position);
            if (distance > 10f) break;
            yield return null;
        }
        Require(distance > 10f, "Снайпер не телепортировался от героя: " + distance.ToString("0.0") + " м");
        Pass("Грозовой снайпер: модель с ногами, молнии и луч задели героя (HP " + hpBefore + " → " + health.CurrentHP +
             "), при сближении телепорт на " + distance.ToString("0.0") + " м; кадр Logs/boss-sniper.png.");
    }

    private static void Capture(string name)
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
