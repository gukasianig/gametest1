using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HeroCombatChecks
{
    private const string Request = "Logs/hero-combat.request";
    private const string DefaultReport = "Logs/hero-combat-results.txt";
    private const string FbxReport = "Logs/operator-fbx-combat.txt";
    private const string SampleScene = "Assets/Scenes/SampleScene.unity";
    private const string GreenScene = "Assets/Scenes/GreenArenaPlay.unity";
    private static string report = DefaultReport;
    private static string frames = "Logs/hero-operator";
    private static CheckRunner runner;
    private static double deadline;
    private static int errors;

    static HeroCombatChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Request))
                EditorApplication.delayCall += Begin;
        };
    }

    public static void RunBatch()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(DefaultReport, "Hero combat check\n");
        HeroModelBuilder.Build(false);
        EditorSceneManager.OpenScene(SampleScene);
        File.WriteAllText(Request, "batch");
        EditorApplication.isPlaying = true;
    }

    public static void RunFbxBatch()
    {
        Require(Application.isBatchMode, "The FBX combat check requires batch mode");
        Directory.CreateDirectory("Logs");
        File.WriteAllText(FbxReport, "Operator FBX combat check\n");
        OperatorFbxImport.ImportAndCheck();
        OperatorFbxImport.InstallInScene(EditorSceneManager.OpenScene(SampleScene));
        File.WriteAllText(Request, "fbx");
        EditorApplication.isPlaying = true;
    }

    public static void InstallFbxAndCheckBatch()
    {
        Require(Application.isBatchMode, "Scene installation requires batch mode");
        Directory.CreateDirectory("Logs");
        File.WriteAllText(FbxReport, "Saved Operator FBX scene check\n");
        OperatorFbxImport.ImportAndCheck();
        foreach (string path in new[] { SampleScene, GreenScene })
        {
            Scene scene = EditorSceneManager.OpenScene(path);
            if (OperatorFbxImport.InstallInScene(scene))
                Require(EditorSceneManager.SaveScene(scene), "Could not save " + path);
        }
        EditorSceneManager.OpenScene(SampleScene);
        File.WriteAllText(Request, "installed");
        EditorApplication.isPlaying = true;
    }

    private static void Begin()
    {
        string mode = File.ReadAllText(Request);
        bool fbx = mode == "fbx" || mode == "installed";
        report = fbx ? FbxReport : DefaultReport;
        frames = fbx ? "Logs/operator-fbx" : "Logs/hero-operator";
        File.Delete(Request);
        errors = 0;
        Application.logMessageReceived += CountErrors;
        deadline = EditorApplication.timeSinceStartup + 45;
        runner = new CheckRunner(CheckCombat(mode == "installed"));
        EditorApplication.update += Tick;
    }

    private static void CountErrors(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            return;
        errors++;
        File.AppendAllText(report, message + "\n");
    }

    private static void Tick()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup < deadline, "Play Mode timed out");
            if (!runner.MoveNext())
            {
                Require(errors == 0, "Unity logged errors during Play Mode");
                Finish(true, "HERO COMBAT PASS");
            }
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Finish(bool success, string result)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= CountErrors;
        File.AppendAllText(report, result + "\n");
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static IEnumerator Until(Func<bool> condition, string message, float seconds = 3f)
    {
        float timeout = Time.realtimeSinceStartup + seconds;
        while (!condition())
        {
            Require(Time.realtimeSinceStartup < timeout, message);
            yield return null;
        }
    }

    private static IEnumerator CheckCombat(bool checkSavedScenes)
    {
        yield return Until(() => PlayerProgression.Instance != null, "Player unavailable");
        MetaProgress.UseMemory();
        MetaProgress.DevUnlockAllWeapons = true;
        GameObject player = PlayerProgression.Instance.gameObject;
        player.GetComponent<StartMenu>().SendMessage("BeginGame", (object)0);
        Require(GameSession.Instance.State == GameRunState.Playing, "Run did not start");
        foreach (WaveManager waves in Object.FindObjectsOfType<WaveManager>()) waves.StopSpawning();
        Animation animation = player.GetComponentInChildren<Animation>();
        if (checkSavedScenes) RequireSavedFbx(animation);
        var movement = player.GetComponent<PlayMovement>();
        var shooter = player.GetComponent<AutoShoter>();
        var health = player.GetComponent<PlayerHealth>();
        Transform hips = animation.transform.Find("Hips") ?? animation.transform.Find("OperatorRig/Hips");
        Transform chest = hips.Find("Spine/Chest");
        Transform hand = chest.Find("UpperArmL/ForearmL/HandL/LeftHandGrip");
        Transform socket = chest.Find("UpperArmR/ForearmR/HandR/WeaponSocket");

        int weaponCount = 0;
        for (int i = 0; i < shooter.weapons.Length; i++)
        {
            if (shooter.weapons[i] == null || !shooter.weapons[i].IsUsable) continue;
            Require(shooter.SelectWeapon(i), "Weapon selection failed " + i);
            yield return null;
            Transform model = socket.Find("SkinModel");
            Require(model != null && model.Find("Muzzle") != null && model.GetComponentInChildren<Renderer>() != null,
                "Weapon model or muzzle missing from right hand " + i);
            weaponCount++;
        }
        File.AppendAllText(report, $"PASS: {weaponCount} weapon presets in right hand\n");
        Require(shooter.SelectWeapon(0), "Could not select starting weapon");

        movement.CheckInput = Vector2.right;
        yield return Until(() => animation.IsPlaying("Run"), "Moving did not play Run");
        Vector3 beforeDash = player.transform.position;
        movement.RequestDash();
        yield return Until(() => movement.Dashing && animation.IsPlaying("Dash"), "Dash did not play Dash");
        yield return Until(() => !movement.Dashing, "Dash did not finish");
        Require(Vector3.Distance(player.transform.position, beforeDash) > 1f && health.Invulnerable, "Dash did not move or grant invulnerability");
        movement.CheckInput = Vector2.zero;
        yield return Until(() => animation.IsPlaying("Idle") && !health.Invulnerable, "No return to Idle after dash");
        File.AppendAllText(report, "PASS: movement, dedicated dash, travel, invulnerability, return to idle\n");
        CaptureHero(animation.transform);

        var enemy = new GameObject("Hero check target") { tag = "Enemy" };
        enemy.transform.position = player.transform.position + player.transform.forward * 7f;
        Transform rightForearm = socket.parent.parent;
        Quaternion resting = rightForearm.localRotation;
        yield return Until(() => Quaternion.Angle(resting, rightForearm.localRotation) > 4f, "No recoil on fire");
        Require(Object.FindObjectOfType<Bullet>() != null, "No bullet fired");
        Capture(Camera.main, frames + "-play.png");

        shooter.grenadeCooldown = .6f;
        shooter.SetGrenadeUnlocked(true);
        yield return Until(() => animation.IsPlaying("Throw"), "Grenade did not start its throw pose");
        Require(Object.FindObjectOfType<Grenade>() == null, "Grenade spawned before release");
        float phase = animation["Throw"].normalizedTime;
        Time.timeScale = 0f;
        float resume = Time.realtimeSinceStartup + .25f;
        yield return Until(() => Time.realtimeSinceStartup >= resume, "Pause time did not elapse");
        Require(Mathf.Abs(animation["Throw"].normalizedTime - phase) < .02f && Object.FindObjectOfType<Grenade>() == null,
            "Paused windup progressed or released a grenade");
        Time.timeScale = 1f;
        yield return Until(() => Object.FindObjectOfType<Grenade>() != null, "Grenade did not release after resume");
        Grenade grenade = Object.FindObjectOfType<Grenade>();
        Require(Vector3.Distance(grenade.transform.position, hand.position) < 1f, "Grenade did not emerge from left hand");
        Require(grenade.damage == shooter.CurrentGrenadeDamage && Mathf.Approximately(grenade.explosionRadius, shooter.CurrentGrenadeRadius),
            "Grenade stats were not applied");
        Require(Physics.GetIgnoreCollision(grenade.GetComponent<Collider>(), player.GetComponent<Collider>()), "Grenade collides with thrower");
        yield return Until(() => grenade.GetComponent<Rigidbody>().velocity.y > 0f, "Grenade did not fly upward");
        shooter.SetGrenadeUnlocked(false);
        yield return Until(() => grenade == null, "Grenade did not explode after its fuse");
        File.AppendAllText(report, "PASS: firing/recoil, paused windup, left-hand release, trajectory, upgrade stats and fuse\n");

        health.TakeDamage(1);
        yield return Until(() => animation.IsPlaying("Hit"), "Nonlethal damage did not play Hit");
        health.TakeDamage(10000);
        yield return Until(() => animation.transform.InverseTransformPoint(hips.position).y < .43f, "Death animation did not play after defeat");
        Require(health.IsDead && GameSession.Instance.State == GameRunState.Defeat && Time.timeScale == 0f, "Death did not finish the run");
        File.AppendAllText(report, "PASS: hit reaction and death at paused game time\n");
        if (checkSavedScenes) yield return CheckGreenArena();
    }

    private static void RequireSavedFbx(Animation animation)
    {
        var skin = animation != null ? animation.GetComponentInChildren<SkinnedMeshRenderer>() : null;
        Require(skin != null && AssetDatabase.GetAssetPath(skin.sharedMesh) == OperatorFbxImport.Path,
            "Saved player visual must reference the new FBX");
        Require(animation.clip != null && animation.clip.name == "Idle" && animation.playAutomatically &&
            animation.cullingType == AnimationCullingType.AlwaysAnimate, "Saved Animation settings are wrong");
    }

    private static IEnumerator CheckGreenArena()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("GreenArenaPlay");
        yield return Until(() => SceneManager.GetActiveScene().path == GreenScene && PlayerProgression.Instance != null &&
            PlayerProgression.Instance.GetComponent<StartMenu>().WaitingForWeapon, "Green arena did not open", 8f);
        GameObject player = PlayerProgression.Instance.gameObject;
        var animation = player.GetComponentInChildren<Animation>();
        RequireSavedFbx(animation);
        player.GetComponent<StartMenu>().SendMessage("BeginGame", (object)0);
        Require(GameSession.Instance.State == GameRunState.Playing, "Green arena run did not start");
        Transform weapon = animation.transform.Find("OperatorRig/Hips/Spine/Chest/UpperArmR/ForearmR/HandR/WeaponSocket");
        yield return Until(() => animation.IsPlaying("Idle") && weapon.Find("SkinModel") != null, "No Idle or held weapon in GreenArenaPlay");
        var skin = animation.GetComponentInChildren<SkinnedMeshRenderer>();
        var baked = new Mesh();
        skin.BakeMesh(baked);
        float minY = float.MaxValue;
        foreach (Vector3 vertex in baked.vertices)
            minY = Mathf.Min(minY, skin.transform.TransformPoint(vertex).y);
        Object.Destroy(baked);
        float ground = GroundHeight.At(player.transform.position);
        Require(Mathf.Abs(minY - ground) < .08f, $"Hero soles are {minY - ground:F3} m from green arena ground");
        File.AppendAllText(report, $"PASS: saved GreenArenaPlay uses FBX, Idle, held weapon, sole gap {minY - ground:F3} m\n");
    }

    private static void Capture(Camera camera, string path)
    {
        var target = new RenderTexture(1280, 720, 24);
        RenderTexture old = RenderTexture.active;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = old;
        Object.Destroy(image);
        Object.Destroy(target);
    }

    private static void CaptureHero(Transform hero)
    {
        var camera = new GameObject("Hero check camera").AddComponent<Camera>();
        camera.CopyFrom(Camera.main);
        camera.orthographic = true;
        camera.orthographicSize = 1.27f;
        Vector3 center = hero.position + Vector3.up;
        camera.transform.position = center + hero.TransformDirection(new Vector3(2.3f, 1.8f, 3.8f));
        camera.transform.LookAt(center);
        Capture(camera, frames + "-close.png");
        Object.Destroy(camera.gameObject);
    }
}
