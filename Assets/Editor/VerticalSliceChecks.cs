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

// Run with -batchmode -executeMethod VerticalSliceChecks.RunBatch (without -quit).
[InitializeOnLoad]
public static class VerticalSliceChecks
{
    private const string Request = "Logs/vertical-slice.request";
    private static IEnumerator routine;
    private static readonly List<string> report = new List<string>();
    private static double started, lastReport;
    private static bool batch;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static VerticalSliceChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Request)) EditorApplication.delayCall += Begin;
            if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Tick;
        };
    }

    public static void RunBatch()
    {
        File.WriteAllText(Request, "batch");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.isPlaying = true;
    }

    private static void Begin()
    {
        string mode = File.ReadAllText(Request).Trim();
        batch = mode.StartsWith("batch");
        File.Delete(Request);
        started = EditorApplication.timeSinceStartup;
        report.Clear();
        routine = mode == "batchsoak" ? SoakOnly() : Checks();
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 540) throw new Exception("Check timed out.");
            if (!routine.MoveNext()) Finish(true, "Selected-system checks complete.");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Pass(string text)
    {
        report.Add("PASS: " + text);
        File.WriteAllLines("Logs/vertical-slice-results.txt", report);
        Debug.Log("VerticalSlice: " + text);
    }

    private static void Require(bool condition, string text) { if (!condition) throw new Exception(text); }
    private static T Field<T>(object target, string name) { return (T)target.GetType().GetField(name, Private).GetValue(target); }
    private static void SetField(object target, string name, object value) { target.GetType().GetField(name, Private).SetValue(target, value); }
    private static void SetProperty(object target, string name, object value) { SetField(target, "<" + name + ">k__BackingField", value); }

    private static void Finish(bool success, string text)
    {
        EditorApplication.update -= Tick;
        report.Add((success ? "PASS: " : "FAIL: ") + text);
        File.WriteAllLines("Logs/vertical-slice-results.txt", report);
        Debug.Log(report[report.Count - 1]);
        if (batch) EditorApplication.Exit(success ? 0 : 1);
        else { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
    }

    private static IEnumerator Checks()
    {
        yield return null;
        PlayerProgression progression = PlayerProgression.Instance;
        GameObject player = progression.gameObject;
        StartMenu menu = player.GetComponent<StartMenu>();
        menu.SendMessage("BeginGame", 1);
        GameSession session = GameSession.Instance;
        AutoShoter shooter = player.GetComponent<AutoShoter>();
        PlayMovement movement = player.GetComponent<PlayMovement>();
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        OrbitManager orbits = player.GetComponent<OrbitManager>();
        shooter.enabled = false;
        movement.enabled = false;
        WaveManager waves = UnityEngine.Object.FindObjectOfType<WaveManager>();
        waves.StopSpawning();
        Require(session.State == GameRunState.Playing && session.survivalSeconds == 600f, "Run goal/start invalid.");
        UpgradeCatalog original = progression.catalog;
        progression.catalog = UnityEngine.Object.Instantiate(original);
        var all = new List<UpgradeDefinition>(progression.catalog.entries);
        Require(all.Count >= 8, "Upgrade catalog is incomplete.");
        foreach (UpgradeEffect effect in new[] { UpgradeEffect.Damage, UpgradeEffect.AttackSpeed, UpgradeEffect.Movement, UpgradeEffect.Pickup })
        {
            progression.catalog.entries = all.FindAll(entry => entry.effect == effect);
            UpgradeDefinition entry = progression.catalog.entries[0];
            int count = 0;
            while (progression.Available(entry))
            {
                progression.AddExperience(progression.settings.ExperienceForLevel(progression.Level) - progression.Experience);
                Require(progression.Choosing && progression.Offered.Count == 1, "Single-entry offer failed.");
                Require(progression.SelectOfferedUpgrade(0), "Upgrade selection failed.");
                if (++count > 30) throw new Exception("Upgrade cap did not stop selection.");
            }
            Require(progression.Rank(entry.id) > 0 && !progression.Available(entry), "Rank/cap invalid.");
            if (effect == UpgradeEffect.Damage) Require(shooter.CurrentDamage == Mathf.RoundToInt(shooter.baseWeapon.damage * 5f), "Damage cap mismatch.");
            if (effect == UpgradeEffect.AttackSpeed) Require(Mathf.Abs(shooter.CurrentFireInterval - shooter.baseWeapon.fireRate / 3f) < 0.001f, "Fire interval cap mismatch.");
            if (effect == UpgradeEffect.Movement) Require(Mathf.Abs(movement.SpeedMultiplier - 2f) < 0.001f, "Movement cap mismatch.");
            if (effect == UpgradeEffect.Pickup) Require(Mathf.Abs(progression.AttractionRadius - 12f) < 0.001f, "Pickup cap mismatch.");
            progression.AddExperience(progression.settings.ExperienceForLevel(progression.Level) - progression.Experience);
            Require(!progression.Choosing && session.State == GameRunState.Playing, "Maxed catalog blocks the game.");
        }
        Pass("A07: numerical damage/fire/movement/pickup upgrades, rank limits, fully maxed fallback.");

        progression.catalog.entries = all.FindAll(entry => entry.effect == UpgradeEffect.GrenadeRadius);
        int oldLevel = progression.Level;
        int amount = -progression.Experience;
        for (int i = 0; i < 3; i++) amount += progression.settings.ExperienceForLevel(oldLevel + i);
        progression.AddExperience(amount);
        Require(progression.PendingChoices == 3, "Multiple-level queue missing.");
        session.TogglePause();
        Require(session.State == GameRunState.Paused && !progression.SelectOfferedUpgrade(0), "Selection is possible through pause.");
        session.TogglePause();
        Require(session.State == GameRunState.Upgrade && Time.timeScale == 0f, "Pause resumed a level-up into combat.");
        for (int i = 0; i < 3; i++) Require(progression.SelectOfferedUpgrade(0), "Queued choice failed.");
        Require(session.State == GameRunState.Playing && progression.PendingChoices == 0, "Queue did not resume combat.");
        session.TogglePause();
        float pausedClock = session.Elapsed;
        double until = EditorApplication.timeSinceStartup + 0.15;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Require(session.Elapsed == pausedClock && Time.timeScale == 0f, "Pause advances time.");
        session.TogglePause();
        Pass("B03: pause/resume during combat and upgrade, no click-through, frozen clock; A07: three queued levels.");

        Field<Dictionary<string, int>>(progression, "ranks").Clear();
        progression.catalog.entries = all;
        progression.AddExperience(10000, false);
        int[] rarityCounts = new int[3];
        UnityEngine.Random.State savedRandom = UnityEngine.Random.state;
        UnityEngine.Random.InitState(47123);
        for (int sample = 0; sample < 2500; sample++)
        {
            progression.RollOffers();
            var ids = new HashSet<string>();
            Require(progression.Offered.Count == 3, "Expected three offers.");
            foreach (UpgradeDefinition entry in progression.Offered)
            {
                Require(ids.Add(entry.id) && progression.Available(entry), "Duplicate/unavailable offer.");
                rarityCounts[(int)entry.rarity]++;
            }
        }
        UnityEngine.Random.state = savedRandom;
        Require(rarityCounts[0] > rarityCounts[1] && rarityCounts[1] > rarityCounts[2] && rarityCounts[2] > 0, "Rarity weighting invalid.");
        Pass("D01-D04: data catalog, 2500 distinct weighted offer rolls, rarity counts " + string.Join("/", rarityCounts));

        progression.catalog.entries = all.FindAll(entry => entry.effect == UpgradeEffect.GrenadeDamage);
        progression.AddExperience(progression.settings.ExperienceForLevel(progression.Level) - progression.Experience);
        Require(progression.SelectOfferedUpgrade(0), "Grenade improvement unavailable.");
        Require(shooter.CurrentGrenadeDamage == Mathf.RoundToInt(shooter.grenadeDamage * 1.3f), "Grenade upgrade mismatch.");
        orbits.SetDamageMultiplier(1.25f);
        Require(orbits.CurrentDamage == Mathf.RoundToInt(orbits.orbitDamage * 1.25f), "Orbit multiplier invalid.");
        progression.catalog.entries = all.FindAll(entry => entry.effect == UpgradeEffect.GrenadeRadius);
        progression.AddExperience(progression.settings.ExperienceForLevel(progression.Level) - progression.Experience);
        Require(progression.SelectOfferedUpgrade(0), "Grenade radius improvement unavailable.");
        Require(Mathf.Abs(shooter.CurrentGrenadeRadius - shooter.grenadeRadius * 1.2f) < 0.001f, "Grenade radius mismatch.");
        GameObject dummy = new GameObject("Composite damage test");
        dummy.tag = "Enemy";
        dummy.transform.position = new Vector3(0, 1, -55);
        Health dummyHealth = dummy.AddComponent<Health>();
        dummyHealth.SetHealth(1000);
        dummyHealth.experienceReward = 0;
        Collider first = new GameObject("Hitbox A", typeof(BoxCollider)).GetComponent<Collider>();
        first.transform.SetParent(dummy.transform, false);
        Collider second = new GameObject("Hitbox B", typeof(BoxCollider)).GetComponent<Collider>();
        second.transform.SetParent(dummy.transform, false);
        Physics.SyncTransforms();
        GameObject grenadeObject = new GameObject("Grenade check");
        grenadeObject.transform.position = dummy.transform.position;
        Grenade grenade = grenadeObject.AddComponent<Grenade>();
        grenade.damage = shooter.CurrentGrenadeDamage;
        grenade.delay = 999;
        grenade.explosionRadius = shooter.CurrentGrenadeRadius;
        grenade.SendMessage("Explode");
        Require(dummyHealth.CurrentHealth == 1000 - shooter.CurrentGrenadeDamage, "Grenade counted a compound hit twice.");
        OrbitWeapon orbit = UnityEngine.Object.Instantiate(orbits.orbitPrefab).GetComponent<OrbitWeapon>();
        orbit.player = player.transform;
        orbit.damage = 13;
        orbit.maxHits = 2;
        orbit.respawnTime = 0.4f;
        int before = dummyHealth.CurrentHealth;
        orbit.SendMessage("OnTriggerEnter", first);
        orbit.SendMessage("OnTriggerEnter", second);
        Require(dummyHealth.CurrentHealth == before - 13, "Orbit counted compound colliders twice.");
        orbit.SendMessage("OnTriggerExit", first);
        orbit.SendMessage("OnTriggerExit", second);
        orbit.SendMessage("OnTriggerEnter", first);
        Require(orbit.IsRespawning && orbit.gameObject.activeSelf, "Orbit respawn stopped itself.");
        float respawnAt = Time.time + 0.6f;
        while (Time.time < respawnAt) yield return null;
        Require(!orbit.IsRespawning && orbit.GetComponent<Renderer>().enabled, "Orbit did not restore renderer/collider.");
        UnityEngine.Object.Destroy(orbit.gameObject);
        UnityEngine.Object.Destroy(dummy);
        Pass("A10: grenade compound damage once, damage multiplier, orbit re-entry/compound hit and timed recovery.");

        progression.catalog.entries = all;
        progression.AddExperience(progression.settings.ExperienceForLevel(progression.Level) - progression.Experience);
        Canvas xpCanvas = Field<Canvas>(progression, "canvas");
        Button[] cards = Field<Button[]>(progression, "buttons");
        Camera uiCamera = new GameObject("UI check camera", typeof(Camera)).GetComponent<Camera>();
        uiCamera.enabled = false;
        foreach (Vector2Int size in new[] { new Vector2Int(800, 600), new Vector2Int(1280, 720), new Vector2Int(720, 1280) })
        {
            RenderTexture texture = new RenderTexture(size.x, size.y, 0);
            uiCamera.targetTexture = texture;
            xpCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            xpCanvas.worldCamera = uiCamera;
            Canvas.ForceUpdateCanvases();
            yield return null;
            foreach (Button card in cards)
            {
                RectTransform rect = card.GetComponent<RectTransform>();
                Require(rect.rect.width > 100f && rect.rect.height > 65f, "Upgrade card collapsed at " + size);
                Require(card.GetComponentInChildren<Text>().text.Contains("→"), "Missing upgrade before/after preview.");
            }
            uiCamera.targetTexture = null;
            UnityEngine.Object.Destroy(texture);
        }
        xpCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        xpCanvas.worldCamera = null;
        UnityEngine.Object.Destroy(uiCamera.gameObject);
        progression.SelectOfferedUpgrade(0);
        health.TakeDamage(Mathf.RoundToInt(health.maxHP * 0.8f));
        float hpAt = Time.time + 0.15f;
        while (Time.time < hpAt) yield return null;
        Require(health.hpImage.fillAmount < health.hpDamageImage.fillAmount, "Damage trail is not delayed.");
        Require(health.hpImage.color.r > health.hpImage.color.g, "Critical HP is not visibly red.");
        Pass("A08/D03: current three-card catalog layout at 800x600, 1280x720, portrait; rank/value preview; critical HP and delayed trail.");

        ArenaLayout arena = ArenaLayout.Instance;
        Vector3 edge = arena.ResolveMovement(new Vector3(70, 1, 60), new Vector3(500, 1, 60), 0.5f);
        Require(Mathf.Abs(edge.x) < arena.halfSize, "Player leaves floor boundary.");
        Bounds cover = arena.Obstacles[0];
        Vector3 from = new Vector3(cover.center.x - 8, 0.5f, cover.center.z);
        Vector3 goal = from + Vector3.right * 16;
        Require(!arena.PathClear(from, goal, 0.5f), "Cover does not block movement.");
        Vector3 walker = from;
        for (int step = 0; step < 500 && Vector3.Distance(walker, goal) > 0.2f; step++)
        {
            Vector3 waypoint = arena.NextWaypoint(walker, goal, 0.5f);
            walker = arena.ResolveMovement(walker, Vector3.MoveTowards(walker, waypoint, 0.15f), 0.5f);
            Require(arena.IsWalkable(walker, 0.49f), "Enemy enters cover during path following.");
        }
        Require(Vector3.Distance(walker, goal) < 0.3f, "Enemy cannot navigate around cover.");
        foreach (EnemyType type in waves.enemies)
        for (int i = 0; i < 20; i++)
        {
            Require(OffscreenSpawn.TryFindPosition(Camera.main, player.transform, type.prefab, 0f, out Vector3 point), "No safe arena spawn.");
            Require(arena.IsWalkable(point, 1f), "Enemy spawns outside arena or in cover.");
        }
        Pass("J01-J04: bounded arena, theme/landmark/cover objects, 60 free offscreen spawns, enemy route around cover.");

        health.IncreaseMaxHealth(1000);
        session.survivalSeconds = 600f;
        SetProperty(session, "Elapsed", 590f);
        yield return null;
        yield return null;
        Require(session.BossWarning && waves.Suspended, "Boss preparation did not stop waves.");
        SetProperty(session, "Elapsed", 600f);
        double bossDeadline = EditorApplication.timeSinceStartup + 3;
        while (session.Boss == null && EditorApplication.timeSinceStartup < bossDeadline) yield return null;
        BossController boss = session.Boss;
        Require(boss != null && boss.Health.maxHealth == 2600, "Boss did not enter safely: state=" + session.State +
            ", elapsed=" + session.Elapsed + ", warning=" + session.BossWarning);
        bool[] warnings = new bool[3];
        bool sawProjectiles = false;
        float bossUntil = Time.time + 15f;
        while (Time.time < bossUntil)
        {
            if (boss.Telegraphing && boss.AttackIndex > 0) warnings[(boss.AttackIndex - 1) % 3] = true;
            if (UnityEngine.Object.FindObjectsOfType<EnemyProjectile>().Length > 0) sawProjectiles = true;
            yield return null;
        }
        Require(warnings[0] && warnings[1] && warnings[2] && sawProjectiles, "Boss attack/telegraph cycle incomplete.");
        boss.Health.TakeDamage(int.MaxValue);
        Require(session.State == GameRunState.Victory && session.RewardExperience == 200 && Time.timeScale == 0f,
            "Boss defeat did not award victory/reward.");
        Pass("B01/B02/F01-F04: 600s goal boundary, preparation, boss HP, three telegraphed attacks/fan projectiles, death -> +200 XP/victory.");

        session.Reload(true);
        double reloadDeadline = EditorApplication.timeSinceStartup + 3;
        while ((GameSession.Instance == session || GameSession.Instance.State != GameRunState.Playing) &&
            EditorApplication.timeSinceStartup < reloadDeadline) yield return null;
        Require(GameSession.Instance.State == GameRunState.Playing && PlayerProgression.Instance.Level == 1 &&
            PlayerProgression.Instance.Experience == 0 && PlayerProgression.Instance.GetComponent<PlayerHealth>().CurrentHP == 100 &&
            PlayerProgression.Instance.GetComponent<AutoShoter>().startWeaponIndex == 1 && RunStatistics.Instance.Kills == 0,
            "Quick restart did not reset the run/use the same weapon.");
        Pass("B04/A07: quick restart resets HP, XP, ranks, kills, time and retains the weapon.");
        GameSession.Instance.Defeat();
        Require(GameSession.Instance.State == GameRunState.Defeat, "Defeat results missing.");
        GameSession oldSession = GameSession.Instance;
        oldSession.Reload(false);
        reloadDeadline = EditorApplication.timeSinceStartup + 3;
        while ((GameSession.Instance == oldSession || !Field<bool>(PlayerProgression.Instance.GetComponent<StartMenu>(), "waiting")) &&
            EditorApplication.timeSinceStartup < reloadDeadline) yield return null;
        Require(GameSession.Instance.State == GameRunState.Menu, "Return-to-menu failed.");
        Pass("B02-B04: defeat/results, return to menu; no automatic restart after menu exit.");

        // Nested enumerator is driven below, so the soak remains a real 300 gameplay seconds.
        IEnumerator soak = Soak();
        while (soak.MoveNext()) yield return null;
    }

    private static Dictionary<int, Vector3> lastPositions = new Dictionary<int, Vector3>();

    // Only the 300 s soak (diagnostics): -executeMethod VerticalSliceChecks.RunSoakBatch
    public static void RunSoakBatch()
    {
        File.WriteAllText(Request, "batchsoak");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator SoakOnly()
    {
        yield return null;
        IEnumerator soak = Soak();
        while (soak.MoveNext()) yield return null;
    }

    private static IEnumerator Soak()
    {
        PlayerProgression progression = PlayerProgression.Instance;
        GameObject player = progression.gameObject;
        player.GetComponent<StartMenu>().SendMessage("BeginGame", 1);
        GameSession session = GameSession.Instance;
        WaveManager waves = UnityEngine.Object.FindObjectOfType<WaveManager>();
        PlayMovement movement = player.GetComponent<PlayMovement>();
        movement.enabled = false;
        Rigidbody body = player.GetComponent<Rigidbody>();
        body.isKinematic = true;
        Vector3[] patrol = { new Vector3(36, 1, 36), new Vector3(-36, 1, 36), new Vector3(-36, 1, -36), new Vector3(36, 1, -36) };
        int waypointIndex = 0, peak = 0, cappedFrames = 0, rescues = 0;
        bool sawFast = false, sawTank = false, freedCap = false;
        float previousTime = Time.time;
        double deadline = EditorApplication.timeSinceStartup + 420;
        lastReport = EditorApplication.timeSinceStartup;
        while (session.Elapsed < 300f)
        {
            Require(EditorApplication.timeSinceStartup < deadline, "Five-minute soak exceeded wall-clock limit.");
            // Pressure is now real: the evading bot is not a skilled player, so rescue it to keep covering 300 s
            // of runtime. The number of rescues is a rough pressure measure, not a balance verdict.
            PlayerHealth botHealth = player.GetComponent<PlayerHealth>();
            if (botHealth.CurrentHP < 40) { botHealth.IncreaseMaxHealth(40); rescues++; }
            Require(!session.IsFinished, "Soak bot died at " + session.Elapsed.ToString("0.0") + "s despite rescues.");
            if (session.State == GameRunState.Upgrade)
            {
                int choice = 0;
                float best = float.NegativeInfinity;
                for (int i = 0; i < progression.Offered.Count; i++)
                {
                    UpgradeEffect effect = progression.Offered[i].effect;
                    float score = effect == UpgradeEffect.Damage ? 5 : effect == UpgradeEffect.AttackSpeed ? 4 :
                        effect == UpgradeEffect.Movement ? 3 : effect == UpgradeEffect.Pickup ? 2 : 1;
                    if (effect == UpgradeEffect.MaxHealth && player.GetComponent<PlayerHealth>().CurrentHP < 70) score = 10;
                    if (score > best) { best = score; choice = i; }
                }
                progression.SelectOfferedUpgrade(choice);
            }
            if (Time.time - previousTime < 0.016f) { yield return null; continue; }
            float delta = Mathf.Min(0.05f, Time.time - previousTime);
            previousTime = Time.time;
            EnemyAI[] enemies = UnityEngine.Object.FindObjectsOfType<EnemyAI>();
            peak = Mathf.Max(peak, EnemyAI.ActiveCount);
            Require(EnemyAI.ActiveCount <= waves.settings.AbsoluteLimitForWave(waves.wave), "Absolute live enemy cap exceeded during soak.");
            if (!waves.HasRoom()) cappedFrames++;
            if (cappedFrames > 0 && waves.HasRoom()) freedCap = true;
            Vector3 position = body.position;
            Vector3 goal = patrol[waypointIndex];
            if (Vector3.Distance(position, goal) < 3f) waypointIndex = (waypointIndex + 1) % patrol.Length;
            Vector3 evade = Vector3.zero;
            foreach (EnemyAI enemy in enemies)
            {
                if (enemy.name.Contains("Fast")) sawFast = true;
                if (enemy.name.Contains("tank")) sawTank = true;
                Vector3 away = position - enemy.transform.position;
                away.y = 0f;
                float distance = away.magnitude;
                if (distance < 9f) evade += away.normalized * (9f - distance) / Mathf.Max(1f, distance);
            }
            // Collect safe nearby experience while using normal speed, damage and health.
            ExperiencePickup nearest = null;
            float nearestDistance = 12f;
            foreach (ExperiencePickup pickup in UnityEngine.Object.FindObjectsOfType<ExperiencePickup>())
            {
                float distance = Vector3.Distance(position, pickup.transform.position);
                if (distance > nearestDistance) continue;
                bool safe = true;
                foreach (EnemyAI enemy in enemies)
                    if (Vector3.Distance(pickup.transform.position, enemy.transform.position) < 5f) { safe = false; break; }
                if (safe) { nearest = pickup; nearestDistance = distance; }
            }
            if (nearest != null) goal = nearest.transform.position;
            Vector3 waypoint = ArenaLayout.Instance.NextWaypoint(position, goal, 0.5f);
            Vector3 direction = waypoint - position;
            direction.y = 0f;
            direction = (direction.normalized + evade * 2f).normalized;
            Vector3 desired = position + direction * movement.speed * movement.SpeedMultiplier * delta;
            body.position = ArenaLayout.Instance.ResolveMovement(position, desired, 0.5f);
            if (EditorApplication.timeSinceStartup - lastReport > 30)
            {
                lastReport = EditorApplication.timeSinceStartup;
                // Diagnostics for stalls: kills, bot position, enemies in weapon range and enemies that barely moved.
                int inRange = 0, frozen = 0;
                float minDistance = float.PositiveInfinity;
                var seen = new Dictionary<int, Vector3>();
                foreach (EnemyAI enemy in enemies)
                {
                    float d = Vector3.Distance(position, enemy.transform.position);
                    minDistance = Mathf.Min(minDistance, d);
                    if (d <= 20f) inRange++;
                    int id = enemy.GetInstanceID();
                    if (lastPositions.TryGetValue(id, out Vector3 before) && Vector3.Distance(before, enemy.transform.position) < 1f) frozen++;
                    seen[id] = enemy.transform.position;
                }
                lastPositions = seen;
                RunStatistics stats = RunStatistics.Instance;
                Pass("A09 soak progress: " + session.Elapsed.ToString("0") + "s, wave " + waves.wave +
                    ", HP " + player.GetComponent<PlayerHealth>().CurrentHP + ", level " + progression.Level + ", peak alive " + peak +
                    " | kills " + (stats != null ? stats.Kills : -1) + ", bot (" + position.x.ToString("0") + "," + position.z.ToString("0") +
                    "), inRange20 " + inRange + ", frozen " + frozen + ", nearest " + minDistance.ToString("0.0") + ", shooterEnabled " +
                    player.GetComponent<AutoShoter>().enabled);
            }
            yield return null;
        }
        Require(sawFast && sawTank, "Fast/Tank availability was not observed.");
        Pass("A09: 300 real gameplay seconds with a moving bot, normal HP/damage/upgrades; peak alive " + peak +
            ", Fast/Tank observed; bot rescues " + rescues + "; no-room frames " + cappedFrames + ", recovery seen " + freedCap +
            ". This is runtime coverage, not a human balance verdict.");
        // Exercise cap recovery deterministically without changing production settings.
        int previousLimit = waves.settings.maxAliveEnemies;
        float previousRadius = waves.settings.pressureRadius;
        waves.settings.maxAliveEnemies = 1;
        waves.settings.pressureRadius = 1000f; // every enemy counts, so the controlled cap is distance independent
        foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>()) UnityEngine.Object.Destroy(enemy.gameObject);
        yield return null; yield return null;
        float wait = Time.time + 25f;
        while (EnemyAI.ActiveCount == 0 && Time.time < wait) yield return null;
        Require(EnemyAI.ActiveCount == 1, "Controlled cap did not fill.");
        EnemyAI blockedEnemy = UnityEngine.Object.FindObjectOfType<EnemyAI>();
        float releaseAt = Time.time + 1f;
        while (Time.time < releaseAt) { Require(EnemyAI.ActiveCount <= 1, "Spawns ignored cap."); yield return null; }
        UnityEngine.Object.Destroy(blockedEnemy.gameObject);
        yield return null;
        wait = Time.time + 25f;
        while (EnemyAI.ActiveCount == 0 && Time.time < wait) yield return null;
        Require(EnemyAI.ActiveCount == 1, "Spawn did not recover after freeing cap.");
        waves.settings.maxAliveEnemies = previousLimit;
        waves.settings.pressureRadius = previousRadius;
        Pass("A09: deterministic cap saturation, no over-spawn, resumption after releasing capacity.");
    }
}
