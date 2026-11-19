using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Opt-in combined Play Mode check. No meta save or PlayerPrefs changes; language file restored.
// -batchmode -executeMethod LocalizationChecks.RunBatch (without -quit).
[InitializeOnLoad]
public static class LocalizationChecks
{
    private const string Request = "Logs/localization.request";
    private const string Report = "Logs/localization-results.txt";
    private const string Backup = "Logs/localization-settings-backup.json";
    private const string Missing = "Logs/localization-settings-missing";
    private static CheckRunner routine;
    private static readonly List<string> lines = new List<string>();
    private static double started;
    private static bool namesOnly;

    static LocalizationChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Request)) EditorApplication.delayCall += Begin;
        };
    }

    public static void RunBatch()
    {
        RunBatchFor("full");
    }

    public static void RunNamesBatch()
    {
        RunBatchFor("names");
    }

    private static void RunBatchFor(string scope)
    {
        Directory.CreateDirectory("Logs");
        if (File.Exists(GameText.SettingsPath)) File.Copy(GameText.SettingsPath, Backup, true);
        else File.WriteAllText(Missing, "no settings file before check");
        File.Delete(GameText.SettingsPath);
        File.WriteAllText(Request, scope);
        EditorSceneManager.OpenScene("Assets/Scenes/GreenArenaPlay.unity");
        EditorApplication.isPlaying = true;
    }

    private static void Begin()
    {
        namesOnly = File.ReadAllText(Request) == "names";
        File.Delete(Request);
        lines.Clear();
        started = EditorApplication.timeSinceStartup;
        routine = new CheckRunner(namesOnly ? NameChecks() : Checks());
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 100) throw new Exception("Localization check timed out.");
            if (!routine.MoveNext()) Finish(true, "Localization checks complete.");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Finish(bool success, string message)
    {
        EditorApplication.update -= Tick;
        if (File.Exists(Backup)) { File.Copy(Backup, GameText.SettingsPath, true); File.Delete(Backup); }
        else if (File.Exists(Missing)) File.Delete(GameText.SettingsPath);
        File.Delete(Missing);
        lines.Add((success ? "PASS: " : "FAIL: ") + message);
        File.WriteAllLines(namesOnly ? "Logs/localization-names-results.txt" : Report, lines);
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Pass(string message) { lines.Add("PASS: " + message); File.WriteAllLines(namesOnly ? "Logs/localization-names-results.txt" : Report, lines); }
    private static IEnumerator Wait(double seconds)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < end) yield return null;
    }

    private static Button ButtonNamed(Transform root, string name)
    {
        return root.GetComponentsInChildren<Button>(true).First(b => b.name == name);
    }

    private static void EnglishOnScreen(string screen)
    {
        foreach (Text text in UnityEngine.Object.FindObjectsOfType<Text>())
        {
            if (!text.isActiveAndEnabled || !text.canvas || !text.canvas.enabled) continue;
            Require(!Regex.IsMatch(text.text, "[А-Яа-яЁё]"), screen + ": untranslated text: " + text.text);
        }
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        Require(GameText.Language == GameLanguage.English, "Fresh launch should use English.");
        GameObject hero = PlayerProgression.Instance.gameObject;
        StartMenu menu = hero.GetComponent<StartMenu>();
        yield return Wait(1.5);
        EnglishOnScreen("Main menu");
        Require(GameObject.Find("Старт").GetComponentInChildren<Text>().text == "Start", "Main menu label or stable object name changed.");

        // Every catalog entry, case variant and rich-text fragment must translate without Cyrillic.
        var asset = Resources.Load<TextAsset>("Localization/game-text");
        var catalog = JsonUtility.FromJson<Entries>(asset.text);
        foreach (GameText.Entry entry in catalog.entries)
        {
            Require(GameText.Translate(entry.ru) == entry.en, "Translation mismatch: " + entry.id);
            Require(GameText.Translate(" " + entry.ru) == " " + entry.en, "Fragment overlaps next phrase: " + entry.id);
            Require(!Regex.IsMatch(GameText.Translate(entry.ru.ToUpperInvariant()), "[А-Яа-яЁё]"), "Uppercase missing: " + entry.id);
        }
        Pass(catalog.entries.Length + " entries and uppercase variants; English default and main menu.");

        Transform menuCanvas = GameObject.Find("Start Menu").transform;
        ButtonNamed(menuCanvas, "Настройки").onClick.Invoke();
        Transform settings = GameObject.Find("Settings").transform;
        EnglishOnScreen("Settings EN");
        Require(settings.GetComponentsInChildren<Text>().Any(t => t.text == "Language"), "Language row missing.");
        Capture("localization-settings-en");
        ButtonNamed(settings, "►").onClick.Invoke();
        Require(GameText.Language == GameLanguage.Russian && File.ReadAllText(GameText.SettingsPath).Contains("ru"), "Russian not saved.");
        Require(settings.GetComponentsInChildren<Text>().Any(t => t.text == "Русский"), "Russian native language name missing.");
        Require(settings.GetComponentsInChildren<Text>().Any(t => t.text == "НАСТРОЙКИ"), "Settings header did not refresh.");
        Capture("localization-settings-ru");
        ButtonNamed(settings, "Назад").onClick.Invoke();
        Require(GameObject.Find("Старт").GetComponentInChildren<Text>().text == "Старт", "Hidden menu did not refresh to Russian.");
        ButtonNamed(menuCanvas, "Настройки").onClick.Invoke();
        ButtonNamed(settings, "◄").onClick.Invoke();
        ButtonNamed(settings, "Назад").onClick.Invoke();
        EnglishOnScreen("Main menu after RU → EN");
        Pass("Settings arrows switch both directions while paused, save ru/en, refresh hidden menu; settings screenshots captured.");

        menu.ShowWeapons();
        EnglishOnScreen("Weapon selection");
        var shooter = hero.GetComponent<AutoShoter>();
        foreach (WeaponData weapon in shooter.weapons)
        {
            string source = (weapon.title ?? "") + " " + (weapon.role ?? "") + " " + (weapon.description ?? "");
            string translated = GameText.Translate(source);
            Require(!Regex.IsMatch(translated, "[А-Яа-яЁё]"), "Serialized weapon text missing: " + source + " => " + translated);
        }
        UnityEngine.Object.FindObjectsOfType<Button>().First(b => b.name == "Назад" && b.isActiveAndEnabled).onClick.Invoke();
        menu.ShowWorkshop();
        EnglishOnScreen("Workshop");
        ButtonNamed(menuCanvas, "‹  НАЗАД").onClick.Invoke();
        menu.ShowModifiers();
        EnglishOnScreen("Challenges");
        // Active challenge screen has its own Back button.
        UnityEngine.Object.FindObjectsOfType<Button>().First(b => b.name == "Назад" && b.isActiveAndEnabled).onClick.Invoke();
        menu.ShowStatistics();
        EnglishOnScreen("Statistics");
        UnityEngine.Object.FindObjectsOfType<Button>().First(b => b.name == "Назад" && b.isActiveAndEnabled).onClick.Invoke();
        Pass("Weapon selection (including serialized weapon descriptions), Workshop, Challenges and Statistics in English.");

        menu.BeginTestArena();
        yield return Wait(0.4);
        hero.GetComponent<PlayerHealth>().IncreaseMaxHealth(50000);
        EnglishOnScreen("Combat HUD and test arena");
        GameSession session = GameSession.Instance;
        session.TogglePause();
        Transform pause = GameObject.Find("Pause").transform;
        ButtonNamed(pause, "Настройки").onClick.Invoke();
        Transform pauseSettings = GameObject.Find("Settings").transform;
        Require(pauseSettings.GetComponentsInChildren<Text>().Any(t => t.text == "English"), "Second settings instance has stale language.");
        ButtonNamed(pauseSettings, "►").onClick.Invoke();
        Require(pause.GetComponentsInChildren<Text>().Any(t => t.text == "Продолжить"), "Pause menu did not switch to Russian.");
        ButtonNamed(pauseSettings, "◄").onClick.Invoke();
        ButtonNamed(pauseSettings, "Назад").onClick.Invoke();
        EnglishOnScreen("Pause EN");
        session.TogglePause();
        ArenaEvents.Instance.Begin(ArenaEventKind.GuardChest);
        yield return Wait(0.2);
        EnglishOnScreen("Chest event");
        ArenaEvents.Instance.End(false);
        var progression = hero.GetComponent<PlayerProgression>();
        progression.OfferChestReward();
        yield return Wait(0.2);
        EnglishOnScreen("Upgrade cards");
        progression.CloseChoices();
        Pass("Combat HUD, test arena, pause settings (both languages), chest event and upgrade cards.");

        // Reload preferences from disk without touching the catalog or live UI subscriptions.
        GameText.SetLanguage(GameLanguage.Russian);
        typeof(GameText).GetField("loaded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, false);
        Require(GameText.Language == GameLanguage.Russian, "Saved Russian did not reload.");
        GameText.SetLanguage(GameLanguage.English);
        typeof(GameText).GetField("loaded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, false);
        Require(GameText.Language == GameLanguage.English, "Saved English did not reload.");
        File.WriteAllText(GameText.SettingsPath, "{broken");
        typeof(GameText).GetField("loaded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, false);
        Require(GameText.Language == GameLanguage.English, "Damaged settings should fall back to English.");
        Pass("Language file reload for ru/en and damaged-file fallback; original language file restored on exit.");
    }

    [Serializable] private class Entries { public GameText.Entry[] entries; }

    private static IEnumerator NameChecks()
    {
        yield return null;
        MetaProgress.UseMemory();
        var hero = PlayerProgression.Instance.gameObject;
        var menu = hero.GetComponent<StartMenu>();
        var shooter = hero.GetComponent<AutoShoter>();
        Require((string)typeof(StartMenu).GetMethod("WeaponName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(menu, new object[] { 0 }) == WeaponSelectScreen.Title(shooter.weapons[0], 0), "Menu statistics uses an internal weapon name.");
        menu.BeginTestArena();
        yield return Wait(0.3);
        Transform test = GameObject.Find("Test Arena UI").transform;
        Require(test.GetComponentsInChildren<Text>().Any(t => t.text == "Pistol"), "Test arena weapon name missing.");
        ButtonNamed(test, "Враги").onClick.Invoke();
        GameText.SetLanguage(GameLanguage.Russian);
        Require(test.GetComponentsInChildren<Text>().Any(t => t.text == "Быстрый"), "Russian enemy names missing.");
        ButtonNamed(test, "Оружие").onClick.Invoke();
        Require(test.GetComponentsInChildren<Text>().Any(t => t.text == "Пистолет"), "Russian weapon names missing.");
        RunStatistics stats = hero.GetComponent<RunStatistics>();
        typeof(RunStatistics).GetMethod("Refresh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(stats, null);
        var body = (Text)typeof(RunStatistics).GetField("body", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(stats);
        Require(body.text.Contains("Пистолет") && !body.text.Contains("pistolet"), "Combat statistics uses an internal name.");
        GameText.SetLanguage(GameLanguage.English);
        body.transform.parent.gameObject.SetActive(true);
        Require(body.text.Contains("Pistol"), "Combat statistics did not switch to English.");
        foreach (EnemyType type in UnityEngine.Object.FindObjectOfType<WaveManager>().enemies)
            Require(GameText.Translate(GameText.EnemyName(type.name)) == type.name, "English enemy label mismatch: " + type.name);
        Pass("Weapon names in menu/run statistics and test arena; RU/EN enemy labels; original language file restored.");
    }

    private static void Capture(string name)
    {
        if (File.Exists("Logs/" + name + ".png")) return;
        Camera camera = Camera.main;
        RenderTexture texture = new RenderTexture(1280, 720, 24);
        var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        foreach (Canvas c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1f; }
        RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
        camera.targetTexture = texture;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = texture;
        Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        image.Apply();
        File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = oldActive;
        camera.targetTexture = oldTarget;
        foreach (Canvas c in canvases) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.worldCamera = null; }
        UnityEngine.Object.Destroy(image);
        texture.Release();
        UnityEngine.Object.Destroy(texture);
    }
}
