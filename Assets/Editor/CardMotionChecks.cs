using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 19.11.2026: анимации карточек, уведомления, предметов и экрана итогов (batch, без -quit).
// Делает лист кадров Logs/card-motion-sheet.png (раздача 0,05/0,2/0,6 с, уход при выборе, уведомление, предметы, итоги)
// и отчёт Logs/card-motion-check.txt. Запуск: -executeMethod CardMotionChecks.RunBatch
[InitializeOnLoad]
public static class CardMotionChecks
{
    const string Marker = "Logs/card-motion.request", Report = "Logs/card-motion-check.txt";
    const int W = 640, H = 360, Columns = 3;
    static double started, mark;
    static int step, errors, failures;
    static readonly StringBuilder log = new StringBuilder();
    static readonly List<Texture2D> frames = new List<Texture2D>();
    static readonly List<string> captions = new List<string>();
    static GameObject hero;
    static PlayerProgression progression;

    static CardMotionChecks()
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

    static void Capture(string caption)
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
        var target = new RenderTexture(W * 2, H * 2, 24);
        var previous = camera.targetTexture;
        var image = new Texture2D(W * 2, H * 2, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, W * 2, H * 2), 0, 0);
            image.Apply();
        }
        finally
        {
            RenderTexture.active = null;
            camera.targetTexture = previous;
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
            target.Release();
        }
        frames.Add(image);
        captions.Add(caption);
        log.AppendLine("frame " + frames.Count + ": " + caption);
    }

    static void Sheet()
    {
        int rows = (frames.Count + Columns - 1) / Columns;
        var sheet = new Texture2D(W * Columns, H * rows, TextureFormat.RGB24, false);
        for (int i = 0; i < frames.Count; i++)
        {
            int col = i % Columns, row = rows - 1 - i / Columns;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    sheet.SetPixel(col * W + x, row * H + y, frames[i].GetPixel(x * 2, y * 2));
        }
        sheet.Apply();
        File.WriteAllBytes("Logs/card-motion-sheet.png", sheet.EncodeToPNG());
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        if (frames.Count > 0) Sheet();
        bool ok = failures == 0 && errors == 0;
        log.AppendLine("failures=" + failures + " runtime errors=" + errors);
        File.WriteAllText(Report, (ok ? "OK" : "FAIL") + "\n" + log);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    // Кадр в заданный момент анимации: время карточек выставляется напрямую (в batch время кадров неравномерно).
    static void Pose(System.Func<UpgradeCardFx, int, float> ageOf)
    {
        var fx = (UpgradeCardFx[])typeof(PlayerProgression).GetField("cardFx", Any).GetValue(progression);
        for (int i = 0; i < fx.Length; i++)
        {
            if (fx[i] == null || !fx[i].isActiveAndEnabled) continue;
            typeof(UpgradeCardFx).GetField("age", Any).SetValue(fx[i], ageOf(fx[i], i));
            typeof(UpgradeCardFx).GetMethod("Update", Any).Invoke(fx[i], null);
        }
    }

    // Окно с обычной, особой и редкой карточкой.
    static void MixOffers()
    {
        var offered = (List<UpgradeDefinition>)typeof(PlayerProgression).GetField("offered", Any).GetValue(progression);
        var catalog = (UpgradeCatalog)typeof(PlayerProgression).GetField("catalog", Any).GetValue(progression);
        UpgradeDefinition common = null, rare = null, special = null;
        foreach (UpgradeDefinition d in catalog.entries)
        {
            if (d.id == "damage") common = d;
            if (d.id == "burn_rounds") rare = d;
            if (d.id == "last_chance") special = d;
        }
        offered.Clear();
        offered.Add(common); offered.Add(special); offered.Add(rare);
        typeof(PlayerProgression).GetMethod("RefreshCards", Any).Invoke(progression, null);
    }

    static Transform Find(string name)
    {
        foreach (Transform t in Object.FindObjectsOfType<Transform>()) if (t.name == name) return t;
        return null;
    }

    static void Tick()
    {
        double t = EditorApplication.timeSinceStartup - started;
        if (t > 90) { log.AppendLine("timeout at step " + step); failures++; Finish(); return; }
        if (hero == null) hero = GameObject.Find("Capsule");
        if (hero == null) return;
        var health = hero.GetComponent<PlayerHealth>();
        if (step >= 1 && health != null && !health.IsDead) { health.GrantInvulnerability(1f); health.Heal(1000); }
        double since = t - mark;
        switch (step)
        {
            case 0:
                if (t < 1.5) return;
                MetaProgress.UseMemory();
                hero.GetComponent<StartMenu>().SendMessage("BeginGame", (object)0);
                progression = hero.GetComponent<PlayerProgression>();
                step = 1; mark = t;
                return;
            case 1:
                if (since < 2.5) return;
                // Уровень → окно из трёх карточек; ставим одну редкую и одну особую, чтобы увидеть эффекты.
                progression.AddExperience(progression.ExperienceRequired);
                step = 2; mark = t;
                return;
            case 2:
                if (since < 0.05) return;
                Check(progression.Choosing, "level-up window opened");
                MixOffers();
                Pose((fx, i) => 0.08f - i * UpgradeCardFx.DealStep);
                Capture("appear 0.08 s");
                Pose((fx, i) => 0.16f - i * UpgradeCardFx.DealStep);
                Capture("appear 0.16 s");
                step = 4; return;
            case 4:
                if (since < 0.9) return;
                Pose((fx, i) => 1.2f);
                Capture("cards settled: common / special / rare");
                step = 5; return;
            case 5:
                if (since < 2.2) return;
                // Блик на середине пути для редкой (период 2,7 с) и особой (1,9 с) карточек.
                Pose((fx, i) =>
                {
                    float phase = i * 1.7f;
                    bool fancy = i == 1;
                    float period = fancy ? 1.9f : 2.7f;
                    float a = 0.35f + 0.375f - phase * 0.2f;
                    while (a < 0.4f) a += period;
                    return a;
                });
                Capture("rare sheen / special sparks + pulse");
                progression.SelectOfferedUpgrade(0);
                Check(Find("Button (chosen)") != null || CountGhosts() > 0, "chosen card ghost created (" + CountGhosts() + " ghosts)");
                step = 6; mark = t; return;
            case 6:
                if (since < 0.15) return;
                Capture("choice: flash + others fall 0.15 s");
                step = 7; return;
            case 7:
                if (since < 0.35) return;
                Capture("choice: card flies to toast 0.35 s");
                step = 8; return;
            case 8:
                if (since < 0.8) return;
                Capture("toast: icon pop + shine 0.8 s");
                Check(CountGhosts() == 0, "ghosts removed after animation");
                if (progression.Choosing) { progression.SelectOfferedUpgrade(0); }
                step = 9; mark = t; return;
            case 9:
                if (since < 0.6 || progression.Choosing) { if (progression.Choosing) progression.SelectOfferedUpgrade(0); return; }
                Vector3 p = hero.transform.position;
                DropPickup.Spawn(p + new Vector3(3f, 0f, 1f), PickupKind.Health);
                DropPickup.Spawn(p + new Vector3(-3f, 0f, 1f), PickupKind.Magnet);
                for (int i = 0; i < 6; i++) ExperiencePickup.Spawn(p + new Vector3(Mathf.Cos(i) * 2.5f, 0f, 3f + Mathf.Sin(i)), 1, merge: false);
                SmallChest.Spawn(p + new Vector3(0f, 0f, 5f));
                step = 10; mark = t; return;
            case 10:
                if (since < 0.15) return;
                Capture("items pop-in 0.15 s");
                step = 11; return;
            case 11:
                if (since < 1.2) return;
                Capture("items settled 1.2 s");
                GameSession.Instance.Defeat(health);
                step = 12; mark = t; return;
            case 12:
                if (since < 0.35) return;
                Capture("results reveal 0.35 s");
                step = 13; return;
            case 13:
                if (since < 2.0) return;
                Capture("results settled 2 s");
                Check(Object.FindObjectsOfType<UiReveal>().Length == 0, "reveal components finished");
                Finish();
                return;
        }
    }

    static int CountGhosts() { return Object.FindObjectsOfType<UpgradeCardGhost>().Length; }
}
