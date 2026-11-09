using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Optional check of the nine special weapons: each one is selected, enemies are placed, and the automatic
// shooting is observed. Batch: -batchmode -executeMethod WeaponChecks.RunBatch (without -quit).
// Report: Logs/weapon-results.txt. Numbers are first guesses; this proves behaviour, not balance.
[InitializeOnLoad]
public static class WeaponChecks
{
    public static bool CheckModules;
    private const string Request = "Logs/weapon-checks.request";
    private const string Report = "Logs/weapon-results.txt";
    private static CheckRunner routine;
    private static readonly System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
    private static double started;

    static WeaponChecks()
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
            if (EditorApplication.timeSinceStartup - started > 420) throw new Exception("Check timed out.");
            if (!routine.MoveNext()) Finish(true, "Weapon checks complete.");
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

    // Waits are in game time: the game runs slower than real time without a window. A real-time cap prevents hangs.
    private static IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        double cap = EditorApplication.timeSinceStartup + 90;
        while (Time.time < end && EditorApplication.timeSinceStartup < cap) yield return null;
    }

    private static GameObject weakPrefab;
    private static Transform hero;

    // A tough, stationary-by-default enemy so damage can be measured exactly.
    private static Health Dummy(Vector3 offset, bool locked = true)
    {
        GameObject enemy = UnityEngine.Object.Instantiate(weakPrefab, hero.position + offset + Vector3.up * 0.7f, Quaternion.identity);
        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        ai.target = hero;
        ai.MovementLocked = locked;
        Health health = enemy.GetComponent<Health>();
        health.SetHealth(100000);
        return health;
    }

    private static void ClearArena()
    {
        foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>()) UnityEngine.Object.Destroy(enemy.gameObject);
    }

    private static int Lost(Health health) { return health.maxHealth - health.CurrentHealth; }

    // Compact state dump added to failure messages.
    private static string Describe(AutoShoter shooter)
    {
        SpecialWeapons special = shooter.GetComponent<SpecialWeapons>();
        Transform picked = shooter.weapons != null && shooter.GetComponent<SpecialWeapons>() != null && special.Weapon != null ?
            WeaponTargets.Pick(shooter.transform.position, shooter.range, special.Weapon.targeting) : null;
        return " [enabled=" + shooter.enabled + ", kind=" + (special != null && special.Weapon != null ? special.Weapon.kind.ToString() : "none") +
            ", timeScale=" + Time.timeScale + ", state=" + GameSession.Instance.State + ", enemies=" + EnemyAI.ActiveCount +
            ", range=" + shooter.range + ", fireInterval=" + shooter.CurrentFireInterval + ", damage=" + shooter.CurrentDamage +
            ", hero=" + shooter.transform.position + ", picked=" + (picked != null ? picked.position.ToString() : "null") +
            ", running=" + SpecialWeapons.Running + ", time=" + Time.time.ToString("0.0") + ", frames=" + Time.frameCount + "]";
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        GameObject player = PlayerProgression.Instance.gameObject;
        StartMenu menu = player.GetComponent<StartMenu>();
        AutoShoter shooter = player.GetComponent<AutoShoter>();
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        WaveManager waves = UnityEngine.Object.FindObjectOfType<WaveManager>();
        hero = player.transform;
        weakPrefab = Array.Find(waves.enemies, e => e.name == "Weak").prefab;

        // Roster and menu pages.
        Require(shooter.weapons.Length == 13, "Weapon count: " + shooter.weapons.Length);
        WeaponKind[] expected = { WeaponKind.Standard, WeaponKind.Standard, WeaponKind.Standard, WeaponKind.Standard, WeaponKind.Boomerang,
            WeaponKind.Mines, WeaponKind.Rocket, WeaponKind.Arc, WeaponKind.Flame, WeaponKind.Ice, WeaponKind.Rail, WeaponKind.Turret, WeaponKind.Drone };
        for (int i = 0; i < expected.Length; i++) Require(shooter.weapons[i].kind == expected[i], "Weapon kind at " + i);
        Require(shooter.weapons[10].targeting == TargetRule.Strongest && shooter.weapons[6].targeting == TargetRule.Densest, "Target rules.");
        for (int i = 4; i < 13; i++) Require(!MetaProgress.IsWeaponUnlocked(i) && MetaProgress.FindUnlock("weapon:" + i) != null, "Unlock entry for " + i);
        menu.ShowWeapons();
        WeaponSelectScreen screen = menu.WeaponScreen;
        Require(screen != null && screen.Selected == 0, "Weapon screen missing or wrong first selection.");
        int shown = 0;
        for (int i = 0; i < 13; i++)
        {
            if (!shooter.weapons[i].IsSelectable) { Require(GameObject.Find("Weapon Card " + i) == null, "Support weapon " + i + " must not be a tile."); continue; }
            Require(Card("Weapon Card " + i).activeInHierarchy, "Tile " + i + " not on the single screen.");
            shown++;
        }
        Require(shown == 10, "Selectable weapons: " + shown);
        MetaProgress.Data.currency = 100000;
        Card("Weapon Card 6").GetComponent<Button>().onClick.Invoke();
        Require(screen.Selected == 6 && !MetaProgress.IsWeaponUnlocked(6), "First click must only select weapon 6.");
        Card("Weapon Card 6").GetComponent<Button>().onClick.Invoke();
        Require(MetaProgress.IsWeaponUnlocked(6) && MetaProgress.Currency == 100000 - 400, "Second click must buy weapon 6.");
        Pass("Roster: 13 weapons with the right kinds/targeting, 10 tiles on one screen (drones, turret and mines are combat upgrades only), first click selects, second buys a locked weapon.");

        // Start a run, then switch weapons directly; grenades are off so only the weapon under test deals damage.
        menu.SendMessage("BeginGame", 1);
        Require(GameSession.Instance.State == GameRunState.Playing, "Run did not start.");
        waves.StopSpawning();
        shooter.grenadePrefab = null;
        health.IncreaseMaxHealth(5000);
        player.GetComponent<PlayMovement>().enabled = false;
        ClearArena();
        yield return null;
        yield return null;

        // Rail: one shot pierces a whole line; the beam reaches the far enemy.
        Select(shooter, 10);
        Health r1 = Dummy(new Vector3(5f, 0f, 0f)), r2 = Dummy(new Vector3(10f, 0f, 0f)), r3 = Dummy(new Vector3(15f, 0f, 0f));
        yield return WaitUntilLost(() => Lost(r1) >= 60 && Lost(r2) >= 60 && Lost(r3) >= 60, 6f);
        Require(Lost(r1) >= 60 && Lost(r2) >= 60 && Lost(r3) >= 60, "Rail did not pierce all three: " + Lost(r1) + "/" + Lost(r2) + "/" + Lost(r3) + Describe(shooter));
        ModuleReached(r1); ModuleReached(r2); ModuleReached(r3);
        Pass("Rail: one beam damaged enemies at 5, 10 and 15 m in a line.");
        ClearArena(); yield return null;

        // Boomerang: damages on the way out and back (two hits per throw for an enemy in the path).
        Select(shooter, 4);
        Health b1 = Dummy(new Vector3(8f, 0f, 0f));
        yield return WaitUntilLost(() => Lost(b1) >= 44, 8f);
        Require(Lost(b1) >= 44 && Lost(b1) % 22 == 0, "Boomerang damage: " + Lost(b1));
        ModuleReached(b1);
        Pass("Boomerang: the saw hit an enemy in its path on both legs (" + Lost(b1) + " damage).");
        ClearArena(); yield return null;

        // Mines: dropped under the hero; an approaching enemy sets them off.
        Select(shooter, 5);
        Health m1 = Dummy(new Vector3(9f, 0f, 0f), false);
        m1.GetComponent<EnemyAI>().speed = 4f;
        yield return WaitUntilLost(() => Lost(m1) >= 40, 12f);
        Require(Lost(m1) >= 40, "Mine did not explode on the approaching enemy: " + Lost(m1));
        ModuleReached(m1);
        Pass("Mines: an enemy walking to the hero detonated mines for " + Lost(m1) + " damage.");
        ClearArena(); yield return null;

        // Rocket: targets the densest cluster, splash hits neighbours, the lone near enemy is ignored.
        Select(shooter, 6);
        Health lone = Dummy(new Vector3(0f, 0f, -5f));
        Health c1 = Dummy(new Vector3(12f, 0f, 0f)), c2 = Dummy(new Vector3(13.5f, 0f, 0.8f)), c3 = Dummy(new Vector3(12f, 0f, 1.8f));
        yield return WaitUntilLost(() => Lost(c1) > 0 && Lost(c2) > 0 && Lost(c3) > 0, 8f);
        Require(Lost(c1) > 0 && Lost(c2) > 0 && Lost(c3) > 0, "Rocket splash missed the cluster: " + Lost(c1) + "/" + Lost(c2) + "/" + Lost(c3));
        Require(Lost(lone) == 0, "Rocket aimed at the lone enemy instead of the cluster.");
        ModuleReached(c1); ModuleReached(c2); ModuleReached(c3);
        Pass("Rocket: aimed at the dense cluster, all three damaged by one blast, the nearer lone enemy untouched.");
        ClearArena(); yield return null;

        // Arc: chain damage falls off along the chain.
        Select(shooter, 7);
        Health[] chain = { Dummy(new Vector3(6f, 0f, 0f)), Dummy(new Vector3(10f, 0f, 0f)), Dummy(new Vector3(14f, 0f, 0f)), Dummy(new Vector3(18f, 0f, 0f)) };
        yield return WaitUntilLost(() => Lost(chain[3]) > 0, 6f);
        Require(Lost(chain[0]) > Lost(chain[1]) && Lost(chain[1]) > Lost(chain[2]) && Lost(chain[2]) >= Lost(chain[3]) && Lost(chain[3]) > 0,
            "Arc falloff: " + Lost(chain[0]) + "/" + Lost(chain[1]) + "/" + Lost(chain[2]) + "/" + Lost(chain[3]));
        Pass("Arc: lightning reached all four enemies with decreasing damage (" + Lost(chain[0]) + ">" + Lost(chain[1]) + ">" + Lost(chain[2]) + ">=" + Lost(chain[3]) + ").");
        foreach (Health part in chain) ModuleReached(part);
        ClearArena(); yield return null;

        // Flame: inside the cone burns, behind and to the side does not.
        Select(shooter, 8);
        Health front = Dummy(new Vector3(4f, 0f, 0f)), behind = Dummy(new Vector3(-5.5f, 0f, 0f)), side = Dummy(new Vector3(0f, 0f, 5.5f));
        yield return WaitUntilLost(() => Lost(front) >= 12, 6f);
        Require(Lost(front) >= 12, "Flame did not damage the enemy in front: " + Lost(front));
        Require(front.GetComponent<BurnEffect>() != null, "Burn effect missing.");
        ModuleReached(front);
        Require(Lost(behind) == 0 && Lost(side) == 0, "Flame hit outside the cone: " + Lost(behind) + "/" + Lost(side));
        Pass("Flame: damage and burn inside the cone only (" + Lost(front) + " dealt), nothing behind or to the side.");
        SpecialWeapons flameWeapon = shooter.GetComponent<SpecialWeapons>();
        Require(flameWeapon != null && flameWeapon.GroundFireCount > 0, "Flame left no ground fire.");
        GroundFireZone patch = UnityEngine.Object.FindObjectOfType<GroundFireZone>();
        Require(patch != null && WeaponTargets.Flat(patch.transform.position, front.transform.position) <= 2f,
            "Ground fire not under the target.");
        Require(Lost(behind) == 0 && Lost(side) == 0, "Ground fire hit outside the cone.");
        Pass("Flame: left " + flameWeapon.GroundFireCount + " ground fire patch(es) under the target, radius " + patch.Radius.ToString("0.0") + " m.");
        ClearArena(); yield return null;

        // Ice: slows on hit, then restores the original speed.
        Select(shooter, 9);
        Health ice = Dummy(new Vector3(9f, 0f, 0f));
        EnemyAI iceAI = ice.GetComponent<EnemyAI>();
        float baseSpeed = iceAI.speed;
        yield return WaitUntilLost(() => SlowEffect.MovementFactor(iceAI) < .99f, 8f);
        Require(Mathf.Approximately(SlowEffect.MovementFactor(iceAI), 0.5f), "Slow factor incorrect.");
        Require(Lost(ice) >= 12, "Ice did no damage.");
        shooter.enabled = false;
        yield return Wait(shooter.weapons[9].effectDuration + 1.5f);
        Require(Mathf.Approximately(iceAI.speed, baseSpeed) && Mathf.Approximately(SlowEffect.MovementFactor(iceAI), 1f), "Speed was not restored: " + iceAI.speed);
        shooter.enabled = true;
        Pass("Ice: damage and half speed on hit, original speed restored after the slow ended.");
        ClearArena(); yield return null;

        // Turret: placed, never more than two, shoots a distant enemy.
        shooter.weapons[11].fireRate = 0.5f;
        Select(shooter, 11);
        Health far = Dummy(new Vector3(10f, 0f, 0f));
        yield return WaitUntilLost(() => Lost(far) >= 18, 10f);
        Require(Lost(far) >= 18, "Turret did not shoot: " + Lost(far));
        ModuleReached(far);
        yield return Wait(3f);
        Require(UnityEngine.Object.FindObjectsOfType<WeaponTurret>().Length <= 2 && UnityEngine.Object.FindObjectsOfType<WeaponTurret>().Length >= 1, "Turret count.");
        Pass("Turret: placed (max 2) and shot the enemy for " + Lost(far) + " damage.");
        ClearArena(); yield return null;

        // Drones: two orbit the hero and damage targets.
        Select(shooter, 12);
        Health drone = Dummy(new Vector3(8f, 0f, 0f));
        Require(UnityEngine.Object.FindObjectsOfType<WeaponDrone>().Length == 2, "Drone count.");
        yield return WaitUntilLost(() => Lost(drone) >= 18, 6f);
        Require(Lost(drone) >= 18, "Drones did not shoot: " + Lost(drone));
        ModuleReached(drone);
        Select(shooter, 1);
        // Destroy is applied at the end of a frame; wait in game time so real frames have passed.
        yield return Wait(0.5f);
        int drones = UnityEngine.Object.FindObjectsOfType<WeaponDrone>().Length;
        int turrets = UnityEngine.Object.FindObjectsOfType<WeaponTurret>().Length;
        int saws = UnityEngine.Object.FindObjectsOfType<WeaponSaw>().Length;
        Require(drones == 0 && turrets == 0 && saws == 0, "Switching weapon must remove placed objects: drones=" + drones + ", turrets=" + turrets + ", saws=" + saws);
        Pass("Drones: two orbiters shot the enemy (" + Lost(drone) + " damage); changing weapon removes placed objects.");

        // Facing (09.11 bug: the hero spun and shot from the side): at the shot the hero faces the target,
        // collision spin does not accumulate.
        ClearArena(); yield return null;
        Rigidbody heroBody = hero.GetComponent<Rigidbody>();
        hero.rotation = Quaternion.LookRotation(Vector3.back);
        heroBody.rotation = hero.rotation;
        heroBody.angularVelocity = new Vector3(0f, 25f, 0f);
        Health aside = Dummy(new Vector3(7f, 0f, 3f));
        yield return WaitUntilLost(() => Lost(aside) > 0, 4f);
        Require(Lost(aside) > 0, "Standard weapon did not hit the side target.");
        Vector3 toTarget = aside.transform.position - hero.position;
        toTarget.y = 0f;
        float facingError = Vector3.Angle(hero.forward, toTarget);
        Require(facingError < 12f, "Hero does not face the target when shooting: " + facingError.ToString("F1") + " deg");
        Require(Mathf.Abs(heroBody.angularVelocity.y) < 0.01f, "Hero keeps spinning: " + heroBody.angularVelocity.y);
        Pass("Facing: hero turns to the target at the shot (error " + facingError.ToString("F1") + " deg), collision spin cleared.");
        ClearArena(); yield return null;
    }

    private static GameObject Card(string name)
    {
        foreach (Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true))
        {
            Transform found = Search(canvas.transform, name);
            if (found != null) return found.gameObject;
        }
        throw new Exception("Not found: " + name);
    }

    private static Transform Search(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = Search(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // Selects a weapon directly (bypasses the menu) and makes sure it fires.
    private static void Select(AutoShoter shooter, int index)
    {
        if (CheckModules)
        {
            MetaProgress.Unlock(MetaProgress.WeaponKey(index));
            WeaponElements.Buy(index, WeaponElement.Frost);
            WeaponElements.Equip(index, WeaponElement.Frost);
        }
        Require(shooter.SelectWeapon(index), "Cannot select weapon " + index);
        shooter.enabled = true;
    }

    private static void ModuleReached(Health health)
    {
        if (CheckModules) Require(SlowEffect.MovementFactor(health) < 1f, "Equipped frost module did not reach " + health.name);
    }

    private static IEnumerator WaitUntilLost(Func<bool> done, float seconds)
    {
        float end = Time.time + seconds;
        double cap = EditorApplication.timeSinceStartup + 90;
        while (!done() && Time.time < end && EditorApplication.timeSinceStartup < cap) yield return null;
    }
}
