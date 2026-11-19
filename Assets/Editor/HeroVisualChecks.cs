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
public static class HeroVisualChecks
{
    private const string Request = "Logs/hero-visual.request";
    private const string Report = "Logs/hero-visual-results.txt";
    private static CheckRunner runner;
    private static double started;
    private static int errors;
    private static byte[] languageBackup;
    private static bool hadLanguageSettings;
    private static readonly List<string> results = new List<string>();
    private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    static HeroVisualChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Request)) EditorApplication.delayCall += Begin;
        };
    }

    public static void RunBatch()
    {
        Directory.CreateDirectory("Logs");
        OperatorFbxImport.ImportAndCheck();
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
            if (EditorApplication.timeSinceStartup - started > 180) throw new Exception("Hero visual checks timed out");
            if (!runner.MoveNext()) Finish(errors == 0, "Hero visual checks complete; runtime errors=" + errors);
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
        MetaProgress.Data.currency=100000;
        var shooter=Object.FindObjectOfType<AutoShoter>();
        var menu=shooter.GetComponent<StartMenu>();
        menu.ShowWeapons();
        typeof(StartMenu).GetMethod("BeginGame",PrivateInstance).Invoke(menu,new object[]{0});
        yield return Wait(.4f);
        var player=shooter.GetComponent<PlayerHealth>();
        var hero=shooter.GetComponent<HeroLocomotion>();
        var movement=shooter.GetComponent<PlayMovement>();
        var animation=shooter.GetComponentInChildren<Animation>();
        var skin=animation.GetComponentInChildren<SkinnedMeshRenderer>();
        Require(AssetDatabase.GetAssetPath(skin.sharedMesh)==OperatorFbxImport.Path,"Scene retained another hero mesh");
        Require(skin.sharedMesh.triangles.Length/3==7882 && skin.bones.Length==16,"Updated geometry/rig missing");
        foreach(var name in new[]{"Idle","Run","RunBack","StrafeLeft","StrafeRight","TurnLeft","TurnRight","Dash","Throw","Hit","Death"})
            Require(animation.GetClip(name)!=null,"Scene missing clip "+name);
        Require(AssetDatabase.AssetPathToGUID(OperatorFbxImport.Path)=="f49d6e0e4fd2cc64bbcfac8133012fe7","FBX GUID changed");
        Pass("Scene uses updated 7882-triangle/16-bone model and all 11 clips; original GUID preserved");
        var playPose=typeof(HeroLocomotion).GetMethod("PlayPose",PrivateInstance);
        playPose.Invoke(hero,new object[]{"Run",.12f});
        animation["Run"].normalizedTime=.35f;
        playPose.Invoke(hero,new object[]{"StrafeLeft",.12f});
        Require(Mathf.Abs(animation["StrafeLeft"].normalizedTime-.35f)<.001f,"Direction transition restarted the gait cycle");
        Pass("Direction transitions retain gait phase");
        Object.FindObjectOfType<WaveManager>().StopSpawning();
        shooter.SetGrenadeUnlocked(false);
        foreach(var enemy in Object.FindObjectsOfType<EnemyAI>())Object.Destroy(enemy.gameObject);
        yield return null;
        Vector3 center=shooter.transform.position;
        var target=Dummy(center+Vector3.forward*12);
        shooter.range=30;
        yield return Wait(.8f);
        foreach(var entry in new[]{new Vector3(0,0,1),new Vector3(0,0,-1),new Vector3(1,0,0),new Vector3(-1,0,0)})
        {
            movement.CheckInput=Vector2.zero;
            shooter.GetComponent<Rigidbody>().position=center;
            target.transform.position=center+Vector3.forward*12;
            yield return Wait(.5f);
            Vector3 cameraForward=Vector3.ProjectOnPlane(movement.cameraTransform.forward,Vector3.up).normalized;
            Vector3 cameraRight=Vector3.ProjectOnPlane(movement.cameraTransform.right,Vector3.up).normalized;
            movement.CheckInput=new Vector2(Vector3.Dot(entry,cameraRight),Vector3.Dot(entry,cameraForward));
            yield return Wait(.7f);
            string expected=entry.z>.5f?"Run":entry.z<-.5f?"RunBack":entry.x>0?"StrafeRight":"StrafeLeft";
            Require(hero.CurrentPose==expected,"Direction "+entry+" selected "+hero.CurrentPose+" expected "+expected);
            Capture("hero-visual-"+expected.ToLowerInvariant(),shooter.transform,true);
            Pass("Runtime direction "+entry+" -> "+hero.CurrentPose);
        }
        movement.CheckInput=Vector2.zero;
        yield return Wait(.6f);
        var seen=new HashSet<string>();
        foreach(float side in new[]{-1f,1f})
        {
            target.transform.position=shooter.transform.position+Vector3.right*(10*side);
            float until=Time.time+1;
            while(Time.time<until){seen.Add(hero.CurrentPose);yield return null;}
        }
        Require(seen.Contains("TurnLeft")&&seen.Contains("TurnRight"),"Turn-in-place poses missing: "+string.Join(",",seen));
        Pass("Stationary target changes play both turn clips");
        bool released=false;
        Require(hero.TryBeginThrow(position=>released=true),"Throw unavailable after reimport");
        yield return Wait(.9f);
        Require(released&&hero.OffhandAvailable,"Grenade release/return regressed");
        Vector3 dashStart=shooter.transform.position;
        movement.CheckInput=Vector2.up;
        movement.RequestDash();
        yield return Wait(.5f);
        Require(Vector3.Distance(dashStart,shooter.transform.position)>2&&!movement.Dashing,"Dash travel/return regressed");
        movement.CheckInput=Vector2.zero;
        yield return Wait(.5f);
        WeaponElements.Buy(0,WeaponElement.Dual);WeaponElements.Equip(0,WeaponElement.Dual);
        shooter.SelectWeapon(0);shooter.range=30;
        var target2=Dummy(shooter.transform.position+Vector3.left*8);
        yield return Wait(.7f);
        Require(ModelSkins.FindOffhandMuzzle(shooter.transform)!=null&&shooter.DualAiming,"Dual rig/socket regressed");
        Require(AimError(ModelSkins.FindMuzzle(shooter.transform),shooter.RightAimTarget)<9,"Dual muzzle mismatch");
        Capture("hero-visual-dual",shooter.transform,true);
        shooter.SelectWeapon(1);shooter.range=30;
        yield return Wait(.5f);
        Capture("hero-visual-rifle",shooter.transform,true);
        Pass("Updated rig retains grenade, dash, dual aiming and rifle support");
        typeof(PlayerHealth).GetField("invulnerableUntil",PrivateInstance).SetValue(player,0f);
        player.TakeDamage(200000,HeroDamageCause.Contact);
        double end=EditorApplication.timeSinceStartup+.25;
        while(EditorApplication.timeSinceStartup<end)yield return null;
        Require(player.IsDead&&(float)typeof(HeroLocomotion).GetField("deathTime",PrivateInstance).GetValue(hero)>0,"Death animation regressed");
        Pass("Actual death samples updated skin");
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
