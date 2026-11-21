using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 19.11.2026: витрина боссов (batch, без -quit). По очереди Страж, Королева роя, Грозовой снайпер и мини-босс Громила:
// крупный кадр в движении, крупный кадр во время подготовки атаки, кадр игровой камеры с полосой HP.
// Считает атаки, повороты к герою, высоту над землёй и ошибки. Лист Logs/boss-showcase.png, отчёт Logs/boss-showcase.txt.
// Запуск: -executeMethod BossShowcaseChecks.RunBatch
[InitializeOnLoad]
public static class BossShowcaseChecks
{
    const string Marker = "Logs/boss-showcase.request", Report = "Logs/boss-showcase.txt";
    const int W = 480, H = 360;
    static double started, mark;
    static int step, errors, failures, bossIndex, telegraphFrames;
    static readonly StringBuilder log = new StringBuilder();
    static readonly List<Texture2D> frames = new List<Texture2D>();
    static GameObject hero;
    static GameObject boss;
    static readonly HashSet<string> attacks = new HashSet<string>();
    static float maxFacingError, maxHeightError, minFacingError = 999f;
    static bool tookTelegraph, enragedShot, hurt;
    static int attackStart;
    static readonly HashSet<string> kinds = new HashSet<string>();
    static string lastSeen = "";

