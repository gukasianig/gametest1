using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Opt-in combined runtime check: -batchmode -executeMethod UpgradeArtworkChecks.RunBatch (no -quit).
[InitializeOnLoad]
public static class UpgradeArtworkChecks
{
    private const string Request = "Logs/upgrade-artwork.request";
    private static int phase;
    private static double started;
    private static double captureAfter;
    private static UpgradeDefinition toastCandidate;
    private static readonly List<string> results = new List<string>();
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    static UpgradeArtworkChecks()
    {
        EditorApplication.playModeStateChanged += OnState;
    }

    public static void RunBatch()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Request, "batch");
        EditorSceneManager.OpenScene("Assets/Scenes/GreenArenaPlay.unity");
        EditorApplication.isPlaying = true;
    }

    private static void OnState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !File.Exists(Request)) return;
        File.Delete(Request);
        started = EditorApplication.timeSinceStartup;
        phase = 0;
        EditorApplication.update += Tick;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 50) throw new Exception("Artwork check timed out");
            if (EditorApplication.timeSinceStartup - started < 2) return;
            var progression = PlayerProgression.Instance;
            Require(progression != null, "Missing PlayerProgression");
            if (phase == 0)
            {
                MetaProgress.UseMemory();
                progression.GetComponent<StartMenu>().SendMessage("BeginGame", (object)1);
                UnityEngine.Object.FindObjectOfType<WaveManager>().StopSpawning();
                progression.GetComponent<AutoShoter>().enabled = false;
                phase++;
                return;
            }
            if (phase == 1)
            {
                var method = typeof(PlayerProgression).GetMethod("CardSprite", PrivateStatic);
                var textures = new HashSet<Texture>();
                int count = 0;
                foreach (UpgradeEffect effect in Enum.GetValues(typeof(UpgradeEffect)))
                {
                    if (effect == UpgradeEffect.Evolution) continue;
                    var definition = new UpgradeDefinition { effect = effect };
                    var sprite = (Sprite)method.Invoke(null, new object[] { definition });
                    var texture = Resources.Load<Texture2D>("UI/Upgrades/" + effect);
                    Require(sprite != null && sprite.texture == texture, "Wrong art resource: " + effect);
                    Require(texture.width == 256 && texture.height == 256, "Wrong dimensions: " + effect);
                    Require(textures.Add(texture), "Reused texture: " + effect);
                    count++;
                }
                results.Add("PASS: " + count + " unique 256x256 Resources textures resolved through CardSprite.");
                var weaponIcon = UiArtwork.Weapon(1);
                Require(weaponIcon != null, "Missing baseline weapon icon");
                var evolution = new UpgradeDefinition { effect = UpgradeEffect.Evolution, icon = weaponIcon };
                Require((Sprite)method.Invoke(null, new object[] { evolution }) == weaponIcon, "Evolution weapon icon replaced");
                results.Add("PASS: Evolution preserves its explicit weapon icon.");
                progression.TestLevelUp();
                Require(progression.Choosing, "Upgrade choices did not open");
                var offered = (List<UpgradeDefinition>)typeof(PlayerProgression).GetField("offered", PrivateInstance).GetValue(progression);
                toastCandidate = offered[0];
                offered.Clear();
                foreach (var effect in new[] { UpgradeEffect.Armor, UpgradeEffect.DroneCount, UpgradeEffect.CursedGlass })
                {
                    var entry = progression.catalog.entries.Find(item => item != null && item.effect == effect);
                    Require(entry != null, "Missing catalog entry " + effect);
                    offered.Add(entry);
                }
                typeof(PlayerProgression).GetMethod("RefreshCards", PrivateInstance).Invoke(progression, null);
                Canvas.ForceUpdateCanvases();
                var icons = (Image[])typeof(PlayerProgression).GetField("icons", PrivateInstance).GetValue(progression);
                var vectors = (UpgradeIcon[])typeof(PlayerProgression).GetField("vectorIcons", PrivateInstance).GetValue(progression);
                for (int i = 0; i < 3; i++)
                {
                    Require(icons[i].enabled && icons[i].sprite != null && icons[i].preserveAspect, "Card art hidden/stretched " + i);
                    Require(vectors[i].enabled && !vectors[i].ShowGlyph && vectors[i].ShowPlusBadge, "Wrong glyph/badge overlay " + i);
                }
                results.Add("PASS: Armor/Drone/Curse cards display art with separate plus badges and no vector glyph.");
                captureAfter = EditorApplication.timeSinceStartup + 0.4;
                phase++;
                return;
            }
            if (EditorApplication.timeSinceStartup < captureAfter) return;
            Capture();
            results.Add("PASS: Captured 1280x720 upgrade window to Logs/upgrade-artwork-cards.png.");
            var toastOffers = (List<UpgradeDefinition>)typeof(PlayerProgression).GetField("offered", PrivateInstance).GetValue(progression);
            toastOffers[0] = toastCandidate;
            Require(progression.SelectOfferedUpgrade(0), "Could not select an available upgrade");
            var toastIcon = (Image)typeof(PlayerProgression).GetField("toastIcon", PrivateInstance).GetValue(progression);
            var toastVector = (UpgradeIcon)typeof(PlayerProgression).GetField("toastVector", PrivateInstance).GetValue(progression);
            Require(toastIcon.enabled && toastIcon.sprite == UiArtwork.Upgrade(toastCandidate.effect), "Toast did not receive selected art");
            Require(toastVector.enabled && toastVector.ShowPlusBadge && !toastVector.ShowGlyph, "Wrong toast overlay");
            Require(GameSession.Instance.State == GameRunState.Playing && Time.timeScale > 0f, "Gameplay did not resume after selection");
            results.Add("PASS: Selected upgrade " + toastCandidate.effect + ", toast art/badge and gameplay resume.");
            Finish(0);
        }
        catch (Exception exception)
        {
            results.Add("FAIL: " + exception);
            Finish(1);
        }
    }

    private static void Finish(int code)
    {
        EditorApplication.update -= Tick;
        File.WriteAllLines("Logs/upgrade-artwork-results.txt", results);
        EditorApplication.Exit(code);
    }

    private static void Capture()
    {
        Camera camera = Camera.main;
        Require(camera != null, "Missing camera");
        var canvases = new List<Canvas>();
        foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvases.Add(canvas);
        }
        var target = new RenderTexture(1280, 720, 24);
        var previous = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes("Logs/upgrade-artwork-cards.png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previousActive;
            camera.targetTexture = previous;
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
            UnityEngine.Object.Destroy(image);
            target.Release();
            UnityEngine.Object.Destroy(target);
        }
    }
}
