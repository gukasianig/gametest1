using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Optional check of crits, piercing, the dash and the pickups (repair kit, magnet).
// Batch: -batchmode -executeMethod CombatMechanicsChecks.RunBatch (without -quit). Report: Logs/combat-mechanics-results.txt.
[InitializeOnLoad]
public static class CombatMechanicsChecks
{
    private const string Request = "Logs/combat-mechanics.request";
    private const string Report = "Logs/combat-mechanics-results.txt";
    private static CheckRunner routine;
    private static readonly List<string> lines = new List<string>();
    private static double started;

    static CombatMechanicsChecks()
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
            if (EditorApplication.timeSinceStartup - started > 300) throw new Exception("Check timed out.");
            if (!routine.MoveNext()) Finish(true, "Combat mechanics checks complete.");
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
        while (Time.time < end && EditorApplication.timeSinceStartup < cap) yield return null;
    }

    private static void SetRanks(PlayerProgression progression, int crit, int power, int pierce)
    {
        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var ranks = (Dictionary<string, int>)typeof(PlayerProgression).GetField("ranks", flags).GetValue(progression);
        ranks["crit_chance"] = crit;
        ranks["crit_power"] = power;
        ranks["pierce"] = pierce;
        typeof(PlayerProgression).GetMethod("ApplyStats", flags).Invoke(progression, null);
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        GameObject player = PlayerProgression.Instance.gameObject;
        player.GetComponent<StartMenu>().SendMessage("BeginGame", 1);
        AutoShoter shooter = player.GetComponent<AutoShoter>();
        shooter.enabled = false;
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        health.IncreaseMaxHealth(2000);
        PlayerProgression progression = PlayerProgression.Instance;
        PlayMovement movement = player.GetComponent<PlayMovement>();
        WaveManager waves = UnityEngine.Object.FindObjectOfType<WaveManager>();
        waves.StopSpawning();
        Require(GameSession.Instance.State == GameRunState.Playing, "Run did not start.");

        // 1. Catalog entries and the upgrade -> stats pipeline.
        UpgradeDefinition critChance = progression.catalog.entries.Find(e => e.id == "crit_chance");
        UpgradeDefinition critPower = progression.catalog.entries.Find(e => e.id == "crit_power");
        UpgradeDefinition pierce = progression.catalog.entries.Find(e => e.id == "pierce");
        Require(critChance != null && critPower != null && pierce != null, "New upgrades are not in the catalog.");
        Require(progression.catalog.entries.Count == 35, "Catalog size: " + progression.catalog.entries.Count);
        typeof(PlayerProgression).GetProperty("Level").SetValue(progression, 6);
        Require(progression.Available(critChance) && pierce != null && progression.Available(pierce), "Crit chance / pierce not offered at level 6.");
        Require(!progression.Available(critPower), "Crit power is offered before any crit chance.");
        SetRanks(progression, 4, 2, 2);
        Require(Mathf.Approximately(CombatStats.CritChance, 0.2f), "Crit chance " + CombatStats.CritChance);
        Require(Mathf.Approximately(CombatStats.CritMultiplier, 2f), "Crit multiplier " + CombatStats.CritMultiplier);
        Require(CombatStats.Pierce == 2, "Pierce " + CombatStats.Pierce);
        Require(progression.Available(critPower), "Crit power is not offered after crit chance.");
        Pass("Catalog has 35 upgrades; crit power needs crit chance; ranks give 20% / x2.0 / pierce 2.");

        // 2. Crit rolls: always crit at 100%, never at 0%, average near the chance.
        CombatStats.Set(1f, 0.5f, 0);
        int boosted = CombatStats.Roll(20, out bool crit);
        Require(crit && boosted == 40, "Forced crit " + boosted);
        CombatStats.Set(0f, 0f, 0);
        Require(CombatStats.Roll(20, out crit) == 20 && !crit, "No crit at 0%.");
        CombatStats.Set(0.25f, 0f, 0);
        int crits = 0;
        for (int i = 0; i < 4000; i++) { CombatStats.Roll(20, out bool c); if (c) crits++; }
        Require(crits > 800 && crits < 1200, "Crit rate off: " + crits + "/4000");
        Pass("Crit rolls: 100% always, 0% never, 25% gave " + crits + "/4000, damage x2.0 with +0.5.");

        // 3. A crit really reaches the enemy: damage through WeaponElementAttack.
        EnemyType weak = Array.Find(waves.enemies, e => e.name == "Weak") ?? waves.enemies[0];
        Vector3 forward = player.transform.forward; forward.y = 0f; forward.Normalize();
        Vector3 origin = player.transform.position;
        GameObject dummy = UnityEngine.Object.Instantiate(weak.prefab, origin + forward * 12f + Vector3.left * 8f, Quaternion.identity);
        dummy.GetComponent<EnemyAI>().MovementLocked = true;
        Health dummyHealth = dummy.GetComponent<Health>();
        dummyHealth.SetHealth(1000);
        CombatStats.Set(1f, 0f, 0);
        new WeaponElementAttack(0).Hit(dummyHealth, 20);
        Require(dummyHealth.CurrentHealth == 1000 - 30, "Crit damage dealt: " + (1000 - dummyHealth.CurrentHealth));
        UnityEngine.Object.Destroy(dummy);
        Pass("A forced crit deals 30 instead of 20 (x1.5 base) to an enemy.");

        // 4. Piercing: a bullet passes through N extra enemies, hitting each once.
        CombatStats.Set(0f, 0f, 0);
        int[] damageTaken = new int[2];
        for (int run = 0; run < 2; run++)
        {
            int pierceCount = run == 0 ? 0 : 2;
            var line = new List<Health>();
            for (int i = 0; i < 3; i++)
            {
                Vector3 place = origin + forward * (6f + i * 3f) + new Vector3(0f, 0f, 0f);
                place.y = weak.prefab.transform.position.y;
                GameObject enemy = UnityEngine.Object.Instantiate(weak.prefab, place, Quaternion.identity);
                float bottomY = float.PositiveInfinity;
                foreach (Renderer r in enemy.GetComponentsInChildren<Renderer>()) bottomY = Mathf.Min(bottomY, r.bounds.min.y);
                if (!float.IsInfinity(bottomY)) enemy.transform.position += Vector3.up * -bottomY;
                enemy.GetComponent<EnemyAI>().MovementLocked = true;
                enemy.GetComponent<Health>().SetHealth(1000);
                line.Add(enemy.GetComponent<Health>());
            }
            yield return Wait(0.2f);
            Collider firstCollider = line[0].GetComponentInChildren<Collider>();
            Vector3 muzzle = new Vector3(shooter.firePoint.position.x, firstCollider.bounds.center.y, shooter.firePoint.position.z);
            GameObject bulletObject = UnityEngine.Object.Instantiate(shooter.bulletPrefab, muzzle, Quaternion.LookRotation(forward));
            Bullet bullet = bulletObject.GetComponent<Bullet>();
            bullet.SetOwner(player.transform);
            bullet.damage = 20;
            bullet.speed = 30f;
            bullet.lifeTime = 3f;
            bullet.pierce = pierceCount;
            yield return Wait(1.5f);
            int hits = 0;
            foreach (Health h in line) if (h != null && h.CurrentHealth < 1000) hits++;
            foreach (Health h in line) if (h != null && 1000 - h.CurrentHealth > 20) throw new Exception("An enemy was hit twice by one bullet.");
            damageTaken[run] = hits;
            foreach (Health h in line) if (h != null) UnityEngine.Object.Destroy(h.gameObject);
            yield return Wait(0.3f);
        }
        Require(damageTaken[0] == 1, "Without pierce the bullet hit " + damageTaken[0] + " enemies.");
        Require(damageTaken[1] == 3, "Pierce 2 hit " + damageTaken[1] + " enemies.");
        Pass("Piercing: no pierce hits 1 of 3 enemies, pierce 2 hits all 3, each exactly once.");

        // 5. Dash: moves the player, grants invulnerability, cooldown blocks a second dash.
        movement.enabled = true;
        Vector3 before = player.transform.position;
        movement.RequestDash();
        yield return Wait(0.05f);
        int hp = health.CurrentHP;
        health.TakeDamage(50);
        Require(health.CurrentHP == hp && health.Invulnerable, "Dash did not protect from damage.");
        yield return Wait(0.5f);
        Vector3 after = player.transform.position;
        float moved = Vector3.Distance(new Vector3(before.x, 0, before.z), new Vector3(after.x, 0, after.z));
        Require(moved >= 5f, "Dash distance " + moved);
        Require(movement.DashCooldownRemaining > 2f, "Dash cooldown " + movement.DashCooldownRemaining);
        health.TakeDamage(50);
        Require(health.CurrentHP == hp - 50, "Damage after the protection ended: " + (hp - health.CurrentHP));
        Vector3 blocked = player.transform.position;
        movement.RequestDash();
        yield return Wait(0.4f);
        Require(Vector3.Distance(blocked, player.transform.position) < 1f, "A second dash worked during the cooldown.");
        Pass("Dash moved " + moved.ToString("0.0") + " m, damage blocked during it, works only after the cooldown.");

        // 6. Pickups: repair kit heals (and is kept at full health), magnet pulls distant crystals, drop rates.
        int low = health.CurrentHP;
        DropPickup.Spawn(player.transform.position, PickupKind.Health);
        yield return Wait(0.3f);
        Require(health.CurrentHP > low, "Repair kit did not heal: " + low + " -> " + health.CurrentHP);
        Require(DropPickup.ActiveCount == 0, "Repair kit was not consumed.");
        health.Heal(100000);
        DropPickup.Spawn(player.transform.position, PickupKind.Health);
        yield return Wait(0.3f);
        Require(DropPickup.ActiveCount == 1, "Repair kit was wasted at full health.");
        DropPickup.Clear();

        Vector3 farAway = Vector3.zero;
        for (int i = 0; i < 30; i++)
        {
            Vector3 candidate = player.transform.position + new Vector3(Mathf.Cos(i) * 30f, 0f, Mathf.Sin(i) * 30f);
            if (ArenaLayout.Instance.IsWalkable(candidate, 1f)) { farAway = candidate; break; }
        }
        Require(farAway != Vector3.zero, "No place for a distant crystal.");
        ExperiencePickup.Spawn(farAway, 1);
        int Crystals() { int n = 0; foreach (GameObject g in UnityEngine.Object.FindObjectsOfType<GameObject>()) if (g.name == "Experience Crystal") n++; return n; }
        Require(Crystals() == 1, "Test crystal missing.");
        DropPickup.Spawn(player.transform.position, PickupKind.Magnet);
        yield return Wait(3f);
        Require(Crystals() == 0, "Magnet did not pull the distant crystal.");
        Pass("Repair kit heals and stays at full HP; magnet collected a crystal 30 m away.");

        int drops = 0, trials = 4000;
        for (int i = 0; i < trials; i++) { DropPickup.TryDrop(player.transform.position, false); }
        drops = DropPickup.ActiveCount;
        DropPickup.Clear();
        Require(drops > trials * 0.02f && drops < trials * 0.09f, "Drop rate off: " + drops + "/" + trials);
        DropPickup.TryDrop(player.transform.position, true);
        Require(DropPickup.ActiveCount == 1, "Elite did not drop a kit.");
        DropPickup.Clear();
        Pass("Drop rate " + drops + "/" + trials + " (expected about 5%), elites always drop a repair kit.");

        // 7. Ricochet: after hitting the first enemy the bullet jumps to a second one standing off the line.
        origin = player.transform.position;
        forward = player.transform.forward; forward.y = 0f; forward.Normalize();
        SetRanks(progression, 0, 0, 0);
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        int[] ricochetHits = new int[2];
        for (int run = 0; run < 2; run++)
        {
            var pair = new List<Health>();
            Vector3[] spots = { origin + forward * 8f, origin + forward * 8f + side * 7f };
            foreach (Vector3 spot in spots)
            {
                GameObject enemy = UnityEngine.Object.Instantiate(weak.prefab, spot, Quaternion.identity);
                float bottomY = float.PositiveInfinity;
                foreach (Renderer r in enemy.GetComponentsInChildren<Renderer>()) bottomY = Mathf.Min(bottomY, r.bounds.min.y);
                if (!float.IsInfinity(bottomY)) enemy.transform.position += Vector3.up * -bottomY;
                enemy.GetComponent<EnemyAI>().MovementLocked = true;
                enemy.GetComponent<Health>().SetHealth(1000);
                pair.Add(enemy.GetComponent<Health>());
            }
            yield return Wait(0.2f);
            Collider firstCollider = pair[0].GetComponentInChildren<Collider>();
            Vector3 muzzle = new Vector3(shooter.firePoint.position.x, firstCollider.bounds.center.y, shooter.firePoint.position.z);
            Bullet bullet = UnityEngine.Object.Instantiate(shooter.bulletPrefab, muzzle, Quaternion.LookRotation(forward)).GetComponent<Bullet>();
            bullet.SetOwner(player.transform);
            bullet.damage = 100;
            bullet.speed = 30f;
            bullet.lifeTime = 3f;
            bullet.ricochet = run == 0 ? 0 : 1;
            yield return Wait(1.5f);
            ricochetHits[run] = pair[1] != null && pair[1].CurrentHealth < 1000 ? 1 : 0;
            if (run == 1) Require(pair[1] != null && pair[1].CurrentHealth == 1000 - 80, "Ricochet damage: " + (pair[1] == null ? -1 : 1000 - pair[1].CurrentHealth) + " first " + (pair[0] == null ? -1 : 1000 - pair[0].CurrentHealth) + " bulletAlive " + (bullet != null) + " ricochetLeft " + (bullet != null ? bullet.ricochet : -9) + " e0 " + pair[0].transform.position + " e1 " + pair[1].transform.position + " lvl " + progression.Level + " timeScale " + Time.timeScale);
            foreach (Health h in pair) if (h != null) UnityEngine.Object.Destroy(h.gameObject);
            yield return Wait(0.3f);
        }
        Require(ricochetHits[0] == 0 && ricochetHits[1] == 1, "Ricochet hits without/with: " + ricochetHits[0] + "/" + ricochetHits[1]);
        Pass("Ricochet: without it the second enemy is untouched; with 1 rank it takes 80% damage after the jump.");

        // 8. Drone, turret and mine cards: combat upgrades for any weapon; counts and damage multipliers apply.
        UpgradeDefinition droneCount = progression.catalog.entries.Find(e => e.id == "drone_count");
        UpgradeDefinition droneDamage = progression.catalog.entries.Find(e => e.id == "drone_damage");
        UpgradeDefinition turretCount = progression.catalog.entries.Find(e => e.id == "turret_count");
        UpgradeDefinition turretDamage = progression.catalog.entries.Find(e => e.id == "turret_damage");
        UpgradeDefinition ricochetCard = progression.catalog.entries.Find(e => e.id == "ricochet");
        Require(droneCount != null && droneDamage != null && turretCount != null && turretDamage != null && ricochetCard != null, "New cards missing.");
        // Дрон, турель и мины — улучшения боя для любого оружия (в выборе перед боем их нет).
        UpgradeDefinition mineCount = progression.catalog.entries.Find(e => e.id == "mine_count");
        Require(mineCount != null, "mine_count card missing.");
        shooter.SelectWeapon(Array.FindIndex(shooter.weapons, w => w != null && w.kind == WeaponKind.Standard));
        Require(progression.Available(droneCount) && progression.Available(turretCount) && progression.Available(mineCount) && progression.Available(ricochetCard) &&
            !progression.Available(droneDamage) && !progression.Available(turretDamage), "Support cards availability with an ordinary weapon.");
        BindingFlags flags2 = BindingFlags.NonPublic | BindingFlags.Instance;
        var ranks = (Dictionary<string, int>)typeof(PlayerProgression).GetField("ranks", flags2).GetValue(progression);
        ranks["drone_count"] = 2;
        ranks["drone_damage"] = 5;
        ranks["turret_count"] = 2;
        ranks["turret_damage"] = 3;
        ranks["mine_count"] = 2;
        typeof(PlayerProgression).GetMethod("ApplyStats", flags2).Invoke(progression, null);
        yield return Wait(0.2f);
        Require(UnityEngine.Object.FindObjectsOfType<WeaponDrone>().Length == 2, "Drones after 2 ranks: " + UnityEngine.Object.FindObjectsOfType<WeaponDrone>().Length);
        SpecialWeapons droneUnit = null, turretUnit = null, mineUnit = null;
        foreach (SpecialWeapons unit in player.GetComponents<SpecialWeapons>())
        {
            if (!unit.support) continue;
            if (unit.Weapon.kind == WeaponKind.Drone) droneUnit = unit;
            if (unit.Weapon.kind == WeaponKind.Turret) turretUnit = unit;
            if (unit.Weapon.kind == WeaponKind.Mines) mineUnit = unit;
        }
        Require(droneUnit != null && turretUnit != null && mineUnit != null, "Support units were not created.");
        Require(Mathf.Approximately(droneUnit.DroneDamage, 2f) && droneUnit.SupportDamage(droneUnit.DroneDamage) >= 2, "Drone damage multiplier " + droneUnit.DroneDamage);
        Require(turretUnit.TurretLimit == 2 && Mathf.Approximately(turretUnit.TurretDamage, 1.6f), "Turret limit/damage " + turretUnit.TurretLimit + " / " + turretUnit.TurretDamage);
        Require(mineUnit.MineLimit == 4, "Mine limit after 2 ranks (expected 4): " + mineUnit.MineLimit);
        Require(progression.Available(droneDamage) && progression.Available(turretDamage), "Damage cards must appear after the first unit.");
        Pass("Support upgrades with an ordinary weapon: 2 drones x2.0 damage, turret limit " + turretUnit.TurretLimit + " x1.6, mine limit " + mineUnit.MineLimit + "; damage cards appear only after the first unit.");

        // 9. The remaining cards: survival, combat, progress and cursed upgrades, card texts, reroll.
        origin = player.transform.position;
        forward = player.transform.forward; forward.y = 0f; forward.Normalize();
        int standardIndex = Array.FindIndex(shooter.weapons, w => w != null && w.kind == WeaponKind.Standard);
        Require(shooter.SelectWeapon(standardIndex), "Standard weapon not selectable.");
        shooter.enabled = false;
        foreach (WeaponDrone d in UnityEngine.Object.FindObjectsOfType<WeaponDrone>()) UnityEngine.Object.Destroy(d.gameObject);
        foreach (WeaponTurret t in UnityEngine.Object.FindObjectsOfType<WeaponTurret>()) UnityEngine.Object.Destroy(t.gameObject);
        MethodInfo preview = typeof(PlayerProgression).GetMethod("Preview", flags2);
        foreach (UpgradeDefinition definition in progression.catalog.entries)
        {
            string text = (string)preview.Invoke(progression, new object[] { definition });
            Require(!string.IsNullOrEmpty(text), "Empty card text for " + definition.id);
        }
        Pass("All 35 cards produce a text preview.");

        foreach (string id in new[] { "crit_chance", "crit_power", "pierce", "ricochet", "drone_count", "drone_damage", "turret_count", "turret_damage", "mine_count" }) ranks[id] = 0;
        float baseRange = shooter.range;
        int basePellets = shooter.CurrentPelletCount;
        ranks["armor"] = 4; ranks["regen"] = 5; ranks["vampirism"] = 5; ranks["dash_cooldown"] = 4;
        ranks["pellets"] = 2; ranks["range"] = 5; ranks["elite_hunter"] = 4; ranks["explosive"] = 2;
        ranks["experience"] = 5; ranks["luck"] = 4; ranks["orbit_speed"] = 5; ranks["pickup_speed"] = 4; ranks["dash_distance"] = 2;
        typeof(PlayerProgression).GetMethod("ApplyStats", flags2).Invoke(progression, null);
        Require(Mathf.Approximately(CombatStats.Armor, 0.2f) && Mathf.Approximately(CombatStats.RegenPerSecond, 2f) &&
            Mathf.Approximately(CombatStats.VampirismPerKill, 1f), "Survival stats.");
        Require(shooter.CurrentPelletCount == basePellets + 2 && shooter.CurrentSpread > 0f, "Pellets " + shooter.CurrentPelletCount + " spread " + shooter.CurrentSpread);
        Require(shooter.range > baseRange * 1.45f, "Range " + baseRange + " -> " + shooter.range);
        Pass("Stats written: armor 20%, regen 2 HP/s, vampirism 1/kill, +2 pellets with spread, range x1.5.");

        // Armor and regeneration.
        yield return Wait(0.6f);
        health.Heal(100000);
        int full = health.CurrentHP;
        health.TakeDamage(100);
        Require(health.CurrentHP == full - 80, "Armor damage taken: " + (full - health.CurrentHP));
        int hurtHp = health.CurrentHP;
        yield return Wait(2.2f);
        Require(health.CurrentHP >= hurtHp + 3, "Regeneration: " + hurtHp + " -> " + health.CurrentHP);
        // Vampirism: a kill heals 1 HP.
        int beforeKill = health.CurrentHP;
        GameObject victim = UnityEngine.Object.Instantiate(weak.prefab, origin + forward * 14f + Vector3.right * 10f, Quaternion.identity);
        victim.GetComponent<EnemyAI>().MovementLocked = true;
        yield return null;
        victim.GetComponent<Health>().TakeDamage(100000);
        Require(health.CurrentHP >= beforeKill + 1, "Vampirism: " + beforeKill + " -> " + health.CurrentHP);
        Pass("Armor removes 20% of a hit, regeneration heals over time, a kill heals through vampirism.");

        // Elite hunter and explosive bullets.
        GameObject elite = UnityEngine.Object.Instantiate(weak.prefab, origin + forward * 14f + Vector3.left * 12f, Quaternion.identity);
        elite.name = "Elite Weak";
        elite.GetComponent<EnemyAI>().MovementLocked = true;
        Health eliteHealth = elite.GetComponent<Health>();
        eliteHealth.SetHealth(1000);
        new WeaponElementAttack(0).Hit(eliteHealth, 20);
        Require(eliteHealth.CurrentHealth == 1000 - 32, "Elite hunter damage " + (1000 - eliteHealth.CurrentHealth));
        UnityEngine.Object.Destroy(elite);
        var blastPair = new List<Health>();
        foreach (Vector3 spot in new[] { origin + forward * 8f, origin + forward * 8f + side * 1.8f })
        {
            GameObject enemy = UnityEngine.Object.Instantiate(weak.prefab, spot, Quaternion.identity);
            float bottomY = float.PositiveInfinity;
            foreach (Renderer r in enemy.GetComponentsInChildren<Renderer>()) bottomY = Mathf.Min(bottomY, r.bounds.min.y);
            if (!float.IsInfinity(bottomY)) enemy.transform.position += Vector3.up * -bottomY;
            enemy.GetComponent<EnemyAI>().MovementLocked = true;
            enemy.GetComponent<Health>().SetHealth(1000);
            blastPair.Add(enemy.GetComponent<Health>());
        }
        yield return Wait(0.2f);
        Collider blastCollider = blastPair[0].GetComponentInChildren<Collider>();
        Bullet blastBullet = UnityEngine.Object.Instantiate(shooter.bulletPrefab,
            new Vector3(shooter.firePoint.position.x, blastCollider.bounds.center.y, shooter.firePoint.position.z), Quaternion.LookRotation(forward)).GetComponent<Bullet>();
        blastBullet.SetOwner(player.transform);
        blastBullet.damage = 100; blastBullet.speed = 30f; blastBullet.lifeTime = 3f;
        yield return Wait(1.5f);
        Require(blastPair[0].CurrentHealth == 900, "Explosive bullet direct hit " + blastPair[0].CurrentHealth);
        Require(blastPair[1].CurrentHealth == 960, "Explosion damage to the neighbour: " + (1000 - blastPair[1].CurrentHealth));
        foreach (Health h in blastPair) if (h != null) UnityEngine.Object.Destroy(h.gameObject);
        Pass("Elite hunter x1.6 on elites; an explosive bullet hits the target and deals 40% to a neighbour 1.8 m away.");

        // Progress: dash cooldown/distance, experience, luck, orbit speed.
        yield return Wait(4f);
        movement.RequestDash();
        yield return Wait(0.3f);
        Require(movement.DashCooldownRemaining < 3f, "Dash cooldown with the upgrade: " + movement.DashCooldownRemaining);
        int xpBefore = progression.Experience;
        int levelBefore = progression.Level;
        ExperiencePickup.Spawn(player.transform.position, 4);
        yield return Wait(0.5f);
        Require(progression.Level == levelBefore && progression.Experience - xpBefore == 6, "Experience bonus: " + (progression.Experience - xpBefore));
        int luckyTrials = 2000;
        for (int i = 0; i < luckyTrials; i++) DropPickup.TryDrop(player.transform.position, false);
        int luckyDrops = DropPickup.ActiveCount;
        DropPickup.Clear();
        Require(luckyDrops > luckyTrials * 0.075f, "Luck did not raise drops: " + luckyDrops + "/" + luckyTrials);
        OrbitManager orbitManager = player.GetComponent<OrbitManager>();
        Require(orbitManager == null || orbitManager.orbitSpeed >= 300f, "Orbit speed " + (orbitManager == null ? -1f : orbitManager.orbitSpeed));
        Pass("Dash cooldown 2.4 s, +50% experience (4 -> 6), luck raised drops to " + luckyDrops + "/" + luckyTrials + ", orbit speed doubled.");

        // Cursed cards.
        ranks["cursed_glass"] = 1; ranks["cursed_haste"] = 1; ranks["cursed_greed"] = 1;
        typeof(PlayerProgression).GetMethod("ApplyStats", flags2).Invoke(progression, null);
        Require(progression.DamageMultiplier >= 1.6f && progression.AttackSpeedMultiplier >= 1.4f, "Cursed multipliers " + progression.DamageMultiplier + " / " + progression.AttackSpeedMultiplier);
        Require(Mathf.Approximately(CombatStats.EnemySpeedCurse, 1.15f) && Mathf.Approximately(CombatStats.EnemyHealthCurse, 1.25f), "Cursed enemy penalties.");
        int maxBefore = health.maxHP;
        health.ReduceMaxHealth(0.3f);
        Require(health.maxHP == Mathf.RoundToInt(maxBefore * 0.7f) && health.CurrentHP <= health.maxHP, "Cursed max health: " + maxBefore + " -> " + health.maxHP);
        Pass("Cursed cards: damage x1.6, attack speed x1.4, enemies +15% speed and +25% HP, max HP -30% without healing.");

        // Reroll on the real level-up screen: two rerolls per run, then it stops; picking cards still works.
        ranks["cursed_glass"] = 0; ranks["cursed_haste"] = 0; ranks["cursed_greed"] = 0;
        typeof(PlayerProgression).GetMethod("ApplyStats", flags2).Invoke(progression, null);
        progression.AddExperience(1000);
        yield return null;
        Require(progression.Choosing && progression.Offered.Count > 0, "Level-up screen did not open.");
        Require(progression.RerollsLeft == 2, "Rerolls at the start: " + progression.RerollsLeft);
        Require(progression.Reroll() && progression.Reroll(), "Rerolls failed.");
        Require(!progression.Reroll() && progression.RerollsLeft == 0, "A third reroll was allowed.");
        int guard = 0;
        while (progression.Choosing && guard++ < 200) progression.SelectOfferedUpgrade(0);
        Require(!progression.Choosing && Time.timeScale > 0f, "Level-up screen did not close after picking cards.");
        Pass("Reroll: 2 per run, the third is refused; picking through " + guard + " level-ups closes the screen.");
    }
}
