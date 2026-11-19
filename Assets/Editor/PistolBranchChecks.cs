using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Одна короткая проверка веток пистолета (12.11.2026): мастерская, Револьвер, Двойные, Липкий.
// Запуск: Unity -batchmode -projectPath . -executeMethod PistolBranchChecks.RunBatch -logFile Logs/pistol.log
[InitializeOnLoad]
public static class PistolBranchChecks
{
    private const string Request = "Logs/pistol-branches.request";
    private const string Report = "Logs/pistol-branches-results.txt";
    private static CheckRunner routine;
    private static double started;
    private static byte[] languageBackup;
    private static bool hadLanguageSettings;
    private static int runtimeErrors;
    private static readonly List<string> results = new List<string>();

    static PistolBranchChecks()
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
        started = EditorApplication.timeSinceStartup;
        results.Clear();
        hadLanguageSettings = File.Exists(GameText.SettingsPath);
        languageBackup = hadLanguageSettings ? File.ReadAllBytes(GameText.SettingsPath) : null;
        runtimeErrors = 0;
        Application.logMessageReceived += OnLog;
        routine = new CheckRunner(Checks());
        EditorApplication.update += Tick;
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            runtimeErrors++;
            results.Add("RUNTIME " + message);
        }
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 240) throw new Exception("Pistol branch check timed out");
            if (!routine.MoveNext()) Finish(true, "Pistol branch checks complete");
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    private static void Finish(bool success, string detail)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        if (hadLanguageSettings) File.WriteAllBytes(GameText.SettingsPath, languageBackup);
        else if (File.Exists(GameText.SettingsPath)) File.Delete(GameText.SettingsPath);
        success = success && runtimeErrors == 0;
        results.Add((success ? "PASS " : "FAIL ") + detail + "; runtime errors=" + runtimeErrors);
        File.WriteAllLines(Report, results);
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static void Check(bool condition, string detail) { if (!condition) throw new Exception(detail); }
    private static void Pass(string detail) { results.Add("PASS " + detail); File.WriteAllLines(Report, results); }

    private static IEnumerator Wait(float seconds)
    {
        float until = Time.time + seconds;
        while (Time.time < until) yield return null;
    }

    private static Health Dummy(Vector3 position)
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = "Pistol check target";
        item.tag = "Enemy";
        item.transform.position = position;
        Health health = item.AddComponent<Health>();
        health.SetHealth(100000);
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

    private static void Buy(WeaponElement branch, int ranks)
    {
        for (int i = 0; i < ranks; i++) Check(WeaponElements.Buy(0, branch), "Cannot buy " + branch + " rank " + (i + 1));
        Check(WeaponElements.Equip(0, branch), "Cannot equip " + branch);
    }

    private static void ClearArena(AutoShoter shooter)
    {
        UnityEngine.Object.FindObjectOfType<WaveManager>().StopSpawning();
        shooter.grenadePrefab = null;
        foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>())
            if (enemy.name != "Pistol check target") UnityEngine.Object.Destroy(enemy.gameObject);
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        MetaProgress.Data.currency = 100000;
        foreach (MetaUnlock unlock in MetaProgress.Unlocks) MetaProgress.Unlock(unlock.id);

        // Мастерская: у пистолета три узла, у остальных четыре; кнопка покупки и надевания работает.
        AutoShoter shooter = PlayerProgression.Instance.GetComponent<AutoShoter>();
        StartMenu menu = shooter.GetComponent<StartMenu>();
        menu.ShowWorkshop();
        WorkshopTree tree = UnityEngine.Object.FindObjectOfType<WorkshopTree>();
        Check(tree != null, "Workshop tree missing");
        Transform group = Find(tree.transform, "Weapon group 0");
        Check(group != null && group.GetComponentsInChildren<Button>().Length == 4, "Pistol group should have weapon + 3 branch nodes");
        foreach (string branch in new[] { "Revolver", "Dual", "Sticky" })
            Check(Find(group, "Node weapon:0:" + branch) != null, "Pistol branch node missing " + branch);
        Check(Find(group, "Node weapon:0:Poison") == null && Find(group, "Node weapon:0:Fire") == null, "Old element nodes still on the pistol");
        Check(tree.Art("el_revolver") != null && tree.Art("el_dual") != null && tree.Art("el_sticky") != null, "Branch icons missing");
        Find(tree.transform, "Node weapon:0:Revolver").GetComponent<Button>().onClick.Invoke();
        Find(tree.transform, "Tree purchase").GetComponent<Button>().onClick.Invoke();
        Check(WeaponElements.Rank(0, WeaponElement.Revolver) == 1, "Tree purchase of the Revolver branch failed");
        Pass("Workshop: pistol shows exactly 3 branch nodes with icons, no old elements, UI purchase works");

        // Револьвер ранг 2: 6 выстрелов, затем пауза не меньше 0,8 с.
        Buy(WeaponElement.Revolver, 1);
        Pass("diag pre-begin: unlocked0=" + MetaProgress.IsWeaponUnlocked(0) + " weapon0=" + (shooter.weapons[0] != null ? shooter.weapons[0].weaponName + " usable=" + shooter.weapons[0].IsUsable : "null") + " coop=" + CoopMode.Selected + " waiting=" + menu.WaitingForWeapon);
        try { typeof(StartMenu).GetMethod("BeginGame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(menu, new object[] { 0 }); }
        catch (Exception error) { throw new Exception("BeginGame failed: " + (error.InnerException ?? error)); }
        yield return null;
        yield return null;
        Pass("diag after BeginGame: timeScale=" + Time.timeScale + " waiting=" + menu.WaitingForWeapon + " enabled=" + shooter.enabled);
        Time.timeScale = 1f;
        ClearArena(shooter);
        Health target = Dummy(shooter.transform.position + new Vector3(6, .8f, 0));
        shooter.enabled = true;
        var hitTimes = new List<float>();
        int last = target.CurrentHealth;
        float end = Time.time + 9f;
        Pass("diag before revolver loop: time=" + Time.time + " timeScale=" + Time.timeScale);
        while (Time.time < end)
        {
            yield return null;
            if (target.CurrentHealth < last) { hitTimes.Add(Time.time); last = target.CurrentHealth; }
        }
        float biggestGap = 0f;
        for (int i = 1; i < hitTimes.Count; i++) biggestGap = Mathf.Max(biggestGap, hitTimes[i] - hitTimes[i - 1]);
        Check(hitTimes.Count >= 7, "Revolver fired too few shots: " + hitTimes.Count);
        Check(biggestGap >= 1.2f, "Revolver reload pause not visible, max gap " + biggestGap);
        Pass("Revolver rank 2: " + hitTimes.Count + " hits in 9 s, longest pause " + biggestGap.ToString("0.00") + " s");
        shooter.enabled = false;
        UnityEngine.Object.Destroy(target.gameObject);

        // Двойные ранг 1: две цели получают урон почти одновременно; одна цель — тоже работает.
        Buy(WeaponElement.Dual, 1);
        shooter.SelectWeapon(0);
        Health a = Dummy(shooter.transform.position + new Vector3(6, .8f, 2));
        Health b = Dummy(shooter.transform.position + new Vector3(6, .8f, -2));
        int ha = a.CurrentHealth, hb = b.CurrentHealth;
        shooter.enabled = true;
        float deadline = Time.time + 4f;
        while ((a.CurrentHealth >= ha || b.CurrentHealth >= hb) && Time.time < deadline) yield return null;
        Check(a.CurrentHealth < ha && b.CurrentHealth < hb, "Dual pistols did not hit two different targets");
        shooter.enabled = false;
        UnityEngine.Object.Destroy(a.gameObject);
        UnityEngine.Object.Destroy(b.gameObject);
        Pass("Dual pistols rank 1: both enemies took damage");

        // Липкий ранг 3: заряд прилипает, рядом взрыв по соседу, компонент исчезает.
        Buy(WeaponElement.Sticky, 3);
        var sticky = new WeaponElementAttack(0);
        Check(sticky.Element == WeaponElement.Sticky && sticky.Rank == 3, "Sticky loadout not captured");
        Vector3 origin = shooter.transform.position + new Vector3(0, .8f, 8);
        Health host = Dummy(origin);
        Health neighbour = Dummy(origin + new Vector3(1.5f, 0, 0));
        int neighbourBefore = neighbour.CurrentHealth;
        sticky.Hit(host, 100);
        Check(host.GetComponent<StickyCharge>() != null, "Sticky charge not attached");
        yield return Wait(1.8f);
        Check(neighbour.CurrentHealth < neighbourBefore, "Sticky explosion did not damage the neighbour");
        Check(host.GetComponent<StickyCharge>() == null, "Sticky charge did not detonate and clean up");
        UnityEngine.Object.Destroy(host.gameObject);
        UnityEngine.Object.Destroy(neighbour.gameObject);
        Pass("Sticky rank 3: charge attaches, detonates after delay, damages a neighbour, cleans up");
        yield return AdditionalChecks(shooter);
    }

    private static IEnumerator AdditionalChecks(AutoShoter shooter)
    {
        yield return null;
        // Kill after the component's frame: transfer must happen synchronously in Died.
        Vector3 point = shooter.transform.position + Vector3.forward * 7f;
        Health host = Dummy(point);
        Health full = Dummy(point + Vector3.right);
        Health free = Dummy(point + Vector3.right * 3f);
        StickyCharge.Attach(host, 100, 2, shooter.Stats);
        StickyCharge.Attach(full, 100, 2, shooter.Stats);
        yield return null;
        host.TakeDamage(200000);
        Check(free.GetComponent<StickyCharge>() != null, "Death did not immediately transfer charge past a full target");
        int before = free.CurrentHealth;
        yield return Wait(1.7f);
        Check(free.CurrentHealth < before, "Transferred charge lost its detonation");
        UnityEngine.Object.Destroy(full.gameObject);
        UnityEngine.Object.Destroy(free.gameObject);
        Pass("Sticky rank 2: death callback transfers immediately, skips full target, detonates on schedule");
        yield return null;

        Buy(WeaponElement.Dual, 2);
        shooter.SelectWeapon(0);
        shooter.enabled = false;
        shooter.Stats.ExtraPellets = 1;
        shooter.Stats.Set(0f, 0f, 2, 1);
        shooter.SetProgressionMultipliers(1f, 1f);
        yield return null;
        Transform left = ModelSkins.FindOffhandMuzzle(shooter.transform);
        Transform right = ModelSkins.FindMuzzle(shooter.transform);
        Check(left != null && right != null, "Two physical weapon muzzles missing");
        Check((left.position - right.position).magnitude > 0.2f, "Both pistols are in the same hand");
        Check(Vector3.Dot(left.forward, right.forward) > 0.8f, "Left pistol points away from aim");
        Check(Mathf.Abs(shooter.CurrentFireInterval - 0.6f / 1.15f) < 0.001f, "Dual volley interval does not match balance");
        Capture("dual-pistols", shooter.transform, true);
        Health target = Dummy(point);
        var shoot = typeof(AutoShoter).GetMethod("Shoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (Bullet bullet in UnityEngine.Object.FindObjectsOfType<Bullet>()) UnityEngine.Object.Destroy(bullet.gameObject);
        yield return null;
        shoot.Invoke(shooter, new object[] { target.transform });
        Bullet[] bullets = UnityEngine.Object.FindObjectsOfType<Bullet>();
        Check(bullets.Length == 4, "Dual + extra pellet should spawn 4 bullets, got " + bullets.Length);
        int fromLeft = 0, fromRight = 0;
        foreach (Bullet bullet in bullets)
        {
            Check(bullet.damage == 18, "One pistol lost full base damage: " + bullet.damage);
            Check(bullet.pierce == 3 && bullet.ricochet == 1 && bullet.elementAttack != null, "Upgrades not shared by both pistols");
            if (Vector3.Distance(bullet.transform.position, left.position) < Vector3.Distance(bullet.transform.position, right.position)) fromLeft++;
            else fromRight++;
            UnityEngine.Object.Destroy(bullet.gameObject);
        }
        Check(fromLeft == 2 && fromRight == 2, "Both volleys must leave their own muzzle");
        yield return null;
        shooter.Evolve();
        shoot.Invoke(shooter, new object[] { target.transform });
        Check(UnityEngine.Object.FindObjectsOfType<Bullet>().Length == shooter.CurrentPelletCount * 2, "Evolution disabled the second pistol");
        foreach (Bullet bullet in UnityEngine.Object.FindObjectsOfType<Bullet>()) UnityEngine.Object.Destroy(bullet.gameObject);
        UnityEngine.Object.Destroy(target.gameObject);
        shooter.Stats.ExtraPellets = 0;
        shooter.Stats.Set(0f, 0f, 0, 0);
        shooter.SelectWeapon(1);
        shooter.enabled = false;
        Check(ModelSkins.FindOffhandMuzzle(shooter.transform) == null, "Switching weapons left the second pistol equipped");
        Pass("Dual: separate hands/muzzles, full damage, 4 upgraded bullets, evolution and cleanup; close-up captured");
        yield return null;

        // Ordinary enemies must not block the Queen's brood.
        WaveManager waves = UnityEngine.Object.FindObjectOfType<WaveManager>();
        waves.wave = 30;
        for (int i = 0; i < 20; i++) Dummy(point + Vector3.right * (15 + i));
        PlayerHealth hero = shooter.GetComponent<PlayerHealth>();
        hero.IncreaseMaxHealth(10000);
        BossController queen = BossController.Create(point + Vector3.forward * 4f, shooter.transform, BossKind.SwarmQueen);
        yield return null;
        queen.StopAllCoroutines();
        queen.enabled = false;
        var brood = typeof(BossController).GetMethod("QueenBrood", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        queen.StartCoroutine((IEnumerator)brood.Invoke(queen, null));
        yield return Wait(1.2f);
        Check(queen.QueenMinionsAlive == 18, "Ordinary crowd blocked first brood: " + queen.QueenMinionsAlive);
        typeof(BossController).GetMethod("Enrage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(queen, null);
        queen.StartCoroutine((IEnumerator)brood.Invoke(queen, null));
        yield return Wait(1.2f);
        Check(queen.QueenMinionsAlive == 44, "Enraged brood should add 26 minions: " + queen.QueenMinionsAlive);
        queen.StartCoroutine((IEnumerator)brood.Invoke(queen, null));
        yield return Wait(1.2f);
        Check(queen.QueenMinionsAlive == 60, "Queen brood cap not respected: " + queen.QueenMinionsAlive);
        Check(EnemyAI.ActiveCount <= waves.settings.AbsoluteLimitForWave(waves.wave), "Brood exceeded global cap");
        foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>()) enemy.MovementLocked = true;
        UnityEngine.Object.Destroy(queen.gameObject);
        foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>()) UnityEngine.Object.Destroy(enemy.gameObject);
        Pass("Queen: 18 then 26 minions despite 20 ordinary enemies; cap 60, global cap retained");
        yield return null;

        hero.TakeDamage(1, HeroDamageCause.Contact);
        HeroDamageCause recorded = hero.LastDamageCause;
        hero.GrantInvulnerability(.15f);
        hero.TakeDamage(100, HeroDamageCause.Fire);
        Check(hero.LastDamageCause == recorded, "Ignored damage overwrote death cause");
        yield return Wait(.2f);
        hero.TakeDamage(200000, HeroDamageCause.Lightning);
        Check(hero.IsDead && GameSession.Instance.State == GameRunState.Defeat, "Death did not show results");
        Text body = (Text)typeof(GameSession).GetField("resultBody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(GameSession.Instance);
        GameText.SetLanguage(GameLanguage.English);
        Check(body.text.Contains("Cause of death: boss lightning") && body.text.Contains("Final hit damage:"), "English death reason missing: " + body.text);
        Capture("death-reason-en", shooter.transform, false);
        GameText.SetLanguage(GameLanguage.Russian);
        Check(body.text.Contains(GameText.Source("death.cause.lightning")), "Russian death reason missing");
        Capture("death-reason-ru", shooter.transform, false);
        Pass("Death: invulnerability preserves source; lethal lightning shown on results in EN/RU");
    }

    private static void Capture(string name, Transform hero, bool closeup)
    {
        Camera camera = Camera.main;
        Vector3 position = camera.transform.position;
        Quaternion rotation = camera.transform.rotation;
        bool orthographic = camera.orthographic;
        float size = camera.orthographicSize;
        var switched = new List<Canvas>();
        foreach (Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            switched.Add(canvas);
        }
        if (closeup)
        {
            camera.transform.position = hero.position + new Vector3(0, 5, -4);
            camera.transform.LookAt(hero.position + Vector3.up);
            camera.orthographic = true;
            camera.orthographicSize = 2.1f;
            foreach (Canvas canvas in switched) canvas.enabled = false;
        }
        var texture = new RenderTexture(1280, 720, 24);
        RenderTexture previous = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        camera.targetTexture = texture;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = texture;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        image.Apply();
        File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = previousActive;
        camera.targetTexture = previous;
        camera.transform.SetPositionAndRotation(position, rotation);
        camera.orthographic = orthographic;
        camera.orthographicSize = size;
        foreach (Canvas canvas in switched) { canvas.enabled = true; canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
        UnityEngine.Object.Destroy(image);
        UnityEngine.Object.Destroy(texture);
    }
}
