using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Замер производительности толпы: ~180 врагов вокруг героя в GreenArenaPlay, затем по очереди отключаются
// подозреваемые системы и сравнивается время кадра. Камера рендерится вручную каждый кадр (в batch окна нет).
// Batch: -batchmode -projectPath . -executeMethod PerfProbe.RunBatch (без -quit). Отчёт Logs/perf-probe.txt.
[InitializeOnLoad]
public static class PerfProbe
{
    private const string Request = "Logs/perf-probe.request";
    private const string Report = "Logs/perf-probe.txt";
    private static CheckRunner routine;
    private static readonly StringBuilder log = new StringBuilder();
    private static RenderTexture target;
    private static double started;
    private static double renderMs; private static int renderFrames;
    private static double nextMove; private static int moveSide;
    // Герой перепрыгивает между двумя точками раз в 1,5 с — толпа всё время бежит (ИИ движения под нагрузкой).
    private static void Shuffle()
    {
        // Окно улучшения ставит игру на паузу (timeScale 0) — тогда враги не двигаются и замер врёт.
        if (PlayerProgression.Instance != null && Time.timeScale <= 0f) { PlayerProgression.Instance.CloseChoices(); Time.timeScale = 1f; }
        if (PlayerProgression.Instance == null || EditorApplication.timeSinceStartup < nextMove) return;
        nextMove = EditorApplication.timeSinceStartup + 1.5;
        moveSide = 1 - moveSide;
        Transform hero = PlayerProgression.Instance.transform;
        Vector3 p = hero.position + Vector3.right * (moveSide == 0 ? -14f : 14f);
        p.y = GroundHeight.At(p) + (hero.position.y - GroundHeight.At(hero.position));
        hero.position = p;
    }

