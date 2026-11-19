using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HeroAimChecks
{
    private const string Request = "Logs/hero-aim.request";
    private const string Report = "Logs/hero-aim-results.txt";
    private static CheckRunner runner;
    private static double started;
    private static int errors;
    private static byte[] languageBackup;
    private static bool hadLanguageSettings;
    private static readonly List<string> results = new List<string>();
    private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    static HeroAimChecks()
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
        results.Clear();
        started = EditorApplication.timeSinceStartup;
        errors = 0;
        hadLanguageSettings = File.Exists(GameText.SettingsPath);
        languageBackup = hadLanguageSettings ? File.ReadAllBytes(GameText.SettingsPath) : null;
        Application.logMessageReceived += OnLog;
        runner = new CheckRunner(Checks());
        EditorApplication.update += Tick;
    }

    private static void OnLog(string text, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors++;
            results.Add("RUNTIME " + text);
        }
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 180) throw new Exception("Hero aim checks timed out");
            if (!runner.MoveNext()) Finish(errors == 0, "Hero aim checks complete; runtime errors=" + errors);
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    private static void Finish(bool ok, string detail)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        if (hadLanguageSettings) File.WriteAllBytes(GameText.SettingsPath, languageBackup);
        else if (File.Exists(GameText.SettingsPath)) File.Delete(GameText.SettingsPath);
        results.Add((ok ? "PASS " : "FAIL ") + detail);
        File.WriteAllLines(Report, results);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static void Require(bool ok, string detail) { if (!ok) throw new Exception(detail); }
    private static void Pass(string detail) { results.Add("PASS " + detail); File.WriteAllLines(Report, results); }
    private static IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }

    private static Health Dummy(Vector3 position)
    {
        var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = "Aim check target";
        item.tag = "Enemy";
        item.transform.position = position;
        var health = item.AddComponent<Health>();
        health.SetHealth(100000);
        health.experienceReward = 0;
        item.AddComponent<EnemyAI>().MovementLocked = true;
        return health;
    }

    private static float AimError(Transform muzzle, Transform target)
    {
        var fx = muzzle.GetComponentInParent<WeaponFireAnimation>();
        return Vector3.Angle(fx.BarrelForward, target.GetComponent<Collider>().bounds.center - muzzle.position);
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        MetaProgress.Data.currency = 100000;
        WeaponElements.Buy(0, WeaponElement.Dual);
        WeaponElements.Equip(0, WeaponElement.Dual);
        var shooter = Object.FindObjectOfType<AutoShoter>();
        var menu = shooter.GetComponent<StartMenu>();
        menu.ShowWeapons();
        typeof(StartMenu).GetMethod("BeginGame", PrivateInstance).Invoke(menu, new object[] { 0 });
        yield return null;
        var movement = shooter.GetComponent<PlayMovement>();
        var hero = shooter.GetComponent<HeroLocomotion>();
        var player = shooter.GetComponent<PlayerHealth>();
        Object.FindObjectOfType<WaveManager>().StopSpawning();
        foreach (var enemy in Object.FindObjectsOfType<EnemyAI>()) Object.Destroy(enemy.gameObject);
        player.GrantInvulnerability(100);
        shooter.SetGrenadeUnlocked(false);
        shooter.range = 25;
        Require(hero.CanAimWeapons && shooter.DualAiming, "Existing rig is not available for aiming");
        var rightMuzzle = ModelSkins.FindMuzzle(shooter.transform);
        var leftMuzzle = ModelSkins.FindOffhandMuzzle(shooter.transform);
        Require(rightMuzzle != null && leftMuzzle != null, "Missing dual muzzles");
        Vector3 center = shooter.transform.position;
        var first = Dummy(center + new Vector3(7, 0, 8));
        var second = Dummy(center + new Vector3(-7, 0, 8));
        yield return Wait(.8f);
        foreach (var pair in new[] { new Vector3(8,0,-8), new Vector3(-9,0,0), new Vector3(0,0,-10) })
        {
            second.transform.position = center + pair;
            yield return Wait(.75f);
            Require(shooter.RightAimTarget != null && shooter.LeftAimTarget != null && shooter.RightAimTarget != shooter.LeftAimTarget, "Independent targets missing: R=" + shooter.RightAimTarget + " L=" + shooter.LeftAimTarget + " range=" + shooter.range);
            float rightError = AimError(rightMuzzle, shooter.RightAimTarget);
            float leftError = AimError(leftMuzzle, shooter.LeftAimTarget);
            Require(rightError < 7 && leftError < 7, "Aim mismatch right=" + rightError + " left=" + leftError);
            Pass("Targets " + pair + ": muzzle error R=" + rightError + " L=" + leftError);
        }
        // Exactly opposite targets: no zero-vector stance or shoulder turn behind the body.
        first.transform.position = center + Vector3.right * 10;
        second.transform.position = center - Vector3.right * 10;
        yield return Wait(1);
        Require(AimError(rightMuzzle, shooter.RightAimTarget) < 7 && AimError(leftMuzzle, shooter.LeftAimTarget) < 7, "Opposite targets mismatch");
        Capture("hero-aim-opposite", shooter.transform, true);
        Pass("Opposite stance and independent muzzle directions");
        shooter.enabled = false;
        foreach (var bullet in Object.FindObjectsOfType<Bullet>()) Object.Destroy(bullet.gameObject);
        yield return null;
        // Fire the actual volley and compare fresh projectile directions against both visual barrels.
        typeof(AutoShoter).GetMethod("Shoot", PrivateInstance).Invoke(shooter, new object[] { shooter.RightAimTarget });
        var bullets = Object.FindObjectsOfType<Bullet>();
        Require(bullets.Length == 2, "Dual volley count=" + bullets.Length);
        var directions = new[] { rightMuzzle.GetComponentInParent<WeaponFireAnimation>().BarrelForward, leftMuzzle.GetComponentInParent<WeaponFireAnimation>().BarrelForward };
        foreach (var bullet in bullets) Require(Mathf.Min(Vector3.Angle(bullet.transform.forward,directions[0]), Vector3.Angle(bullet.transform.forward,directions[1])) < 2, "Projectile diverged from barrel");
        Pass("Two actual projectiles agree with visual barrels");
        bool released = false;
        Require(hero.TryBeginThrow(position => released = true), "Throw did not start");
        yield return Wait(.12f);
        Require(!hero.OffhandAvailable && !leftMuzzle.gameObject.activeInHierarchy, "Left pistol did not leave throwing hand");
        yield return Wait(1.2f);
        Require(released && hero.OffhandAvailable && leftMuzzle.gameObject.activeInHierarchy, "Throw release or return failed");
        movement.RequestDash();
        yield return Wait(.04f);
        Require(movement.Dashing, "Dash did not start");
        yield return Wait(.3f);
        Require(!movement.Dashing, "Dash did not finish");
        shooter.enabled = true;
        yield return Wait(.4f);
        Capture("hero-aim-dual", shooter.transform, true);
        Pass("Grenade hand release/return and dash resume");
        movement.CheckInput = new Vector2(.6f, .4f);
        yield return Wait(.3f);
        Require(shooter.GetComponent<Rigidbody>().velocity.sqrMagnitude > .1f, "Run movement missing");
        Require(AimError(ModelSkins.FindMuzzle(shooter.transform), shooter.RightAimTarget) < 9, "Running arm lost aim");
        movement.CheckInput = Vector2.zero;
        shooter.enabled = false;
        foreach (var bullet in Object.FindObjectsOfType<Bullet>()) Object.Destroy(bullet.gameObject);
        yield return Wait(.4f);
        hero.Recoil(true);
        Require((float)typeof(HeroLocomotion).GetField("leftRecoilUntil", PrivateInstance).GetValue(hero) > Time.time &&
            (float)typeof(HeroLocomotion).GetField("recoilUntil", PrivateInstance).GetValue(hero) <= Time.time, "Recoil is still shared");
        shooter.Stats.ExtraPellets = 2;
        shooter.Evolve();
        yield return null;
        typeof(AutoShoter).GetMethod("Shoot", PrivateInstance).Invoke(shooter, new object[] { shooter.RightAimTarget });
        Require(Object.FindObjectsOfType<Bullet>().Length == 2 * shooter.CurrentPelletCount && shooter.CurrentPelletCount >= 4, "Evolved extra-pellet volley changed");
        shooter.enabled = true;
        shooter.range = 25;
        yield return Wait(.1f);
        Pass("Running aim, independent recoil, evolution and extra-pellet volley");
        var previous = shooter.RightAimTarget;
        previous.GetComponent<Health>().TakeDamage(200000);
        yield return Wait(.3f);
        Require(shooter.RightAimTarget != previous, "Dead target retained");
        if (first != null) Object.Destroy(first.gameObject);
        if (second != null) Object.Destroy(second.gameObject);
        yield return Wait(.2f);
        Require(shooter.RightAimTarget == null && shooter.LeftAimTarget == null, "Lost targets retained");
        shooter.SelectWeapon(1);
        yield return Wait(.2f);
        Require(!shooter.DualAiming && ModelSkins.FindOffhandMuzzle(shooter.transform) == null, "Weapon change retained second gun");
        Pass("Death/loss of target and switch to single weapon");
        GameSession.Instance.TogglePause();
        Quaternion rotation = shooter.transform.rotation;
        double until = EditorApplication.timeSinceStartup + .15;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Require(Quaternion.Angle(rotation, shooter.transform.rotation) < .01f, "Aim moved during pause");
        GameSession.Instance.TogglePause();
        Pass("Pause freezes aiming");
        player.TakeDamage(200000);
        // Invulnerability deliberately remains active; use the explicit session defeat to check pose teardown.
        GameSession.Instance.Defeat(player);
        yield return null;
        Require(GameSession.Instance.IsFinished, "Defeat did not finish");
        Pass("Result transition at end of run");
    }

    private static void Capture(string name, Transform hero, bool closeup)
    {
        Camera camera = Camera.main;
        Vector3 position = camera.transform.position;
        Quaternion rotation = camera.transform.rotation;
        bool orthographic = camera.orthographic;
        float size = camera.orthographicSize;
        var canvases = new List<Canvas>();
        foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>())
        {
            if (!canvas.enabled || !canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvases.Add(canvas);
            if (closeup) canvas.enabled = false;
        }
        if (closeup)
        {
            camera.transform.position = hero.position + new Vector3(0, 5, -4);
            camera.transform.LookAt(hero.position + Vector3.up);
            camera.orthographic = true;
            camera.orthographicSize = 2f;
        }
        var texture = new RenderTexture(1280,720,24);
        RenderTexture previous = camera.targetTexture, active = RenderTexture.active;
        camera.targetTexture = texture;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = texture;
        var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1280,720),0,0);
        image.Apply();
        File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());
        camera.targetTexture = previous;
        RenderTexture.active = active;
        camera.transform.SetPositionAndRotation(position,rotation);
        camera.orthographic = orthographic;
        camera.orthographicSize = size;
        foreach (Canvas canvas in canvases) { canvas.enabled=true; canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null; }
        Object.Destroy(image);
        Object.Destroy(texture);
    }
}
