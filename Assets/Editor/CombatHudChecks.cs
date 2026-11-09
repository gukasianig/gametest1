using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Short check of the combat screen: hero panel (HP numbers, armor, dash), event banner, upgrade cards and the
// upgrade toast; saves three frames with the UI to Logs/combat-hud-*.png.
// Batch: -batchmode -executeMethod CombatHudChecks.RunBatch (without -quit). Report: Logs/combat-hud-results.txt.
[InitializeOnLoad]
public static class CombatHudChecks
{
    private const string Request = "Logs/combat-hud.request";
    private const string Report = "Logs/combat-hud-results.txt";
    private static CheckRunner routine;
    private static readonly List<string> lines = new List<string>();
    private static double started;

    static CombatHudChecks()
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
        started = EditorApplication.timeSinceStartup;
        routine = new CheckRunner(Checks());
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 180) throw new Exception("Check timed out.");
            if (!routine.MoveNext()) Finish(true, "Combat HUD checks complete.");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Pass(string text) { lines.Add("PASS: " + text); File.WriteAllLines(Report, lines); }
    private static void Require(bool condition, string text) { if (!condition) throw new Exception(text); }

    private static void Finish(bool success, string text)
    {
        EditorApplication.update -= Tick;
        lines.Add((success ? "PASS: " : "FAIL: ") + text);
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
        GameObject player = PlayerProgression.Instance.gameObject;

        // 0. Main menu: Blender background, title and the button column; weapon screen gets the dimmed background.
        yield return WaitReal(1.6); // menu intro (MenuIntro) takes ~1.2 s
        Require(GameObject.Find("Menu Art") != null, "Main menu background missing.");
        foreach (string name in new[] { "Старт", "Мастерская", "Испытания", "Настройки", "Тест-арена" })
            Require(GameObject.Find(name) != null, "Main menu button missing: " + name);
        Capture("menu-home");
        StartMenu menu = player.GetComponent<StartMenu>();
        menu.ShowWeapons();
        yield return WaitReal(0.3);
        Require(GameObject.Find("Dim") != null && GameObject.Find("Dim").GetComponent<Image>().enabled, "Background not dimmed on the weapon screen.");
        Capture("menu-weapons");
        Pass("Main menu: background, title and 5 buttons; weapon screen dims the background; frames Logs/menu-home.png, menu-weapons.png.");

        player.GetComponent<StartMenu>().SendMessage("BeginGame", 1);
        player.GetComponent<AutoShoter>().enabled = false;
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        PlayerProgression progression = PlayerProgression.Instance;
        yield return WaitReal(0.5);

        // 1. Hero panel: numbers, damage, armor.
        CombatHud hud = player.GetComponent<CombatHud>();
        Require(hud != null && GameObject.Find("Combat HUD") != null, "Combat HUD missing.");
        Require(hud.HealthText == health.CurrentHP + " / " + health.maxHP, "HP text: " + hud.HealthText);
        Require(hud.ArmorText == "Брони нет", "Armor text without armor: " + hud.ArmorText);
        Require(!health.hpImage.transform.parent.gameObject.activeInHierarchy, "Old HP bar still visible.");
        CombatStats.Armor = 0.2f;
        int before = health.CurrentHP;
        health.TakeDamage(10);
        yield return WaitReal(0.3);
        Require(before - health.CurrentHP == 8, "Armor 20% should turn 10 into 8: " + (before - health.CurrentHP));
        Require(hud.ArmorText == "Броня 20%" && hud.HealthText.StartsWith(health.CurrentHP + " / "), "Panel after hit: " + hud.HealthText + " | " + hud.ArmorText);
        Pass("Hero panel: '" + hud.HealthText + "', '" + hud.ArmorText + "', armor turned 10 damage into 8, old bar hidden.");

        // 2. Event banner.
        ArenaEvents events = ArenaEvents.Instance;
        events.Begin(ArenaEventKind.MeteorShower);
        yield return WaitReal(0.6);
        Require(events.BannerVisible, "Event banner not shown.");
        Capture("combat-hud-event");
        yield return WaitReal(3.3);
        Require(!events.BannerVisible, "Event banner did not leave.");
        events.End(false);
        Pass("Event banner slides in for the meteor shower and leaves after ~3 s; frame Logs/combat-hud-event.png.");

        // 3. Upgrade cards: clear before/after line and rank line.
        progression.AddExperience(progression.settings.ExperienceForLevel(progression.Level) - progression.Experience);
        double wait = EditorApplication.timeSinceStartup + 5;
        while (!progression.Choosing && EditorApplication.timeSinceStartup < wait) yield return null;
        Require(progression.Choosing, "Level-up cards did not open.");
        yield return WaitReal(0.6);
        GameObject levelUp = GameObject.Find("Level Up");
        Require(levelUp != null, "Level Up screen missing.");
        int checkedCards = 0;
        foreach (Button card in levelUp.GetComponentsInChildren<Button>())
        {
            Text text = card.GetComponentInChildren<Text>();
            if (text == null || !text.text.Contains("<size=27>")) continue;
            Require((text.text.Contains("→") || text.text.Contains("ПРОКЛЯТИЕ")) && (text.text.Contains("новое") || text.text.Contains("ранг")) &&
                !text.text.Contains(">×"),
                "Card text unclear: " + text.text);
            checkedCards++;
        }
        Require(checkedCards >= 1, "No upgrade cards found.");
        Capture("combat-hud-cards");
        Pass(checkedCards + " cards: title, labelled before → after, rank line; frame Logs/combat-hud-cards.png.");

        // 4. Toast after the choice.
        progression.SelectOfferedUpgrade(0);
        yield return WaitReal(0.5);
        GameObject toast = GameObject.Find("Upgrade Feedback");
        Require(toast != null && toast.activeInHierarchy, "Upgrade toast not shown.");
        string toastText = "";
        foreach (Text label in toast.GetComponentsInChildren<Text>()) toastText += label.text + " | ";
        Require(toastText.Contains("УЛУЧШЕНИЕ") && toastText.Contains("→") && toastText.Contains("ранг"), "Toast text: " + toastText);
        Capture("combat-hud-toast");
        Pass("Upgrade toast: " + toastText.Replace("<b>", "").Replace("</b>", "") + " frame Logs/combat-hud-toast.png.");
    }

    // Overlay canvases are not in camera renders: switch them to the main camera for one frame and save a PNG.
    private static void Capture(string name)
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        RenderTexture texture = new RenderTexture(1280, 720, 24);
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
        Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
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