    static PerfProbe()
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
        log.Clear();
        started = EditorApplication.timeSinceStartup;
        routine = new CheckRunner(Run());
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 300) throw new Exception("Perf probe timed out");
            if (!routine.MoveNext()) Finish("done");
        }
        catch (Exception error) { Finish("FAIL " + error); }
    }

    private static void Finish(string text)
    {
        EditorApplication.update -= Tick;
        log.AppendLine(text);
        File.WriteAllText(Report, log.ToString());
        EditorApplication.Exit(0);
    }

    private static IEnumerator Wait(float seconds)
    {
        double until = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < until) { RenderFrame(); yield return null; }
    }

    private static void RenderFrame()
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        if (target == null) target = new RenderTexture(1920, 1080, 24);
        RenderTexture previous = camera.targetTexture;
        camera.targetTexture = target;
        var rsw = System.Diagnostics.Stopwatch.StartNew();
        camera.Render();
        renderMs += rsw.Elapsed.TotalMilliseconds; renderFrames++;
        Shuffle();
        camera.targetTexture = previous;
    }

    static readonly string[] Markers =
    {
        "Update.ScriptRunBehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate", "PostLateUpdate.PlayerUpdateCanvases",
        "FixedUpdate.PhysicsFixedUpdate", "PreLateUpdate.DirectorUpdateAnimationEnd", "PostLateUpdate.UpdateAllSkinnedMeshes",
        "Camera.Render", "Canvas.SendWillRenderCanvases", "Canvas.BuildBatch", "EnemyAI.Path", "EnemyAI.Crowd", "EnemyAI.Resolve",
    };

    // Средние миллисекунды за окно: кадр целиком (по часам редактора) и выбранные маркеры профилировщика.
    private static IEnumerator Measure(string label, float seconds)
    {
        var recorders = new List<ProfilerRecorder>();
        foreach (string marker in Markers) recorders.Add(ProfilerRecorder.StartNew(marker.StartsWith("EnemyAI") ? ProfilerCategory.Scripts : ProfilerCategory.Internal, marker, 1));
        var totals = new double[Markers.Length];
        int frames = 0;
        double begin = EditorApplication.timeSinceStartup;
        int startFrame = Time.frameCount;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        renderMs = 0; renderFrames = 0;
        while (EditorApplication.timeSinceStartup - begin < seconds)
        {
            RenderFrame();
            yield return null;
            if (Time.frameCount == startFrame) continue;
            for (int i = 0; i < recorders.Count; i++) totals[i] += recorders[i].LastValue / 1e6;
            frames++;
        }
        sw.Stop();
        int played = Mathf.Max(1, Time.frameCount - startFrame);
        log.Append(label.PadRight(34)).Append(" кадр ").Append((sw.Elapsed.TotalMilliseconds / played).ToString("0.0")).Append(" мс");
        log.Append("  (отрисовка ").Append((renderMs / Mathf.Max(1, renderFrames)).ToString("0.0")).Append(" мс)  врагов ").Append(EnemyAI.ActiveCount).AppendLine();
        for (int i = 0; i < Markers.Length; i++)
            if (frames > 0 && totals[i] / frames > 0.05) log.Append("      ").Append(Markers[i].PadRight(46)).Append((totals[i] / frames).ToString("0.00")).AppendLine(" мс");
        foreach (ProfilerRecorder recorder in recorders) recorder.Dispose();
        File.WriteAllText(Report, log.ToString());
    }

    private static IEnumerator Run()
    {
        yield return null;
        MetaProgress.UseMemory();
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        PlayerProgression.Instance.GetComponent<StartMenu>().SendMessage("BeginGame", (object)1);
        yield return Wait(0.5f);
        WaveManager waves = UnityEngine.Object.FindObjectOfType<WaveManager>();
        waves.StopSpawning();
        Transform hero = PlayerProgression.Instance.transform;
        hero.GetComponent<PlayerHealth>().GrantInvulnerability(1000f);
        AutoShoter shooter = hero.GetComponent<AutoShoter>();
        shooter.enabled = false;
        int count = 0;
        for (int round = 0; round < 6 && count < 180; round++)
            for (int t = 0; t < waves.enemies.Length && count < 180; t++)
            {
                if (waves.enemies[t] == null || waves.enemies[t].prefab == null) continue;
                count += waves.SpawnTest(t, 3, hero.position);
            }
        log.AppendLine("Создано врагов: " + count + ", типов " + waves.enemies.Length);
        yield return Wait(2f);

        yield return Measure("Всё включено (без стрельбы)", 4f);
        shooter.enabled = true;
        yield return Measure("Со стрельбой", 4f);
        shooter.enabled = false;

        EnemyHealthDisplay hud = UnityEngine.Object.FindObjectOfType<EnemyHealthDisplay>();
        if (hud != null) hud.gameObject.SetActive(false);
        yield return Measure("Без полосок HP", 4f);
        if (hud != null) hud.gameObject.SetActive(true);

        var animators = new List<Behaviour>();
        foreach (ModelWalkAnimator a in UnityEngine.Object.FindObjectsOfType<ModelWalkAnimator>()) { a.enabled = false; animators.Add(a); }
        yield return Measure("Без анимации врагов (" + animators.Count + ")", 4f);
        foreach (Behaviour a in animators) if (a != null) a.enabled = true;

        foreach (EnemyAI ai in EnemyAI.All) ai.MovementLocked = true;
        yield return Measure("Враги стоят (без ИИ движения)", 4f);
        foreach (EnemyAI ai in new List<EnemyAI>(EnemyAI.All)) ai.MovementLocked = false;

        var abilities = new List<Behaviour>();
        foreach (EnemyAbility a in UnityEngine.Object.FindObjectsOfType<EnemyAbility>()) { a.enabled = false; abilities.Add(a); }
        yield return Measure("Без способностей врагов", 4f);
        foreach (Behaviour a in abilities) if (a != null) a.enabled = true;

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null) terrain.drawTreesAndFoliage = false;
        yield return Measure("Без травы/деревьев террейна", 4f);
        if (terrain != null) terrain.drawTreesAndFoliage = true;

        var urp = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        log.AppendLine("Качество: " + QualitySettings.names[QualitySettings.GetQualityLevel()] + ", тени " + urp.shadowDistance + " м, каскадов " + urp.shadowCascadeCount + ", MSAA " + urp.msaaSampleCount);
        float distance = urp.shadowDistance;
        int cascades = urp.shadowCascadeCount;
        urp.shadowDistance = 45f;
        urp.shadowCascadeCount = 2;
        yield return Measure("Тени 45 м, 2 каскада", 4f);
        urp.shadowDistance = 0f;
        yield return Measure("Без теней совсем", 4f);
        urp.shadowDistance = distance;
        urp.shadowCascadeCount = cascades;

        var hidden = new List<Renderer>();
        foreach (EnemyAI ai in EnemyAI.All)
            foreach (Renderer r in ai.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hidden.Add(r); }
        yield return Measure("Без моделей врагов", 4f);
        foreach (Renderer r in hidden) if (r != null) r.enabled = true;

        // Сколько рендереров у толпы (каждая часть модели — отдельный объект и вызов отрисовки).
        int renderers = 0, skinned = 0;
        foreach (EnemyAI ai in EnemyAI.All)
        {
            renderers += ai.GetComponentsInChildren<MeshRenderer>().Length;
            skinned += ai.GetComponentsInChildren<SkinnedMeshRenderer>().Length;
        }
        log.AppendLine("Рендереров у врагов: обычных " + renderers + ", скиннутых " + skinned);
    }
}
