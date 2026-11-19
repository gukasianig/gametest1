using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Модели предметов из Blender (make_items.py, 11.11.2026): кристалл, аптечка, магнит, малый сундук, турель, дрон, мина, пила.
// Ставит их рядом с героем, проверяет модель и подвижные части, снимает крупный кадр Logs/item-models.png.
// Batch: -batchmode -executeMethod ItemModelChecks.RunBatch (без -quit). Отчёт: Logs/item-models-results.txt.
[InitializeOnLoad]
public static class ItemModelChecks
{
    private const string Request = "Logs/item-models.request";
    private const string Report = "Logs/item-models-results.txt";
    private static CheckRunner routine;
    private static readonly List<string> lines = new List<string>();
    private static double started;

    static ItemModelChecks()
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
            if (EditorApplication.timeSinceStartup - started > 90) throw new Exception("Check timed out.");
            if (!routine.MoveNext()) Finish(true, "Item model checks complete.");
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

    private static IEnumerator WaitReal(double seconds)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < end) yield return null;
    }

    private static Transform Skin(GameObject item, string what)
    {
        Transform skin = item != null ? item.transform.Find("SkinModel") : null;
        Require(skin != null, what + ": no model (SkinModel).");
        return skin;
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        GameObject hero = PlayerProgression.Instance.gameObject;
        yield return WaitReal(1.6);   // меню должно успеть открыться (StartMenu.Start), иначе BeginGame не сработает
        hero.GetComponent<StartMenu>().SendMessage("BeginGame", (object)0);   // без (object) 0 уходит в перегрузку SendMessageOptions
        WaveManager waves = UnityEngine.Object.FindObjectOfType<WaveManager>();
        if (waves != null) waves.StopSpawning();
        yield return WaitReal(0.4);
        Require(GameSession.Instance.State == GameRunState.Playing, "Run did not start: " + GameSession.Instance.State);
        foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>()) UnityEngine.Object.Destroy(enemy.gameObject);
        AutoShoter shooter = hero.GetComponent<AutoShoter>();
        Vector3 c = hero.transform.position + hero.transform.forward * 6f;
        Vector3 right = Vector3.Cross(Vector3.up, hero.transform.forward).normalized;

        // Подбираемые: кристалл, аптечка, магнит, малый сундук (вне радиуса подбора).
        ExperiencePickup.Spawn(c - right * 3f, 1);
        DropPickup kit = DropPickup.Spawn(c - right * 1.5f, PickupKind.Health);
        DropPickup magnet = DropPickup.Spawn(c, PickupKind.Magnet);
        SmallChest chest = SmallChest.Spawn(c + right * 1.6f);
        // Юниты: включаем по одному из улучшений и ставим вручную рядом.
        shooter.SetSupportUnits(1, 1, 1, 1f, 1f);
        yield return null;
        SpecialWeapons turretSlot = null, mineSlot = null;
        foreach (SpecialWeapons slot in hero.GetComponents<SpecialWeapons>())
            if (slot.support && slot.Weapon != null)
            {
                if (slot.Weapon.kind == WeaponKind.Turret) turretSlot = slot;
                if (slot.Weapon.kind == WeaponKind.Mines) mineSlot = slot;
            }
        Require(turretSlot != null && mineSlot != null, "Support slots not created.");
        WeaponTurret.Create(turretSlot, c + right * 3.4f + hero.transform.forward * 1.5f);
        WeaponMine.Create(mineSlot, c + right * 3.2f - hero.transform.forward * 1.2f, 1);
        // Пила улетает и возвращается к герою (там исчезает) — проверяем её сразу после броска.
        WeaponSaw.Create(turretSlot, c - right * 1f + hero.transform.forward * 2.5f, right, 1, 3f);
        yield return null;
        Skin(GameObject.Find("Saw"), "Saw");
        yield return WaitReal(0.8);

        GameObject crystal = GameObject.Find("Experience Crystal");
        Transform crystalSkin = Skin(crystal, "XP crystal");
        Skin(kit.gameObject, "Repair kit");
        Skin(magnet.gameObject, "Magnet");
        Require(chest != null && chest.transform.Find("Visual/SkinModel") != null, "Small chest: no model.");
        GameObject turret = GameObject.Find("Turret"), drone = GameObject.Find("Drone"), mine = GameObject.Find("Mine");
        Skin(turret, "Turret");
        Skin(drone, "Drone");
        Skin(mine, "Mine");
        Require(ItemModelParts.Find(turret.transform, "Head") != null && ItemModelParts.Find(turret.transform, "TurretMuzzle") != null, "Turret Head/TurretMuzzle missing.");
        Transform rotor = ItemModelParts.Find(drone.transform, "RotorL");
        Require(rotor != null && ItemModelParts.Find(drone.transform, "RotorR") != null && ItemModelParts.Find(drone.transform, "Eye") != null, "Drone parts missing.");
        Transform lamp = ItemModelParts.Find(mine.transform, "Light");
        Require(lamp != null && lamp.GetComponent<Renderer>() != null, "Mine Light missing.");
        var sizes = new List<string>();
        foreach (GameObject go in new[] { crystal, kit.gameObject, magnet.gameObject, chest.transform.Find("Visual").gameObject, turret, drone, mine })
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in go.transform.Find("SkinModel").GetComponentsInChildren<Renderer>())
                if (r.enabled || r.name == "Light") { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            sizes.Add(go.name + " " + b.size.ToString("0.00"));
        }
        Pass("Model sizes (m): " + string.Join("; ", sizes));
        foreach (GameObject go in new[] { crystal, kit.gameObject, magnet.gameObject, chest.gameObject, turret, drone, mine })
            Require(go.GetComponentInChildren<ModelWalkAnimator>() == null, go.name + " got the enemy walk animator (part named Body/Leg/Wing).");
        Pass("Models on all 8 items (crystal, repair kit, magnet, small chest, turret, drone, mine, saw); parts Head, TurretMuzzle, RotorL/R, Eye, Light found.");

        // Винт крутится 1500°/с: за секунду он может вернуться почти в то же положение, поэтому мерим за короткий отрезок.
        Quaternion rotorBefore = rotor.rotation, crystalBefore = crystal.transform.rotation;
        Vector3 droneBefore = drone.transform.position;
        yield return WaitReal(0.04);
        float rotorTurn = Quaternion.Angle(rotorBefore, rotor.rotation);
        Require(rotorTurn > 1f, "Drone rotor does not spin. WeaponDrone: " + (drone.GetComponent<WeaponDrone>() != null) +
            ", drone moved " + Vector3.Distance(droneBefore, drone.transform.position).ToString("0.000") + ", rotor path " + Path(rotor) +
            ", drones named Drone: " + Array.FindAll(UnityEngine.Object.FindObjectsOfType<Transform>(), t => t.name == "Drone").Length +
            ", timeScale " + Time.timeScale + ", state " + GameSession.Instance.State);
        Vector3 skinBefore = crystalSkin.localPosition;
        int lampOn = 0;
        for (int i = 0; i < 20; i++) { yield return WaitReal(0.05); if (lamp.GetComponent<Renderer>().enabled) lampOn++; }
        Require(Quaternion.Angle(crystalBefore, crystal.transform.rotation) > 1f && (crystalSkin.localPosition - skinBefore).sqrMagnitude > 1e-6f,
            "Crystal does not spin/bob.");
        Require(lampOn > 0 && lampOn < 20, "Mine light does not blink: on " + lampOn + "/20.");
        Pass("Animation: rotor turned " + rotorTurn.ToString("0") + "° in 0.04 s, crystal spins and bobs, mine light on " + lampOn + "/20 samples.");

        Vector3 muzzle;
        Require(shooter.TryGetMuzzle(out muzzle) && Vector3.Distance(muzzle, hero.transform.position) < 2f, "Hero muzzle not on own weapon.");
        Pass("Hero muzzle on own weapon model (" + Vector3.Distance(muzzle, hero.transform.position).ToString("0.00") + " m from hero).");

        // Вторая партия: снаряды врагов, спора, корона элиты, сундук события, ящик сброса.
        Vector3 p = c + right * 6f;
        EnemyProjectile acid = EnemyProjectile.Create(p, hero.transform.forward, hero.transform, 0, EnemyProjectile.Style.Acid);
        EnemyProjectile quill = EnemyProjectile.Create(p + right * 1f, hero.transform.forward, hero.transform, 0, EnemyProjectile.Style.Quill);
        EnemyProjectile orb = EnemyProjectile.Create(p + right * 2f, hero.transform.forward, hero.transform, 0, EnemyProjectile.Style.Ball);
        MortarShell.Launch(p + right * 3f + Vector3.up, p + right * 3f + hero.transform.forward * 4f, 3f, 2f);
        yield return null;
        Skin(acid.gameObject, "Acid shot");
        Skin(quill.gameObject, "Bone quill");
        Skin(orb.gameObject, "Hostile orb");
        Skin(GameObject.Find("Mortar spore"), "Mortar spore");
        Require(acid.GetComponent<TrailRenderer>().enabled && quill.GetComponent<TrailRenderer>().enabled, "Projectile trail hidden by the model.");
        Renderer shaft = ItemModelParts.Find(quill.transform, "Shaft").GetComponent<Renderer>();
        Renderer fletch = ItemModelParts.Find(quill.transform, "Fletching").GetComponent<Renderer>();
        float tail = Vector3.Dot(fletch.bounds.center - shaft.bounds.center, quill.transform.forward);
        Require(tail < 0f, "Bone quill flies backwards (fletching ahead): " + tail.ToString("0.00"));
        Require(-tail < 0.8f, "Bone quill model too long: fletching " + (-tail).ToString("0.00") + " m behind the shaft centre.");
        Capture("item-shots", p + right * 1.5f + Vector3.up * 2.2f - hero.transform.forward * 2.4f, p + right * 1.5f + Vector3.up * 0.9f);
        Pass("Enemy shots: acid, bone quill (tip forward, fletching " + (-tail).ToString("0.00") + " m behind), orb, mortar spore have models; trails kept; frame Logs/item-shots.png.");

        if (waves != null) waves.wave = Mathf.Max(waves.wave, 6);   // на нулевой волне нет подходящих врагов для элиты
        GameObject elite = waves != null ? waves.SpawnElite() : null;
        Require(elite != null, "Elite not spawned.");
        Transform crown = elite.transform.Find("Elite crown");
        Require(crown != null && crown.Find("SkinModel") != null && crown.GetComponent<ItemModelParts.Spinner>() != null, "Elite crown model/spin missing.");
        Pass("Elite crown model (" + (elite.GetComponent<EliteExplosive>() != null ? "explosive, orange" : "gold") + ") spins.");
        UnityEngine.Object.Destroy(elite);

        ArenaEvents events = ArenaEvents.Instance;
        events.Begin(ArenaEventKind.SupplyDrop);
        yield return WaitReal(0.15);
        GameObject crate = GameObject.Find("Supply crate");
        Skin(crate, "Supply crate");
        float high = crate.transform.position.y;
        yield return WaitReal(1.0);
        Require(high - crate.transform.position.y > 3f, "Supply crate did not fall: " + high + " → " + crate.transform.position.y);
        Pass("Supply crate model fell from " + high.ToString("0.0") + " m to " + crate.transform.position.y.ToString("0.0") + " m.");
        events.Begin(ArenaEventKind.GuardChest);
        yield return WaitReal(0.3);
        GameObject guard = GameObject.Find("Guard chest");
        Skin(guard, "Guard chest");
        Transform lid = ItemModelParts.Find(guard.transform, "Lid");
        Require(lid != null, "Guard chest Lid missing.");
        Capture("item-guard-chest", guard.transform.position + Vector3.up * 4f - hero.transform.forward * 5f, guard.transform.position + Vector3.up * 0.8f);
        Pass("Guard chest model with Lid; frame Logs/item-guard-chest.png.");

        Capture("item-models", c + Vector3.up * 7f - hero.transform.forward * 6.5f, c + Vector3.up * 0.4f);
        Vector3 units = (turret.transform.position + mine.transform.position) * 0.5f;
        Capture("item-units", units + Vector3.up * 3.2f - hero.transform.forward * 3.2f + right * 0.5f, units);
        Vector3 droneAt = drone.transform.position;
        Capture("item-drone", droneAt + Vector3.up * 1.1f - hero.transform.forward * 1.6f, droneAt);
        Pass("Close-up frames Logs/item-models.png, item-units.png (turret, mine), item-drone.png.");
        int level = PlayerProgression.Instance.Level;
        PlayerProgression.Instance.TestLevelUp();
        yield return WaitReal(0.1);
        Require(PlayerProgression.Instance.Level == level + 1, "Level-up failed.");
        Pass("Level-up with effect ran without errors (level " + PlayerProgression.Instance.Level + ").");
    }

    private static string Path(Transform t) { return t == null ? "" : Path(t.parent) + "/" + t.name; }

    private static void Capture(string name, Vector3 from, Vector3 look)
    {
        var go = new GameObject("Item Check Camera");
        Camera camera = go.AddComponent<Camera>();
        camera.fieldOfView = 40f;
        go.transform.position = from;
        go.transform.rotation = Quaternion.LookRotation(look - from, Vector3.up);
        var texture = new RenderTexture(1280, 720, 24);
        camera.targetTexture = texture;
        camera.Render();
        RenderTexture.active = texture;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        image.Apply();
        File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        UnityEngine.Object.Destroy(image);
        UnityEngine.Object.Destroy(go);
        texture.Release();
    }
}