    static BossShowcaseChecks()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode && File.Exists(Marker))
            {
                File.Delete(Marker); started = EditorApplication.timeSinceStartup; step = 0;
                Application.logMessageReceived += (m, st, type) => { if (type == LogType.Exception || type == LogType.Error) { errors++; log.AppendLine("ERROR " + m + "\n" + st); } };
                EditorApplication.update += Tick;
            }
        };
    }

    public static void RunBatch()
    {
        File.WriteAllText(Marker, "1");
        EditorSceneManager.OpenScene("Assets/Scenes/GreenArenaPlay.unity");
        EditorApplication.isPlaying = true;
    }

    static void Check(bool ok, string what) { if (!ok) failures++; log.AppendLine((ok ? "PASS " : "FAIL ") + what); }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        if (frames.Count > 0)
        {
            int columns = 3, rows = (frames.Count + columns - 1) / columns;
            var sheet = new Texture2D(W * columns, H * rows, TextureFormat.RGB24, false);
            for (int i = 0; i < frames.Count; i++) sheet.SetPixels((i % columns) * W, (rows - 1 - i / columns) * H, W, H, frames[i].GetPixels());
            sheet.Apply();
            File.WriteAllBytes("Logs/boss-showcase.png", sheet.EncodeToPNG());
        }
        bool ok = failures == 0 && errors == 0;
        log.AppendLine("failures=" + failures + " runtime errors=" + errors);
        File.WriteAllText(Report, (ok ? "OK" : "FAIL") + "\n" + log);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static void Shot(Camera camera)
    {
        var target = new RenderTexture(W, H, 24);
        var previous = camera.targetTexture;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(W, H, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        image.Apply();
        RenderTexture.active = null;
        camera.targetTexture = previous;
        target.Release();
        frames.Add(image);
    }

    // Крупный кадр босса отдельной камерой (три четверти).
    static void CloseUp()
    {
        Vector3 focus = boss.transform.position;
        Bounds b = Bounds(boss);
        float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
        focus = b.center;
        var go = new GameObject("Showcase camera");
        var camera = go.AddComponent<Camera>();
        camera.fieldOfView = 40f;
        Vector3 toHero = hero.transform.position - boss.transform.position;
        toHero.y = 0f;
        Vector3 side = Quaternion.Euler(0f, 35f, 0f) * (toHero.sqrMagnitude > 0.01f ? toHero.normalized : Vector3.forward);
        camera.transform.position = focus + side * size * 1.5f + Vector3.up * size * 0.8f;
        camera.transform.LookAt(focus);
        Shot(camera);
        Object.Destroy(go);
    }

    // Кадр игровой камеры вместе с интерфейсом (полоса HP босса).
    static void GameView()
    {
        Camera camera = Camera.main;
        var canvases = new List<Canvas>();
        foreach (var canvas in Object.FindObjectsOfType<Canvas>())
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvases.Add(canvas);
        }
        Canvas.ForceUpdateCanvases();
        Shot(camera);
        foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
    }

    static Bounds Bounds(GameObject go)
    {
        Renderer[] all = go.GetComponentsInChildren<Renderer>();
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        foreach (Renderer r in all) if (r.enabled && !(r is LineRenderer)) b.Encapsulate(r.bounds);
        return b;
    }

    static bool Telegraphing()
    {
        if (boss == null) return false;
        BossController c = boss.GetComponent<BossController>();
        if (c != null) return c.Telegraphing;
        MiniBoss m = boss.GetComponent<MiniBoss>();
        return m != null && m.Telegraphing;
    }

    static int AttackCount()
    {
        BossController c = boss != null ? boss.GetComponent<BossController>() : null;
        if (c != null) return c.AttackIndex;
        MiniBoss m = boss != null ? boss.GetComponent<MiniBoss>() : null;
        return m != null ? m.Attacks : 0;
    }

    static string Name(int index) { return index == 0 ? "Guardian" : index == 1 ? "SwarmQueen" : index == 2 ? "StormSniper" : "MiniBoss"; }

    static void Spawn(int index)
    {
        Vector3 at = hero.transform.position + hero.transform.forward * 13f + hero.transform.right * 3f;
        if (index < 3)
        {
            // Через GameSession, как в забеге: появляется и полоса HP босса.
            const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            GameSession session = GameSession.Instance;
            typeof(GameSession).GetField("<NextBoss>k__BackingField", any).SetValue(session, (BossKind)index);
            typeof(GameSession).GetMethod("SpawnBoss", any).Invoke(session, null);
            boss = session.Boss != null ? session.Boss.gameObject : BossController.Create(at, hero.transform, (BossKind)index).gameObject;
            boss.transform.position = new Vector3(at.x, boss.transform.position.y, at.z);
        }
        else
        {
            WaveManager waves = Object.FindObjectOfType<WaveManager>();
            boss = waves != null ? waves.SpawnMiniBoss() : null;
            if (boss != null) { boss.transform.position = new Vector3(at.x, boss.transform.position.y, at.z); }
        }
        attacks.Clear();
        kinds.Clear();
        lastSeen = "";
        enragedShot = hurt = false;
        maxFacingError = maxHeightError = 0f;
        minFacingError = 999f;
        tookTelegraph = false;
        telegraphFrames = 0;
        attackStart = boss != null ? AttackCount() : 0;
    }

    static void Measure()
    {
        if (boss == null) return;
        Vector3 toHero = hero.transform.position - boss.transform.position;
        toHero.y = 0f;
        Vector3 forward = boss.transform.forward;
        Transform skin = boss.transform.Find("SkinModel");
        forward.y = 0f;
        if (toHero.sqrMagnitude > 1f && forward.sqrMagnitude > 0.01f)
        {
            float error = Vector3.Angle(forward, toHero);
            minFacingError = Mathf.Min(minFacingError, error);
            maxFacingError = Mathf.Max(maxFacingError, error);
        }
        Bounds b = Bounds(boss);
        float ground = GroundHeight.At(boss.transform.position);
        maxHeightError = Mathf.Max(maxHeightError, Mathf.Abs(b.min.y - ground));
        if (Telegraphing()) telegraphFrames++;
        string attack = LastAttackName();
        if (attack != lastSeen && attack.Length > 0) { kinds.Add(attack); lastSeen = attack; }
    }

    static string LastAttackName()
    {
        BossController c = boss != null ? boss.GetComponent<BossController>() : null;
        if (c != null) return c.LastAttack;
        MiniBoss m = boss != null ? boss.GetComponent<MiniBoss>() : null;
        return m != null ? m.LastAttack : "";
    }

    static bool BossEnraged()
    {
        BossController c = boss != null ? boss.GetComponent<BossController>() : null;
        if (c != null) return c.Enraged;
        MiniBoss m = boss != null ? boss.GetComponent<MiniBoss>() : null;
        return m != null && m.Enraged;
    }

    static void Tick()
    {
        double t = EditorApplication.timeSinceStartup - started;
        if (t > 200) { log.AppendLine("timeout at step " + step); failures++; Finish(); return; }
        if (hero == null) hero = GameObject.Find("Capsule");
        if (hero == null) return;
        var health = hero.GetComponent<PlayerHealth>();
        if (step >= 1 && health != null && !health.IsDead) { health.GrantInvulnerability(1f); health.Heal(10000); }
        if (GameSession.Instance != null && GameSession.Instance.State == GameRunState.Upgrade)
        {
            GameObject levelUp = GameObject.Find("Level Up");
            if (levelUp != null)
                foreach (UnityEngine.UI.Button card in levelUp.GetComponentsInChildren<UnityEngine.UI.Button>())
                    if (card.interactable) { card.onClick.Invoke(); break; }
        }
        Measure();
        double since = t - mark;
        switch (step)
        {
            case 0:
                if (t < 1.5) return;
                MetaProgress.UseMemory();
                hero.GetComponent<StartMenu>().SendMessage("BeginGame", (object)0);
                step = 1; mark = t;
                return;
            case 1:
                if (since < 2.5) return;
                foreach (WaveManager waves in Object.FindObjectsOfType<WaveManager>()) waves.enabled = false;
                foreach (EnemyAI e in new List<EnemyAI>(EnemyAI.All)) if (e != null) Object.Destroy(e.gameObject);
                hero.GetComponent<AutoShoter>().enabled = false;
                bossIndex = 0;
                step = 2; mark = t;
                return;
            case 2:
                if (since < 0.3) return;
                Spawn(bossIndex);
                if (boss == null) { Check(false, Name(bossIndex) + " not spawned"); Finish(); return; }
                step = 3; mark = t;
                return;
            case 3:
                if (since < 1.6) return;
                CloseUp();   // в движении/ожидании
                step = 4; mark = t;
                return;
            case 4:
                if (!tookTelegraph && Telegraphing() && since > 0.3) { CloseUp(); tookTelegraph = true; }
                // На середине — ниже половины HP: вторая фаза и «ЯРОСТЬ» на полосе.
                if (!hurt && since > 8) { Health h = boss.GetComponent<Health>(); h.TakeDamage(Mathf.RoundToInt(h.maxHealth * 0.55f)); hurt = true; }
                if (hurt && !enragedShot && BossEnraged() && since > 10) { GameView(); enragedShot = true; }
                if (since < 18) return;
                if (!tookTelegraph) CloseUp();
                if (!enragedShot) GameView();
                Check(AttackCount() - attackStart >= 3, Name(bossIndex) + ": attacks " + (AttackCount() - attackStart) + " in " + since.ToString("0") + " s");
                Check(kinds.Count >= 3, Name(bossIndex) + ": different attacks " + kinds.Count + " (" + string.Join(", ", kinds) + ")");
                Check(BossEnraged(), Name(bossIndex) + ": second phase below 50% HP");
                Check(minFacingError < 30f, Name(bossIndex) + ": faces the hero (best " + minFacingError.ToString("0") + "°)");
                Transform bar = GameSession.Instance != null ? GameSession.Instance.transform.root : null;
                BossHealthBar hud = Object.FindObjectOfType<BossHealthBar>();
                Check(hud != null && hud.Visible, Name(bossIndex) + ": boss HP bar visible");
                log.AppendLine("  " + Name(bossIndex) + ": facing error min " + minFacingError.ToString("0") + "°, max " + maxFacingError.ToString("0") +
                    "°; model bottom vs ground max " + maxHeightError.ToString("0.00") + " m; telegraph frames " + telegraphFrames +
                    "; size " + Bounds(boss).size.ToString("0.0"));
                Object.Destroy(boss);
                foreach (EnemyAI e in new List<EnemyAI>(EnemyAI.All)) if (e != null) Object.Destroy(e.gameObject);
                bossIndex++;
                if (bossIndex > 3) { Finish(); return; }
                step = 2; mark = t;
                return;
        }
    }
}
