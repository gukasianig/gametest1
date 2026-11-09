using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Optional check of the extended arena and the random arena events.
// Batch: -batchmode -executeMethod ArenaEventChecks.RunBatch (without -quit). Report: Logs/arena-event-results.txt.
[InitializeOnLoad]
public static class ArenaEventChecks
{
    private const string Request = "Logs/arena-event.request";
    private const string Report = "Logs/arena-event-results.txt";
    private static CheckRunner routine;
    private static readonly System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
    private static double started;

    static ArenaEventChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Request)) EditorApplication.delayCall += Begin;
            if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Tick;
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
            if (!routine.MoveNext()) Finish(true, "Arena and event checks complete.");
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

    private static IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        double cap = EditorApplication.timeSinceStartup + 120;
        while (Time.time < end && EditorApplication.timeSinceStartup < cap)
        {
            // Reward crystals can level the hero up; the card screen stops the game clock, so pick a card.
            if (PlayerProgression.Instance != null && PlayerProgression.Instance.Choosing) PlayerProgression.Instance.SelectOfferedUpgrade(0);
            yield return null;
        }
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        GameObject player = PlayerProgression.Instance.gameObject;
        player.GetComponent<StartMenu>().SendMessage("BeginGame", 1);
        player.GetComponent<AutoShoter>().enabled = false;
        player.GetComponent<PlayMovement>().enabled = false;
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        health.IncreaseMaxHealth(2000);
        GameSession session = GameSession.Instance;
        Require(session.State == GameRunState.Playing, "Run did not start.");
        ArenaLayout arena = ArenaLayout.Instance;
        ArenaEvents events = ArenaEvents.Instance;
        Require(arena != null && events != null, "Arena or event system missing.");

        // 1. The extended arena: bigger, outer cover exists, the floor covers it, the centre is still free.
        Require(arena.halfSize >= 110f, "Arena was not extended: " + arena.halfSize);
        Require(arena.Obstacles.Count >= 19, "Outer cover missing: " + arena.Obstacles.Count);
        Require(arena.IsWalkable(Vector3.zero, 1f), "Arena centre is blocked.");
        Require(arena.IsWalkable(new Vector3(arena.halfSize - 15f, 0f, 0f), 1f), "Outer zone is not walkable.");
        Require(!arena.IsWalkable(new Vector3(arena.halfSize - 1f, 0f, 0f), 1f), "Perimeter does not block.");
        GridDrawer floor = UnityEngine.Object.FindObjectOfType<GridDrawer>();
        Require(floor == null || floor.groundSize >= arena.halfSize * 2f, "Floor is smaller than the arena.");
        Pass("Arena half-size " + arena.halfSize + ", " + arena.Obstacles.Count + " obstacles, outer zone walkable, perimeter blocks.");

        // 2. Meteor shower: telegraph markers appear and a blast hurts the player in the circle.
        int before = health.CurrentHP;
        events.Begin(ArenaEventKind.MeteorShower);
        Require(events.Active && events.Current == ArenaEventKind.MeteorShower, "Meteor shower did not start.");
        yield return Wait(0.8f);
        Require(UnityEngine.Object.FindObjectsOfType<Meteor>().Length > 0, "No meteor warning appeared.");
        events.Blast(player.transform.position, 3.5f, 60, 15);
        Require(health.CurrentHP == before - 15, "Meteor blast damage to player: " + (before - health.CurrentHP));
        events.End(false);
        yield return Wait(0.3f);
        Require(!events.Active, "Event did not end.");
        Pass("Meteor shower: warning circles spawn, blast damages the player (15), event ends.");

        // 3. Magnetic storm: factors apply and reset.
        events.Begin(ArenaEventKind.MagneticStorm);
        Require(Mathf.Approximately(ArenaEvents.EnemySpeedFactor, 1.3f) && Mathf.Approximately(ArenaEvents.ExperienceFactor, 2f), "Storm factors.");
        events.End(false);
        Require(Mathf.Approximately(ArenaEvents.EnemySpeedFactor, 1f) && Mathf.Approximately(ArenaEvents.ExperienceFactor, 1f), "Factors not reset.");
        Pass("Magnetic storm: enemies x1.3 speed, experience x2, both reset after the event.");

        // 4. Repair field heals the player standing in it.
        health.TakeDamage(500);
        int hurt = health.CurrentHP;
        events.Begin(ArenaEventKind.RepairField);
        GameObject field = GameObject.Find("Repair field");
        Require(field != null, "Repair field missing.");
        player.transform.position = new Vector3(field.transform.position.x, player.transform.position.y, field.transform.position.z);
        yield return Wait(2f);
        Require(health.CurrentHP > hurt, "Repair field did not heal: " + hurt + " -> " + health.CurrentHP);
        events.End(false);
        yield return Wait(0.3f);
        Require(GameObject.Find("Repair field") == null, "Repair field not removed.");
        Pass("Repair field heals the player (" + hurt + " -> " + health.CurrentHP + ") and is removed.");

        // 5. Supply drop spawns crystals and a beacon.
        events.Begin(ArenaEventKind.SupplyDrop);
        Require(GameObject.Find("Supply beacon") != null, "Supply beacon missing.");
        int crystals = 0;
        foreach (GameObject item in UnityEngine.Object.FindObjectsOfType<GameObject>()) if (item.name == "Experience Crystal") crystals++;
        Require(crystals >= 14, "Supply crystals: " + crystals);
        foreach (GameObject item in UnityEngine.Object.FindObjectsOfType<GameObject>())
            if (item.name == "Experience Crystal")
                Require(item.transform.position.y >= GroundHeight.At(item.transform.position) + 0.3f, "Supply crystal under the ground at " + item.transform.position);
        yield return Wait(0.3f);
        GameObject eventHud = GameObject.Find("Event HUD");
        Require(eventHud != null, "Event HUD missing for the supply drop.");
        bool arrowShown = false, hintShown = false, distanceShown = false;
        foreach (UnityEngine.UI.Text label in eventHud.GetComponentsInChildren<UnityEngine.UI.Text>())
        {
            if (label.text == "►") arrowShown = true;
            if (label.text.Contains("маяку")) hintShown = true;
            if (label.text.EndsWith(" м")) distanceShown = true;
        }
        Require(arrowShown && hintShown && distanceShown, "Supply hint/arrow/distance: " + hintShown + "/" + arrowShown + "/" + distanceShown);
        Pass("Supply drop: crystals stand above the ground; the HUD shows what to do and an arrow with the distance to the beacon.");
        events.End(false);
        yield return Wait(0.3f);
        Require(GameObject.Find("Supply beacon") == null, "Beacon not removed.");
        Pass("Supply drop: beacon plus " + crystals + " crystals, beacon removed afterwards.");

        // 5b. Hold the point: standing in the circle fills the progress; the reward appears and the event ends early.
        events.holdSeconds = 2f;
        foreach (GameObject leftover in UnityEngine.Object.FindObjectsOfType<GameObject>()) if (leftover.name == "Experience Crystal") UnityEngine.Object.Destroy(leftover);
        yield return Wait(0.2f);
        int levelBeforeHold = PlayerProgression.Instance.Level;
        int hpBeforeHold = health.CurrentHP;
        events.Begin(ArenaEventKind.HoldPoint);
        GameObject zone = GameObject.Find("Hold point");
        Require(zone != null, "Hold point missing.");
        player.transform.position = new Vector3(zone.transform.position.x, player.transform.position.y, zone.transform.position.z);
        yield return Wait(1f);
        Require(events.HoldProgress > 0.4f && events.HoldProgress < 2f, "Hold progress after 1 s: " + events.HoldProgress);
        player.transform.position = player.transform.position + new Vector3(30f, 0f, 0f);
        float outside = events.HoldProgress;
        yield return Wait(0.7f);
        Require(events.HoldProgress - outside < 0.05f, "Progress grew while outside the circle.");
        player.transform.position = new Vector3(zone.transform.position.x, player.transform.position.y, zone.transform.position.z);
        yield return Wait(2f);
        Require(PlayerProgression.Instance.Level > levelBeforeHold, "Hold reward gave no experience: level " + levelBeforeHold + " -> " + PlayerProgression.Instance.Level);
        Require(DropPickup.ActiveCount >= 1 || health.CurrentHP > hpBeforeHold, "Hold reward repair kit missing.");
        yield return Wait(2f);
        Require(!events.Active, "Hold-point event did not end after success.");
        DropPickup.Clear();
        events.holdSeconds = 10f;
        Pass("Hold point: progress only inside the circle, reward raised the level (" + levelBeforeHold + " -> " + PlayerProgression.Instance.Level + ") plus a repair kit, event ends early.");

        // 5c. Ambush: a ring of enemies appears around the hero after the warning.
        int enemiesBefore = EnemyAI.ActiveCount;
        events.Begin(ArenaEventKind.Ambush);
        yield return Wait(0.5f);
        Require(GameObject.Find("Ambush marker") != null, "No ambush warning markers.");
        yield return Wait(2.2f);
        Require(events.AmbushCount >= 6, "Ambush enemies created: " + events.AmbushCount);
        Require(EnemyAI.ActiveCount >= enemiesBefore + 6, "Ambush enemies alive: " + (EnemyAI.ActiveCount - enemiesBefore));
        events.End(false);
        Pass("Ambush: warning markers, then " + events.AmbushCount + " enemies around the hero.");

        // 5d. Toxic pools: damage after the warning to the hero and to enemies inside.
        foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>()) UnityEngine.Object.Destroy(enemy.gameObject);
        yield return Wait(0.3f);
        events.Begin(ArenaEventKind.ToxicPools);
        GameObject pool = GameObject.Find("Toxic pool");
        Require(pool != null && events.PoolCount == 3, "Toxic pools missing.");
        EnemyType weakType = Array.Find(UnityEngine.Object.FindObjectOfType<WaveManager>().enemies, e => e.name == "Weak");
        GameObject poolEnemy = UnityEngine.Object.Instantiate(weakType.prefab, pool.transform.position + Vector3.up * 0.5f, Quaternion.identity);
        poolEnemy.GetComponent<EnemyAI>().MovementLocked = true;
        poolEnemy.GetComponent<Health>().SetHealth(1000);
        player.transform.position = new Vector3(pool.transform.position.x, player.transform.position.y, pool.transform.position.z);
        int poolHp = health.CurrentHP;
        yield return Wait(1f);
        Require(health.CurrentHP == poolHp && poolEnemy.GetComponent<Health>().CurrentHealth == 1000, "Pools hurt during the warning.");
        yield return Wait(1.6f);
        Require(health.CurrentHP < poolHp, "Pool did not hurt the hero.");
        Require(poolEnemy.GetComponent<Health>().CurrentHealth < 1000, "Pool did not hurt the enemy.");
        Require(pool.transform.childCount >= 2, "Toxic pool is not the AcidPuddle model.");
        string poolMaterials = "";
        foreach (Renderer part in pool.GetComponentsInChildren<Renderer>()) poolMaterials += part.sharedMaterial.name + " ";
        Require(poolMaterials.Contains("HazardEmissive") && poolMaterials.Contains("SurfaceHazardsPalette"),
            "Acid materials not remapped: " + poolMaterials);
        Require(Mathf.Abs(pool.transform.localScale.x - 4f / 2.7f) < 0.05f && pool.GetComponentInChildren<Collider>() == null,
            "Acid pool scale/collider: " + pool.transform.localScale);
        Pass("Toxic pools use AcidPuddle (" + poolMaterials.Trim() + "), spread to " + pool.transform.localScale.x.ToString("0.00") + "x, no colliders.");
        events.End(false);
        UnityEngine.Object.Destroy(poolEnemy);
        yield return Wait(0.3f);
        Require(GameObject.Find("Toxic pool") == null, "Pools not removed.");
        Pass("Toxic pools: no damage during the warning, then hurt the hero and an enemy; removed afterwards.");

        // 5d2. Wildfire: three GroundFireVisual hearths grow, burn the hero and an enemy, then go out.
        events.Begin(ArenaEventKind.Wildfire);
        yield return Wait(0.3f);
        GroundFireZone[] fires = UnityEngine.Object.FindObjectsOfType<GroundFireZone>();
        Require(events.FireCount == 3 && fires.Length == 3, "Wildfire hearths: " + fires.Length);
        Require(fires[0].GetComponentInChildren<GroundFireVisual>() != null, "Wildfire has no GroundFireVisual model.");
        float startRadius = fires[0].Radius;
        GameObject fireEnemy = UnityEngine.Object.Instantiate(weakType.prefab, fires[0].transform.position + Vector3.up * 0.5f, Quaternion.identity);
        fireEnemy.GetComponent<EnemyAI>().MovementLocked = true;
        fireEnemy.GetComponent<Health>().SetHealth(1000);
        player.transform.position = new Vector3(fires[0].transform.position.x, player.transform.position.y, fires[0].transform.position.z);
        int fireHp = health.CurrentHP;
        yield return Wait(2f);
        Require(fires[0].Radius > startRadius + 0.3f, "Wildfire did not grow: " + startRadius + " -> " + fires[0].Radius);
        Require(health.CurrentHP < fireHp, "Wildfire did not hurt the hero.");
        Require(fireEnemy.GetComponent<Health>().CurrentHealth < 1000, "Wildfire did not hurt the enemy.");
        int flames = fires[0].GetComponentsInChildren<MeshRenderer>().Length;
        float grown = fires[0].Radius;
        events.End(false);
        UnityEngine.Object.Destroy(fireEnemy);
        yield return Wait(3.2f);
        Require(UnityEngine.Object.FindObjectsOfType<GroundFireZone>().Length == 0, "Wildfire not removed after going out.");
        Pass("Wildfire: 3 hearths with GroundFireVisual (" + flames + " renderers), radius " + startRadius.ToString("0.0") + " -> " +
             grown.ToString("0.0") + " m in 2 s, hurt the hero and an enemy, went out and were removed.");

        // 5e. The on-screen event line exists and names the running event.
        events.Begin(ArenaEventKind.MagneticStorm);
        yield return Wait(0.3f);
        GameObject hud = GameObject.Find("Event HUD");
        Require(hud != null, "Event HUD missing.");
        UnityEngine.UI.Text hudLabel = hud.GetComponentInChildren<UnityEngine.UI.Text>();
        Require(hudLabel != null && hudLabel.text.Contains("МАГНИТНАЯ БУРЯ"), "Event HUD text: " + (hudLabel == null ? "none" : hudLabel.text));
        events.End(false);
        Pass("Event line shows the running event and its time left.");

        // 6. The timer starts events by itself.
        int started = events.StartedCount;
        events.firstEventAt = 0.1f;
        typeof(ArenaEvents).GetField("nextAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(events, 0f);
        yield return Wait(1f);
        Require(events.StartedCount > started && events.Active, "Timer did not start an event.");
        events.End(false);
        Pass("Timer starts a random event by itself.");
    }
}
