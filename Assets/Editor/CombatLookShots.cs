using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Кадры «как выглядит бой» с игровой камеры: по одному врагу каждого типа вокруг героя + круги метеоритов.
// Batch: Unity.exe -batchmode -projectPath . -executeMethod CombatLookShots.RunBatch (без -quit).
// Кадры: Logs/combat-look-meteor.png, Logs/combat-look-crowd.png; отчёт Logs/combat-look.txt.
[InitializeOnLoad]
public static class CombatLookShots
{
    private const string Request = "Logs/combat-look.request";
    private const string Report = "Logs/combat-look.txt";
    private static CheckRunner routine;
    private static double started;

    static CombatLookShots()
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
        started = EditorApplication.timeSinceStartup;
        routine = new CheckRunner(Shots());
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 120) throw new Exception("Timed out.");
            if (!routine.MoveNext()) Finish(true, "Frames saved.");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Finish(bool success, string text)
    {
        EditorApplication.update -= Tick;
        File.WriteAllText(Report, (success ? "PASS: " : "FAIL: ") + text);
        if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
        else EditorApplication.isPlaying = false;
    }

    private static IEnumerator WaitReal(double seconds, PlayerHealth hero)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < end)
        {
            if (hero != null) { hero.GrantInvulnerability(1f); hero.Heal(1000); }
            yield return null;
        }
    }

    private static IEnumerator Shots()
    {
        yield return WaitReal(0.5, null);
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) throw new Exception("Player missing.");
        player.GetComponent<StartMenu>().SendMessage("BeginGame", 1);
        player.GetComponent<AutoShoter>().enabled = false;
        PlayerHealth hero = player.GetComponent<PlayerHealth>();
        yield return WaitReal(0.8, hero);

        WaveManager waves = UnityEngine.Object.FindObjectOfType<WaveManager>();
        if (waves != null) waves.enabled = false;   // только наши враги, без случайной волны
        int count = 0;
        Vector3 centre = player.transform.position;
        for (int i = 0; i < waves.enemies.Length; i++)
        {
            if (waves.enemies[i].prefab == null) continue;
            float angle = i * Mathf.PI * 2f / waves.enemies.Length;
            float distance = 4.5f + (i % 2) * 2.5f;
            Vector3 point = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            point.y = GroundHeight.At(point);
            GameObject enemy = UnityEngine.Object.Instantiate(waves.enemies[i].prefab, point, Quaternion.LookRotation(centre - point));
            ModelSkins.ApplyEnemy(enemy, waves.enemies[i].prefab.name);   // как WaveManager: модель D3 вместо заготовки
            count++;
        }
        yield return WaitReal(0.6, hero);

        ArenaEvents events = ArenaEvents.Instance;
        events.Begin(ArenaEventKind.MeteorShower);
        yield return WaitReal(1.4, hero);
        Capture("combat-look-meteor");
        yield return WaitReal(0.6, hero);
        events.End(false);
        yield return WaitReal(2.5, hero);
        Capture("combat-look-crowd");
        File.WriteAllText(Report, "Spawned " + count + " enemy types; camera offset " +
            Camera.main.GetComponent<TopDownCamera>().offset);
    }

    private static void Capture(string name)
    {
        Camera camera = Camera.main;
        const int width = 1600, height = 900;
        RenderTexture texture = new RenderTexture(width, height, 24);
        List<Canvas> switched = new List<Canvas>();
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
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = previous;
        foreach (Canvas canvas in switched)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
        }
        UnityEngine.Object.Destroy(image);
        texture.Release();
    }
}
