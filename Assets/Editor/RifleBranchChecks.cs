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
public static class RifleBranchChecks
{
    private const string Request = "Logs/rifle-branches.request";
    private const string Report = "Logs/rifle-branches-results.txt";
    private static CheckRunner runner;
    private static double started;
    private static int errors;
    private static byte[] languageBackup;
    private static bool hadLanguageSettings;
    private static readonly List<string> results = new List<string>();
    private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly MethodInfo Shoot = typeof(AutoShoter).GetMethod("Shoot", PrivateInstance);

    static RifleBranchChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Request)) EditorApplication.delayCall += Begin;
        };
    }

    public static void RunBatch()
    {
        Directory.CreateDirectory("Logs");
        RifleBranchAssets.Install();
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
            if (EditorApplication.timeSinceStartup - started > 180) throw new Exception("Rifle checks timed out");
            if (!runner.MoveNext()) Finish(errors == 0, "Rifle branch checks complete; runtime errors=" + errors);
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

    private static Transform Find(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root) { Transform found = Find(child, name); if (found != null) return found; }
        return null;
    }

    private static void Buy(WeaponElement branch, int count)
    {
        for (int i = 0; i < count; i++) Require(WeaponElements.Buy(1, branch), "Cannot buy " + branch);
        Require(WeaponElements.Equip(1, branch), "Cannot equip " + branch);
    }

    private static Health Dummy(Vector3 position)
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = "Rifle check target";
        item.tag = "Enemy";
        item.transform.position = position;
        Health health = item.AddComponent<Health>();
        health.SetHealth(1000000);
        health.experienceReward = 0;
        item.AddComponent<EnemyAI>().MovementLocked = true;
        return health;
    }

    private static void ClearBullets()
    {
        foreach (Bullet bullet in Object.FindObjectsOfType<Bullet>()) Object.Destroy(bullet.gameObject);
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        MetaProgress.Data.currency = 100000;
        foreach (MetaUnlock unlock in MetaProgress.Unlocks) MetaProgress.Unlock(unlock.id);
        Require((int)WeaponElement.Revolver == 5 && (int)WeaponElement.Dual == 6 && (int)WeaponElement.Sticky == 7, "Saved enum values changed");
        Require(!WeaponElements.Valid(0, WeaponElement.Assault) && !WeaponElements.Valid(1, WeaponElement.Revolver) &&
            !WeaponElements.Valid(1, WeaponElement.Fire) && WeaponElements.IsLocked(2), "Branch weapon isolation failed");
        foreach (WeaponElement branch in new[] { WeaponElement.Assault, WeaponElement.Minigun, WeaponElement.Heavy })
        {
            string key = WeaponElements.RifleModelKey(branch);
            ModelSkinSet.Entry entry = ModelSkins.CurrentSet.Find(key);
            Require(entry != null && entry.model != null, "Missing model " + key);
            Require(Resources.Load<Sprite>("UI/Weapons/" + key) != null, "Missing weapon icon " + key);
            Require(Resources.Load<Texture2D>("UI/Tree/" + WeaponElements.Icons[(int)branch]) != null, "Missing tree icon " + key);
            foreach (Renderer renderer in entry.model.GetComponentsInChildren<Renderer>())
                foreach (Material material in renderer.sharedMaterials)
                    Require(material != null && material.shader.name == "Universal Render Pipeline/Lit", "Wrong branch material " + key);
            for (int rank = 1; rank <= 3; rank++) Require(WeaponElements.Description(branch, rank).Length > 20, "Missing rank text");
        }
        Pass("Three FBX models, URP materials, six icon assets, nine descriptions; enum IDs and weapon isolation preserved");

        AutoShoter shooter = PlayerProgression.Instance.GetComponent<AutoShoter>();
        StartMenu menu = shooter.GetComponent<StartMenu>();
        menu.ShowWorkshop();
        WorkshopTree tree = Object.FindObjectOfType<WorkshopTree>();
        Transform group = Find(tree.transform, "Weapon group 1");
        Require(group != null && group.GetComponentsInChildren<Button>().Length == 4, "Rifle must have weapon + three branches");
        foreach (string name in new[] { "Assault", "Minigun", "Heavy" })
            Require(Find(group, "Node weapon:1:" + name) != null, "Missing rifle branch node " + name);
        Require(Find(group, "Node weapon:1:Fire") == null, "Old rifle elements still visible");
        Find(group, "Node weapon:1:Assault").GetComponent<Button>().onClick.Invoke();
        Find(tree.transform, "Tree purchase").GetComponent<Button>().onClick.Invoke();
        Require(WeaponElements.Rank(1, WeaponElement.Assault) == 1, "Workshop purchase failed");
        Text category = (Text)typeof(WorkshopTree).GetField("category", PrivateInstance).GetValue(tree);
        Require(((LocalizedUiText)category).SourceText == GameText.Source("workshop.branch.caption"), "Branch is still labelled as an element");
        GameLanguage originalLanguage = GameText.Language;
        GameText.SetLanguage(GameLanguage.English);
        Capture("rifle-workshop-en", shooter.transform, false);
        GameText.SetLanguage(GameLanguage.Russian);
        Capture("rifle-workshop-ru", shooter.transform, false);
        GameText.SetLanguage(originalLanguage);
        Pass("Workshop shows three rifle branches and buys/equips Assault through UI");
        Buy(WeaponElement.Assault, 2);
        typeof(StartMenu).GetMethod("BeginGame", PrivateInstance).Invoke(menu, new object[] { 1 });
        yield return null;
        yield return null;
        Time.timeScale = 1f;
        Object.FindObjectOfType<WaveManager>().StopSpawning();
        foreach (EnemyAI enemy in Object.FindObjectsOfType<EnemyAI>()) Object.Destroy(enemy.gameObject);
        shooter.enabled = false;
        shooter.grenadePrefab = null;
        shooter.GetComponent<PlayerHealth>().IncreaseMaxHealth(20000);
        shooter.Stats.ExtraPellets = 1;
        shooter.Stats.Set(0f, 0f, 1, 1);
        shooter.SetProgressionMultipliers(1f, 1f);
        yield return null;
        Vector3 point = shooter.transform.position + new Vector3(6, .8f, 0);
        Health target = Dummy(point);
        int firstDamage = 0;
        for (int i = 0; i < 3; i++)
        {
            ClearBullets();
            yield return Wait(.04f);
            Shoot.Invoke(shooter, new object[] { target.transform });
            Bullet[] bullets = Object.FindObjectsOfType<Bullet>();
            Require(bullets.Length == 2, "Assault lost extra projectiles: count=" + bullets.Length + " configured=" + shooter.CurrentPelletCount + " stats=" + shooter.Stats.ExtraPellets);
            foreach (Bullet bullet in bullets)
            {
                Require(bullet.pierce == 1 && bullet.ricochet == 1 && bullet.stats == shooter.Stats, "Assault lost shared upgrades");
                if (i == 0) firstDamage = bullet.damage;
                if (i == 2) Require(bullet.damage > firstDamage * 1.3f, "Final burst shot is not stronger");
            }
        }
        ClearBullets();
        yield return Wait(.04f);
        shooter.SelectWeapon(1);
        shooter.enabled = true;
        var seen = new HashSet<int>();
        var times = new List<float>();
        float end = Time.time + 2.3f;
        while (Time.time < end)
        {
            bool made = false;
            foreach (Bullet bullet in Object.FindObjectsOfType<Bullet>()) if (seen.Add(bullet.GetInstanceID())) made = true;
            if (made) times.Add(Time.time);
            yield return null;
        }
        float smallest = 10f, largest = 0f;
        for (int i = 1; i < times.Count; i++) { float gap = times[i] - times[i-1]; smallest = Mathf.Min(smallest,gap); largest = Mathf.Max(largest,gap); }
        Require(times.Count >= 6 && smallest < .2f && largest > .25f, "Assault timing does not form bursts");
        Require(target.CurrentHealth < 1000000, "Assault bullets never hit target");
        shooter.enabled = false;
        RequireModelFacing(shooter);
        Capture("rifle-assault", shooter.transform, true);
        Pass("Assault: extra bullets and shared upgrades, stronger third shot, live burst gaps " + smallest.ToString("0.000") + "/" + largest.ToString("0.000"));

        Buy(WeaponElement.Minigun, 3);
        shooter.SelectWeapon(1);
        float slow = shooter.CurrentFireInterval;
        yield return Wait(2.3f);
        Require(shooter.MachineSpin > .95f && shooter.CurrentFireInterval < slow * .6f, "Minigun did not spin up");
        shooter.enabled = false;
        RequireModelFacing(shooter);
        Capture("rifle-minigun", shooter.transform, true);
        Health closer = Dummy(shooter.transform.position + new Vector3(3, .8f, 0));
        shooter.enabled = true;
        yield return Wait(.06f);
        Require(shooter.MachineSpin < .7f, "Changing target did not halve spin");
        yield return Wait(1.2f);
        closer.TakeDamage(2000000);
        yield return Wait(.05f);
        Require(shooter.MachineSpin < .7f, "Death-induced target switch did not halve spin");
        Object.Destroy(target.gameObject);
        if (closer != null) Object.Destroy(closer.gameObject);
        yield return Wait(1.2f);
        Require(shooter.MachineSpin < .01f, "Minigun did not cool without targets");
        shooter.enabled = false;
        ClearBullets();
        Pass("Minigun: spin-up, faster steady fire, target-switch penalty and no-target cooling");
        yield return null;

        Buy(WeaponElement.Heavy, 3);
        shooter.SelectWeapon(1);
        shooter.enabled = false;
        target = Dummy(point);
        ClearBullets();
        yield return Wait(.04f);
        Shoot.Invoke(shooter, new object[] { target.transform });
        foreach (Bullet bullet in Object.FindObjectsOfType<Bullet>())
        {
            Require(bullet.pierce == 4 && bullet.ricochet == 1, "Heavy pierce did not stack");
            Require(bullet.damage >= 33 && bullet.speed > shooter.baseWeapon.bulletSpeed, "Heavy bullet damage/speed wrong");
        }
        Require(Mathf.Abs(shooter.CurrentFireInterval - .13f * 2.8f) < .001f, "Heavy interval wrong");
        RequireModelFacing(shooter);
        Capture("rifle-heavy", shooter.transform, true);
        ClearBullets();
        yield return Wait(.04f);
        shooter.Evolve();
        Shoot.Invoke(shooter, new object[] { target.transform });
        foreach (Bullet bullet in Object.FindObjectsOfType<Bullet>()) Require(bullet.pierce == 5, "Evolution replaced Heavy pierce");
        Require(ModelSkins.WeaponKey(shooter.baseWeapon, 1) == "Rifle_Heavy", "Evolution lost branch model");
        Pass("Heavy: damage, slower interval, faster bullets, shared ricochet and pierce 1+3+1 with evolution");
        shooter.SelectWeapon(0);
        shooter.enabled = false;
        Require(ModelSkins.WeaponKey(shooter.baseWeapon, 0) == "Weapon0" && !shooter.Evolved && shooter.MachineSpin == 0f, "Weapon switch leaked rifle state");
        Require(WeaponElements.Buy(0, WeaponElement.Revolver), "Pistol branch was locked by rifle changes");
        string path = "Logs/rifle-branches-save.json";
        try
        {
            File.WriteAllText(path, JsonUtility.ToJson(MetaProgress.Data));
            MetaSave loaded = MetaProgress.Load(path);
            foreach (WeaponElement branch in new[] { WeaponElement.Assault, WeaponElement.Minigun, WeaponElement.Heavy })
                Require(loaded.ranks.Exists(e => e.key == WeaponElements.Key(1, branch) && e.value == 3), "Branch rank lost in save");
            Require(loaded.ranks.Exists(e => e.key == "element:selected:1" && e.value == (int)WeaponElement.Heavy), "Equipped rifle branch lost in save");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
        ClearBullets();
        Object.Destroy(target.gameObject);
        Pass("Save round trip retains all ranks/equipped branch; pistol branch remains available; weapon switch resets state");
    }

    private static void RequireModelFacing(AutoShoter shooter)
    {
        Transform muzzle = ModelSkins.FindMuzzle(shooter.transform);
        Require(muzzle != null, "Generated muzzle missing");
        Transform source = Find(muzzle.parent, "Muzzle");
        Transform grip = Find(muzzle.parent, "GripPoint");
        Require(source != null && grip != null, "Source weapon markers missing");
        float facing = Vector3.Dot((source.position - grip.position).normalized, shooter.transform.forward);
        Require(facing > .8f, "Weapon is held backwards: " + facing);
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
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
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
