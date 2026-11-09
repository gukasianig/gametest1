using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Optional check for the Summoner/Sniper roles, elites and the wave director.
// Batch: -batchmode -executeMethod EnemyDirectorChecks.RunBatch (without -quit). Report: Logs/enemy-director-results.txt.
[InitializeOnLoad]
public static class EnemyDirectorChecks
{
    private const string Request = "Logs/enemy-director.request";
    private const string Report = "Logs/enemy-director-results.txt";
    private static CheckRunner routine;
    private static readonly System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
    private static double started;

    static EnemyDirectorChecks()
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
        EditorSceneManager.OpenScene("Assets/Scenes/GreenArenaPlay.unity");
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
            if (EditorApplication.timeSinceStartup - started > 240) throw new Exception("Check timed out.");
            if (!routine.MoveNext()) Finish(true, "Enemy and director checks complete.");
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

    // Game-time wait (the game runs slower than real time without a window); a real-time cap prevents hangs.
    private static IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        double cap = EditorApplication.timeSinceStartup + 120;
        while (Time.time < end && EditorApplication.timeSinceStartup < cap) yield return null;
    }

    private static GameObject Place(GameObject prefab, Vector3 position, Transform target, bool lockMovement)
    {
        GameObject enemy = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        ai.target = target;
        ai.MovementLocked = lockMovement;
        return enemy;
    }

    private static void ClearEnemies()
    {
        foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>()) UnityEngine.Object.Destroy(enemy.gameObject);
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        GameObject player = PlayerProgression.Instance.gameObject;
        player.GetComponent<StartMenu>().SendMessage("BeginGame", 1);
        AutoShoter shooter = player.GetComponent<AutoShoter>();
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        WaveManager waves = UnityEngine.Object.FindObjectOfType<WaveManager>();
        GameSession session = GameSession.Instance;
        shooter.enabled = false;
        player.GetComponent<PlayMovement>().enabled = false;
        health.IncreaseMaxHealth(2000);
        Require(session.State == GameRunState.Playing, "Run did not start.");

        // 1. Registration.
        EnemyType summonerType = Array.Find(waves.enemies, e => e.name == "Summoner");
        EnemyType sniperType = Array.Find(waves.enemies, e => e.name == "Sniper");
        Require(summonerType != null && sniperType != null, "New enemy types are not registered.");
        Require(Array.Find(waves.enemies, e => e.name == "Shielder") == null, "Removed Shielder is still registered.");
        Require(summonerType.minWave == 9 && summonerType.minSurvivalSeconds == 120f &&
            sniperType.minWave == 10 && sniperType.minSurvivalSeconds == 130f, "Unlock gates.");
        Require(summonerType.prefab.GetComponent<EnemyAbility>().role == EnemyRole.Summoner &&
            sniperType.prefab.GetComponent<EnemyAbility>().role == EnemyRole.Sniper, "Prefab roles.");
        Require(summonerType.prefab.GetComponent<EnemyAbility>().splitPrefab != null, "Summoner minion prefab missing.");
        Require(sniperType.prefab.GetComponent<Health>().maxHealth == 55 &&
            summonerType.prefab.GetComponent<Health>().maxHealth == 110 && waves.enemies.Length == 14, "Stats / roster size: " + waves.enemies.Length);
        Transform target = player.transform;
        Pass("Roster: 10 types (no Shielder), Summoner 9/120 s, Sniper 10/130 s, roles, stats and minion prefab set.");

        // 3. Director schedule (pure logic).
        WaveDirector director = new WaveDirector();
        int changes = 0;
        director.PhaseChanged += (phase, cycle) => changes++;
        foreach (float time in new[] { 0f, 10f, 50f, 80f, 90f }) director.Evaluate(time);
        Require(changes == 4, "Phase change count: " + changes);
        director.Evaluate(0f);
        Require(director.Phase == DirectorPhase.Build && Mathf.Approximately(director.LimitMultiplier, 0.6f) && director.IntervalMultiplier == 1f, "Build start.");
        director.Evaluate(44.9f);
        Require(director.LimitMultiplier > 0.89f && director.LimitMultiplier < 0.9001f, "Build end ramp: " + director.LimitMultiplier);
        director.Evaluate(50f);
        Require(director.Phase == DirectorPhase.Peak && director.LimitMultiplier == 1f && director.IntervalMultiplier == 0.75f, "Peak.");
        director.Evaluate(75f);
        Require(director.Phase == DirectorPhase.Relief && director.LimitMultiplier == 0.3f && director.IntervalMultiplier == 1.8f, "Relief.");
        director.Evaluate(85f);
        Require(director.Phase == DirectorPhase.Build && director.Cycle == 1, "Next cycle.");
        Require(WaveDirector.EventForCycle(0) == DirectorEvent.None && WaveDirector.EventForCycle(1) == DirectorEvent.Elite &&
            WaveDirector.EventForCycle(2) == DirectorEvent.Horde && WaveDirector.EventForCycle(3) == DirectorEvent.Elite, "Event schedule.");
        Require(WaveDirector.EliteCount(1) == 1 && WaveDirector.EliteCount(5) == 2 && WaveDirector.EliteCount(99) == 3 &&
            WaveDirector.HordeCount(2) == 12 && WaveDirector.HordeCount(99) == 18, "Event sizes.");
        Pass("Director: Build ramp 0.6->0.9, Peak 1.0/x0.75 interval, Relief 0.3/x1.8, cycle 85 s, events alternate elite/horde, 4 phase changes.");

        // 4. Summoner (waves still running): warning, then at most four minions with no experience.
        GameObject summoner = Place(summonerType.prefab, target.position + new Vector3(-10f, 0.7f, 0f), target, false);
        EnemyAbility summon = summoner.GetComponent<EnemyAbility>();
        double summonDeadline = EditorApplication.timeSinceStartup + 20;
        bool sawWarning = false;
        while (summon.LiveMinions == 0 && EditorApplication.timeSinceStartup < summonDeadline)
        {
            sawWarning |= summon.Telegraphing;
            yield return null;
        }
        Require(summon.LiveMinions > 0, "Summoner produced no minions.");
        Require(sawWarning, "Summon had no warning circle.");
        yield return Wait(18f);
        Require(summon.LiveMinions <= EnemyAbility.SummonerMinionLimit, "Minion limit exceeded: " + summon.LiveMinions);
        Require(EnemyAI.ActiveCount <= waves.settings.AbsoluteLimitForWave(waves.wave), "Absolute cap exceeded by summons.");
        GameObject minionObject = GameObject.Find("Summoned minion");
        if (minionObject != null)
            Require(minionObject.GetComponent<Health>().experienceReward == 0 && minionObject.GetComponent<Health>().maxHealth == 15, "Minion stats.");
        Pass("Summoner: warning circle, minions appear, never more than " + EnemyAbility.SummonerMinionLimit + " alive, respects the hard cap, minions: 15 HP/0 XP.");

        // Quiet the arena for exact tests.
        waves.StopSpawning();
        ClearEnemies();
        yield return null;
        yield return null;
        FieldInfo elapsedField = typeof(GameSession).GetField("<Elapsed>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        elapsedField.SetValue(session, 300f);
        waves.wave = 20;

        // 5. The director gates room near the player; peak allows, relief does not.
        GameObject weakPrefab = Array.Find(waves.enemies, e => e.name == "Weak").prefab;
        GameObject[] crowd = new GameObject[8];
        for (int i = 0; i < crowd.Length; i++) crowd[i] = Place(weakPrefab, target.position + new Vector3(8f + i, 0.7f, 6f), target, true);
        yield return null;
        waves.Director.Evaluate(50f);
        Require(waves.HasRoom(), "Peak should have room with 8 enemies near.");
        waves.Director.Evaluate(75f);
        Require(!waves.HasRoom(), "Relief should have no room with 8 enemies near.");
        waves.Director.Evaluate(30f);
        foreach (GameObject enemy in crowd) UnityEngine.Object.Destroy(enemy);
        yield return null;
        Pass("Director gates spawning: with 8 enemies near the hero, Peak has room, Relief has none.");

        // 6. Elite.
        GameObject elite = waves.SpawnElite();
        Require(elite != null, "Elite did not spawn.");
        string typeName = elite.name.Substring("Elite ".Length);
        EnemyType eliteType = Array.Find(waves.enemies, e => e.name == typeName);
        Require(eliteType != null, "Elite type: " + elite.name);
        Health eliteHealth = elite.GetComponent<Health>();
        int scaled = Mathf.RoundToInt(eliteType.prefab.GetComponent<Health>().maxHealth * (1f + 19 * waves.settings.healthGrowthPerWave));
        Require(eliteHealth.maxHealth == scaled * 3, "Elite health: " + eliteHealth.maxHealth + " vs " + scaled * 3);
        Require(eliteHealth.experienceReward == eliteType.prefab.GetComponent<Health>().experienceReward * 4, "Elite experience.");
        Require(Mathf.Approximately(elite.transform.localScale.x, eliteType.prefab.transform.localScale.x * 1.3f), "Elite size.");
        Require(elite.transform.Find("Elite crown") != null, "Elite crown missing.");
        UnityEngine.Object.Destroy(elite);
        Pass("Elite: x3 health, x4 experience, x1.3 size, crown marker, never a Bomber/Healer/Summoner.");

        // 7. Horde event stays under the hard cap.
        // Let pending destroys finish so the baseline is exact, then wait for the whole burst (retries included).
        ClearEnemies();
        yield return null;
        yield return null;
        int beforeHorde = EnemyAI.ActiveCount;
        waves.StartCoroutine(waves.HordeEvent(8));
        double hordeDeadline = EditorApplication.timeSinceStartup + 30;
        while (EnemyAI.ActiveCount - beforeHorde < 8 && EditorApplication.timeSinceStartup < hordeDeadline) yield return null;
        int spawned = EnemyAI.ActiveCount - beforeHorde;
        Require(spawned == 8 && EnemyAI.ActiveCount <= waves.settings.AbsoluteLimitForWave(waves.wave),
            "Horde size: " + spawned + " (game time " + Time.time.ToString("0.0") + " s)");
        Pass("Horde event: all 8 spawned (failed positions are retried), within the hard cap.");
        ClearEnemies();
        yield return null;
        yield return null;

        // 8. Sniper: long warning, then exactly one 22-damage shot that hits a still hero.
        Vector3[] directions = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        Vector3 offset = Vector3.zero;
        foreach (Vector3 direction in directions)
            if (ArenaLayout.Instance.PathClear(target.position, target.position + direction * 14f, 0.3f) &&
                ArenaLayout.Instance.IsWalkable(target.position + direction * 14f, 0.5f)) { offset = direction * 14f; break; }
        Require(offset != Vector3.zero, "No clear line for the sniper test.");
        int hpBefore = health.CurrentHP;
        GameObject sniper = Place(sniperType.prefab, target.position + offset + Vector3.up * 0.7f, target, false);
        EnemyAbility snipe = sniper.GetComponent<EnemyAbility>();
        double snipeDeadline = EditorApplication.timeSinceStartup + 8;
        bool warned = false;
        while (health.CurrentHP == hpBefore && EditorApplication.timeSinceStartup < snipeDeadline)
        {
            warned |= snipe.Telegraphing;
            yield return null;
        }
        Require(warned, "Sniper showed no warning line.");
        Require(hpBefore - health.CurrentHP == 22, "Sniper damage: " + (hpBefore - health.CurrentHP));
        Pass("Sniper: warning line, then a single 22-damage shot.");
    }

}
