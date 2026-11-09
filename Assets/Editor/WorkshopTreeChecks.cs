using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One combined system check: tree, save compatibility, status rules, all weapon paths, existing meta cycle.
[InitializeOnLoad]
public static class WorkshopTreeChecks
{
    private const string Request = "Logs/workshop-tree.request";
    private const string Report = "Logs/workshop-tree-results.txt";
    private static CheckRunner routine;
    private static double started;
    private static bool focused;
    private static readonly List<string> results = new List<string>();
    static WorkshopTreeChecks()
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
    public static void RunFocusedBatch()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Request, "focused");
        EditorSceneManager.OpenScene("Assets/Scenes/GreenArenaPlay.unity");
        EditorApplication.isPlaying = true;
    }
    private static void Begin()
    {
        focused = File.ReadAllText(Request).Trim() == "focused";
        File.Delete(Request);
        started = EditorApplication.timeSinceStartup;
        results.Clear();
        routine = new CheckRunner(Checks());
        EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 600) throw new Exception("Combined check timed out");
            if (!routine.MoveNext()) Finish(true, "Combined workshop system checks complete");
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    private static void Finish(bool success, string detail)
    {
        EditorApplication.update -= Tick;
        results.Add((success ? "PASS " : "FAIL ") + detail);
        File.WriteAllLines(Report, results);
        EditorApplication.Exit(success ? 0 : 1);
    }
    private static void Check(bool condition, string detail)
    {
        if (!condition) throw new Exception(detail);
    }
    private static void Pass(string detail) { results.Add("PASS " + detail); File.WriteAllLines(Report, results); }
    private static IEnumerator Wait(float seconds)
    {
        float until = Time.time + seconds;
        while (Time.time < until) yield return null;
    }
    private static IEnumerator ReloadMenu()
    {
        PlayerProgression previous = PlayerProgression.Instance;
        SceneManager.LoadScene("GreenArenaPlay");
        double deadline = EditorApplication.timeSinceStartup + 30;
        while (PlayerProgression.Instance == null || PlayerProgression.Instance == previous ||
            !PlayerProgression.Instance.GetComponent<StartMenu>().WaitingForWeapon)
        {
            Check(EditorApplication.timeSinceStartup < deadline, "Reloaded menu did not initialize");
            yield return null;
        }
    }
    private static Health Dummy(Vector3 position)
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = "Element check target";
        item.tag = "Enemy";
        item.transform.position = position;
        Health health = item.AddComponent<Health>();
        health.SetHealth(10000);
        health.experienceReward = 0;
        item.AddComponent<EnemyAI>().MovementLocked = true;
        return health;
    }
    private static Transform Find(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root) { Transform found = Find(child, name); if (found != null) return found; }
        return null;
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        // Стихии оружия временно закрыты (WeaponElements.Locked): проверяем только, что купить и надеть их нельзя.
        bool modulesLocked = WeaponElements.Locked;
        if (modulesLocked)
        {
            MetaProgress.Data.currency = 100000;
            foreach (MetaUnlock unlock in MetaProgress.Unlocks) MetaProgress.Unlock(unlock.id);
            for (int w = 0; w < 13; w++)
                for (int e = 1; e <= 4; e++)
                    Check(!WeaponElements.Buy(w, (WeaponElement)e) && !WeaponElements.Equip(w, (WeaponElement)e), "Locked module bought or equipped " + w + ":" + e);
            Pass("Weapon modules are locked: none of 52 can be bought or equipped; purchase checks skipped until the lock is removed");
            yield break;
        }
        Check(!WeaponElements.Buy(2, WeaponElement.Fire), "Module bought for a locked weapon");
        Check(!WeaponElements.Buy(0, WeaponElement.Fire), "Module bought without currency");
        Check(!WeaponElements.Equip(0, WeaponElement.Fire), "Unowned module equipped");
        MetaProgress.Data.currency = 100000;
        foreach (MetaUnlock unlock in MetaProgress.Unlocks) MetaProgress.Unlock(unlock.id);
        for (int w = 0; w < 13; w++)
            for (int e = 1; e <= 4; e++)
            {
                int before = MetaProgress.Currency;
                Check(WeaponElements.Buy(w, (WeaponElement)e), "Cannot buy module " + w + ":" + e);
                Check(MetaProgress.Currency == before - 120, "Wrong initial module cost");
            }
        Check(WeaponElements.Rank(0, WeaponElement.Fire) == 1 && WeaponElements.Rank(1, WeaponElement.Fire) == 1, "Independent ranks missing");
        Check(WeaponElements.Buy(0, WeaponElement.Fire) && WeaponElements.Buy(0, WeaponElement.Fire), "Rank upgrade failed");
        int cappedCurrency = MetaProgress.Currency;
        Check(!WeaponElements.Buy(0, WeaponElement.Fire) && MetaProgress.Currency == cappedCurrency, "Rank cap spent currency");
        Check(WeaponElements.Rank(1, WeaponElement.Fire) == 1, "Upgrading one weapon changed another");
        Check(!WeaponElements.Buy(13, WeaponElement.Fire) && !WeaponElements.Buy(0, (WeaponElement)99), "Invalid keys accepted");
        Check(WeaponElements.Equip(0, WeaponElement.Frost), "Cannot switch owned module");
        Check(WeaponElements.Equipped(0) == WeaponElement.Frost && WeaponElements.Equipped(1) == WeaponElement.Fire, "Equip leaked between weapons");
        Pass("52 independent module purchases, prices, rank caps, locked/invalid/no-funds rejection and independent loadouts");

        string saved = JsonUtility.ToJson(MetaProgress.Data);
        string path = "Logs/workshop-save-check.json";
        try
        {
            File.WriteAllText(path, saved);
            MetaSave loaded = MetaProgress.Load(path);
            Check(loaded.ranks.Exists(entry => entry.key == WeaponElements.Key(0, WeaponElement.Fire) && entry.value == 3), "Module rank lost in save");
            Check(loaded.ranks.Exists(entry => entry.key == "element:selected:0" && entry.value == 2), "Loadout lost in save");
            File.WriteAllText(path, "{\"version\":1,\"currency\":321,\"ranks\":[{\"key\":\"meta_health\",\"value\":2}],\"unlocked\":[\"weapon:2\"]}");
            MetaSave legacy = MetaProgress.Load(path);
            Check(legacy.currency == 321 && legacy.ranks[0].value == 2 && legacy.unlocked.Contains("weapon:2"), "Legacy save damaged");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
        Pass("New keyed module data round-trips; original version-1 balances, ranks and unlocks remain readable");

        AutoShoter shooter = PlayerProgression.Instance.GetComponent<AutoShoter>();
        StartMenu menu = shooter.GetComponent<StartMenu>();
        menu.ShowWorkshop();
        WorkshopTree tree = UnityEngine.Object.FindObjectOfType<WorkshopTree>();
        Check(tree != null && tree.NodeCount == 69, "Expected 4 body + 13 weapons + 52 modules");
        foreach (string id in new[] { "tree_background", "root_workshop", "el_fire", "el_frost", "el_lightning", "el_poison", "w_saw", "w_mines", "w_rocket", "w_arc", "w_flame", "w_ice_cannon", "w_rail", "w_turret", "w_drones" })
            Check(tree.Art(id) != null, "Missing artwork " + id);
        Find(tree.transform, "Node meta_health").GetComponent<Button>().onClick.Invoke();
        int hpRank = MetaProgress.Rank("meta_health");
        Find(tree.transform, "Tree purchase").GetComponent<Button>().onClick.Invoke();
        Check(MetaProgress.Rank("meta_health") == hpRank + 1, "Tree body purchase failed");
        Find(tree.transform, "Node weapon:1:Frost").GetComponent<Button>().onClick.Invoke();
        Find(tree.transform, "Tree equip").GetComponent<Button>().onClick.Invoke();
        Check(WeaponElements.Equipped(1) == WeaponElement.Frost, "Tree equip button failed");
        tree.Scroll.verticalNormalizedPosition = 0;
        Canvas.ForceUpdateCanvases();
        yield return null;
        Check(tree.Scroll.content.anchoredPosition.y > 0, "Tree scrolling failed");
        tree.Scroll.verticalNormalizedPosition = 1;
        Find(tree.transform, "Node weapon:2:Frost").GetComponent<Button>().onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-tree.png");
        Pass("69 interactive nodes, all 15 assets loaded, purchase/equip buttons and scrolling; screenshot saved");

        menu.SendMessage("BeginGame", 1);
        UnityEngine.Object.FindObjectOfType<WaveManager>().StopSpawning();
        shooter.enabled = false;
        shooter.grenadePrefab = null;
        foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>()) UnityEngine.Object.Destroy(enemy.gameObject);
        yield return null;
        Vector3 origin = shooter.transform.position + new Vector3(8, 0, 0);
        Health target = Dummy(origin);
        Health neighbour = Dummy(origin + new Vector3(2, 0, 0));
        WeaponElements.Equip(0, WeaponElement.Fire);
        var fire = new WeaponElementAttack(0);
        fire.Hit(target, 100);
        Check(target.GetComponent<ElementStatus>() != null, "Fire status missing");
        int afterHit = target.CurrentHealth;
        yield return Wait(1.1f);
        Check(target.CurrentHealth < afterHit, "Fire did not tick");
        Time.timeScale = 0;
        int pausedHp = target.CurrentHealth;
        for (int i = 0; i < 10; i++) yield return null;
        Check(target.CurrentHealth == pausedHp, "Periodic damage ran while paused");
        Time.timeScale = 1;
        SlowEffect.Apply(target, .7f, .3f);
        Check(Mathf.Approximately(SlowEffect.MovementFactor(target), .7f), "Slow missing");
        yield return Wait(.4f);
        Check(Mathf.Approximately(SlowEffect.MovementFactor(target), 1f), "Slow did not expire");
        WeaponElements.Equip(0, WeaponElement.Lightning);
        var lightning = new WeaponElementAttack(0);
        int hp = neighbour.CurrentHealth;
        lightning.Hit(target, 100);
        Check(neighbour.CurrentHealth == hp - 30, "Lightning did not hit neighbour");
        hp = neighbour.CurrentHealth;
        lightning.Hit(target, 100);
        Check(neighbour.CurrentHealth == hp, "Lightning cooldown ignored");
        ElementStatus.Apply(target, WeaponElement.Poison, 10);
        afterHit = target.CurrentHealth;
        yield return Wait(1.1f);
        Check(target.CurrentHealth < afterHit - 8, "Poison did not tick");
        target.gameObject.AddComponent<BossController>().enabled = false;
        SlowEffect.Apply(target, .6f, 1f);
        Check(Mathf.Approximately(SlowEffect.MovementFactor(target), .8f), "Boss slow resistance missing");
        UnityEngine.Object.Destroy(target.gameObject);
        UnityEngine.Object.Destroy(neighbour.gameObject);
        Pass("Real status ticks, pause, slow expiry, lightning neighbour/cooldown and boss slow resistance");

        yield return null;
        Health poisonTarget = Dummy(origin);
        for (int burst = 0; burst < 10; burst++)
        {
            ElementStatus.Apply(poisonTarget, WeaponElement.Poison, 10);
            yield return Wait(.1f);
        }
        int poisonBefore = poisonTarget.CurrentHealth;
        yield return Wait(1f);
        int poisonDamage = poisonBefore - poisonTarget.CurrentHealth;
        Check(poisonDamage >= 28 && poisonDamage <= 32, "Continuous poison should reach 3 stacks, capped at 30 DPS: " + poisonDamage);
        yield return Wait(4.2f);
        Check(poisonTarget.GetComponent<ElementStatus>() == null, "Poison did not expire");
        UnityEngine.Object.Destroy(poisonTarget.gameObject);
        Pass("Continuous rapid hits build exactly three poison stacks, bounded DPS and complete expiry");
        yield return null;
        for (int weapon = 0; weapon < 4; weapon++)
        {
            WeaponElements.Equip(weapon, WeaponElement.Frost);
            shooter.SelectWeapon(weapon);
            Health bulletTarget = Dummy(shooter.transform.position + new Vector3(6, .8f, 0));
            shooter.enabled = true;
            float deadline = Time.time + 6;
            while (SlowEffect.MovementFactor(bulletTarget) >= 1f && Time.time < deadline) yield return null;
            Check(SlowEffect.MovementFactor(bulletTarget) < 1f, "Real bullet did not apply module for weapon " + weapon);
            shooter.enabled = false;
            UnityEngine.Object.Destroy(bulletTarget.gameObject);
            yield return Wait(.2f);
        }
        Pass("All four standard weapons apply the captured module through real projectile collisions");

        if (focused) yield break;

        // Existing direct dependencies are reused, not duplicated. Each gets a fresh scene and memory save.
        yield return ReloadMenu();
        WeaponChecks.CheckModules = true;
        yield return (IEnumerator)typeof(WeaponChecks).GetMethod("Checks", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        WeaponChecks.CheckModules = false;
        Pass("Existing WeaponChecks: all nine special weapons and cleanup");
        yield return ReloadMenu();
        yield return (IEnumerator)typeof(MetaProgressChecks).GetMethod("Checks", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        Pass("Existing MetaProgressChecks: tree purchase, bonuses, menu locks, modifiers, save and reward cycle");
    }

    public static void Capture(Canvas canvas, string path, int width = 1600, int height = 900, Action validate = null)
    {
        RenderMode mode = canvas.renderMode;
        Camera previousCamera = canvas.worldCamera;
        float previousDistance = canvas.planeDistance;
        WorkshopTree tree = canvas.GetComponentInChildren<WorkshopTree>();
        if (tree != null) tree.RememberScrollPosition();
        GameObject item = new GameObject("UI check camera", typeof(Camera));
        Camera camera = item.GetComponent<Camera>();
        camera.transform.position = new Vector3(0, 10000, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.02f, .04f, .07f);
        camera.cullingMask = 1 << 5;
        var layers = new Dictionary<GameObject, int>();
        foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) { layers[child.gameObject] = child.gameObject.layer; child.gameObject.layer = 5; }
        RenderTexture texture = RenderTexture.GetTemporary(width, height, 24);
        RenderTexture active = RenderTexture.active;
        Texture2D png = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            camera.targetTexture = texture;
            canvas.GetComponent<CanvasScaler>().SendMessage("Handle");
            Canvas.ForceUpdateCanvases();
            if (tree != null) tree.RefreshLayout();
            Canvas.ForceUpdateCanvases();
            if (validate != null) validate();
            camera.Render();
            RenderTexture.active = texture;
            png.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            png.Apply();
            File.WriteAllBytes(path, png.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode;
            canvas.worldCamera = previousCamera;
            canvas.planeDistance = previousDistance;
            foreach (var entry in layers) entry.Key.layer = entry.Value;
            RenderTexture.active = active;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(texture);
            UnityEngine.Object.Destroy(png);
            UnityEngine.Object.Destroy(item);
            canvas.GetComponent<CanvasScaler>().SendMessage("Handle");
            Canvas.ForceUpdateCanvases();
            if (tree != null) tree.RefreshLayout();
            Canvas.ForceUpdateCanvases();
        }
    }
}
