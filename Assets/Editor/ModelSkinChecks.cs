using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Необязательная проверка моделей оружия в бою (ModelSkinSet) и снимки игровой камеры.
// Batch: -batchmode -executeMethod ModelSkinChecks.RunBatch (без -quit). Отчёт: Logs/model-skin-results.txt, кадры docs/art/Skin_play_*.png.
[InitializeOnLoad]
public static class ModelSkinChecks
{
    private const string Request = "Logs/model-skin.request";
    private const string Report = "Logs/model-skin-results.txt";
    private static CheckRunner routine;
    private static readonly List<string> lines = new List<string>();
    private static double started;
    private static int errorLogs;

    static ModelSkinChecks()
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
        errorLogs = 0;
        Application.logMessageReceived += OnLog;
        started = EditorApplication.timeSinceStartup;
        routine = new CheckRunner(Checks());
        EditorApplication.update += Tick;
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception) { errorLogs++; lines.Add("LOG " + type + ": " + message); }
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 200) throw new Exception("Check timed out.");
            if (!routine.MoveNext()) Finish(true, "Model skin checks complete.");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Pass(string text) { lines.Add("PASS: " + text); File.WriteAllLines(Report, lines); }
    private static void Require(bool condition, string text) { if (!condition) throw new Exception(text); }

    private static void Finish(bool success, string text)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        lines.Add((success ? "PASS: " : "FAIL: ") + text);
        File.WriteAllLines(Report, lines);
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        double cap = EditorApplication.timeSinceStartup + 60;
        while (Time.time < end && EditorApplication.timeSinceStartup < cap)
        {
            if (PlayerProgression.Instance != null && PlayerProgression.Instance.Choosing) PlayerProgression.Instance.SelectOfferedUpgrade(0);
            yield return null;
        }
    }

    private static void Shot(string path)
    {
        Camera cam = Camera.main;
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null;
        RenderTexture.active = null;
        UnityEngine.Object.Destroy(rt);
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        GameObject player = PlayerProgression.Instance.gameObject;
        player.GetComponent<StartMenu>().SendMessage("BeginGame", 1);
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        health.IncreaseMaxHealth(2000);
        Require(GameSession.Instance.State == GameRunState.Playing, "Run did not start.");
        Directory.CreateDirectory("docs/art");
        AutoShoter shooter = player.GetComponent<AutoShoter>();
        string[] expected = { "blaster-b", "blaster-h", "blaster-d", "blaster-e" };

        for (int w = 0; w < shooter.weapons.Length; w++)
        {
            if (shooter.weapons[w] == null || !shooter.weapons[w].IsUsable) continue;
            Require(shooter.SelectWeapon(w), "SelectWeapon failed for " + w);
            yield return null;
            Transform socket = null;
            foreach (Transform t in player.GetComponentsInChildren<Transform>(true)) if (t.name == "WeaponSocket") socket = t;
            Require(socket != null, "WeaponSocket missing.");
            int skins = 0;
            Transform skin = null;
            foreach (Transform c in socket) if (c.name == "SkinModel") { skins++; skin = c; }
            Require(skins == 1, "Weapon " + w + ": expected exactly 1 SkinModel, found " + skins);
            Require(skin.GetComponentsInChildren<Collider>().Length == 0, "Skin has colliders.");
            Renderer[] rs = skin.GetComponentsInChildren<Renderer>();
            Require(rs.Length > 0 && rs[0].enabled && rs[0].sharedMaterial != null && rs[0].sharedMaterial.shader.name.Contains("Universal"), "Skin renderer/material wrong.");
            Pass("Weapon " + w + " (" + shooter.weapons[w].kind + ") shows one skin model, no colliders, URP material.");
            Require(ModelSkins.TryGetMuzzle(out Vector3 muzzle), "Weapon " + w + ": no muzzle.");
            Vector3 ml = player.transform.InverseTransformPoint(muzzle);
            if (w < 4) Require(ml.z > 0.35f, "Weapon " + w + ": muzzle is not in front of the hero, local z=" + ml.z.ToString("F2"));
            yield return Wait(w == 3 || w == 6 || w == 10 ? 2f : 0.6f);
            if (w == 3 || w == 6 || w == 10) Shot("docs/art/Skin_play_weapon" + w + ".png");
        }
        Pass("All usable weapon presets (" + shooter.weapons.Length + ") show a model in hand.");

        // Арена: укрытия получили модели, исходные кубы скрыты, коллайдеры остались.
        ArenaLayout arena = ArenaLayout.Instance;
        int skinned = 0, hiddenCubes = 0;
        foreach (Transform t in arena.transform) if (t.name.StartsWith("SkinModel_")) skinned++;
        foreach (Transform t in arena.transform)
        {
            if (!t.name.StartsWith("Cargo") && !t.name.Contains("generator") && !t.name.Contains("tower")) continue;
            Renderer r = t.GetComponent<Renderer>();
            if (r != null && !r.enabled && t.GetComponent<Collider>() != null) hiddenCubes++;
        }
        Require(skinned >= 14, "Arena skins missing: " + skinned);
        Require(hiddenCubes >= 12, "Arena cubes not hidden or lost their colliders: " + hiddenCubes);
        Pass("Arena: " + skinned + " cover models; " + hiddenCubes + " source cubes hidden with colliders kept.");

        ArenaPropsRoot editable = UnityEngine.Object.FindObjectOfType<ArenaPropsRoot>();
        Transform decorRoot = editable != null && editable.transform.childCount > 0 ? editable.transform : arena.transform.Find("Arena decor");
        Require(decorRoot != null && decorRoot.childCount >= 120, "Arena decor missing: " + (decorRoot != null ? decorRoot.childCount : 0));
        Require(decorRoot.GetComponentsInChildren<Collider>().Length == 0, "Decor must not have colliders.");
        bool inCentre = false;
        foreach (Transform t in decorRoot) if (new Vector2(t.position.x, t.position.z).magnitude < 9f) inCentre = true;
        Require(!inCentre, "Decor placed in the spawn area.");
        Pass("Arena decor: " + decorRoot.childCount + " objects, no colliders, nothing in the centre.");

        if (ModelSkins.CurrentSet != null && ModelSkins.CurrentSet.Find("Saw") != null)
        {
        // Враги с моделью из Blender: первый появившийся Weak получил SkinModel, исходный рендерер выключен, коллайдер остался.
        int weak = 0, weakSkinned = 0;
        foreach (EnemyAI ai in UnityEngine.Object.FindObjectsOfType<EnemyAI>())
        {
            if (!ai.name.StartsWith("Enemy_Weak")) continue;
            weak++;
            Transform sm = ai.transform.Find("SkinModel");
            Renderer own = ai.GetComponent<Renderer>();
            if (sm != null && (own == null || !own.enabled) && ai.GetComponent<Collider>() != null) weakSkinned++;
        }
        Require(weak == 0 || weakSkinned == weak, "Enemy_Weak skinned " + weakSkinned + " of " + weak);
        Pass("Enemy_Weak in battle: " + weakSkinned + " of " + weak + " have the Blender model (collider kept, cube hidden).");

        // Ориентация и анимация: метка Front смотрит туда же, куда движется враг; ноги реально качаются при ходьбе.
        yield return Wait(2f);
        int facingChecked = 0, legsMoved = 0;
        var seenTypes = new HashSet<string>();
        foreach (EnemyAI ai in UnityEngine.Object.FindObjectsOfType<EnemyAI>())
        {
            Transform sm = ai.transform.Find("SkinModel");
            if (sm == null) continue;
            seenTypes.Add(ai.name.Replace("(Clone)", "").Trim());
            Transform front = null, leg = null;
            foreach (Transform t in sm.GetComponentsInChildren<Transform>()) { if (t.name == "Front") front = t; if (t.name == "Leg_FL") leg = t; }
            Require(front != null && sm.GetComponent<ModelWalkAnimator>() != null, "Weak skin lacks Front marker or ModelWalkAnimator.");
            Vector3 dir = front.position - sm.position; dir.y = 0f;
            Require(Vector3.Dot(dir.normalized, ai.transform.forward) > 0.9f, "Weak model faces away from enemy forward.");
            facingChecked++;
            if (leg != null && Quaternion.Angle(leg.localRotation, Quaternion.identity) > 0.5f) legsMoved++;
        }
        if (facingChecked > 0) Require(legsMoved > 0, "Legs never swing on walking enemies.");
        Pass("Skinned enemies (" + string.Join(", ", seenTypes) + "): model faces forward (" + facingChecked + " checked), legs swing on " + legsMoved + " walking enemies.");

        // Все типы врагов: создаём каждый префаб, применяем модель и проверяем перёд/ноги/размер.
        string[] types = { "Weak", "Fast", "tank", "Shooter", "Charger", "Bomber", "Healer", "Splitter", "Summoner", "Sniper", "Mortar", "Teleporter", "Buffer", "Armored" };
        foreach (string type in types)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy_" + type + ".prefab");
            Require(prefab != null, "Prefab missing: " + type);
            GameObject e = UnityEngine.Object.Instantiate(prefab, new Vector3(0f, 0f, -60f), Quaternion.identity);
            ModelSkins.ApplyEnemy(e, prefab.name);
            Transform sm = e.transform.Find("SkinModel");
            Require(sm != null, type + ": no SkinModel.");
            Transform front = null;
            foreach (Transform t in sm.GetComponentsInChildren<Transform>()) if (t.name == "Front") front = t;
            Require(front != null && sm.GetComponent<ModelWalkAnimator>() != null, type + ": no Front/animator.");
            Vector3 d = front.position - sm.position; d.y = 0f;
            Require(Vector3.Dot(d.normalized, e.transform.forward) > 0.9f, type + ": faces away.");
            Bounds sb = new Bounds(sm.position, Vector3.zero);
            foreach (Renderer r in sm.GetComponentsInChildren<Renderer>()) sb.Encapsulate(r.bounds);
            Require(sb.size.y > 0.3f && sb.size.y < 4f, type + ": odd model height " + sb.size.y.ToString("F2"));
            UnityEngine.Object.Destroy(e);
        }
        Pass("All " + types.Length + " enemy prefabs get a model, face forward, have animator, sane size.");

        // Босс и объекты спецоружия
        BossController boss = BossController.Create(new Vector3(0f, 0f, -70f), player.transform);
        Transform bs = boss.transform.Find("SkinModel");
        Require(bs != null && bs.GetComponent<ModelWalkAnimator>() != null, "Boss has no model/animator.");
        Require(boss.GetComponent<Collider>() != null, "Boss lost its collider.");
        UnityEngine.Object.Destroy(boss.gameObject);
        foreach (string n in new[] { "Saw", "Mine", "Turret", "Drone" })
        {
            GameObject o = SpecialWeapons.Prim(PrimitiveType.Cube, n, new Vector3(0f, 0.5f, -80f), Vector3.one, Color.white);
            Require(o.transform.Find("SkinModel") != null && !o.GetComponent<Renderer>().enabled, n + ": model not applied.");
            UnityEngine.Object.Destroy(o);
        }
        GameObject barrel = SpecialWeapons.Prim(PrimitiveType.Cube, "Turret barrel", new Vector3(0f, 0.5f, -80f), Vector3.one, Color.white);
        Require(!barrel.GetComponent<Renderer>().enabled, "Turret barrel not hidden.");
        UnityEngine.Object.Destroy(barrel);
        // Модель не должна искажаться сплюснутым родителем: турель на "плоском" примитиве сохраняет пропорции
        GameObject squashed = SpecialWeapons.Prim(PrimitiveType.Cylinder, "Turret", new Vector3(0f, 0.5f, -80f), new Vector3(1f, 0.1f, 1f), Color.white);
        Bounds qb = new Bounds(squashed.transform.position, Vector3.zero);
        foreach (Renderer r in squashed.transform.Find("SkinModel").GetComponentsInChildren<Renderer>()) qb.Encapsulate(r.bounds);
        Require(qb.size.y > 0.5f * Mathf.Max(qb.size.x, qb.size.z), "Skin distorted by squashed parent: " + qb.size.ToString("F2"));
        UnityEngine.Object.Destroy(squashed);
        ExperiencePickup.Spawn(new Vector3(5f, 0f, -90f), 1);
        DropPickup kitDrop = DropPickup.Spawn(new Vector3(8f, 0f, -90f), PickupKind.Health);
        DropPickup magnetDrop = DropPickup.Spawn(new Vector3(11f, 0f, -90f), PickupKind.Magnet);
        GameObject crystalObj = GameObject.Find("Experience Crystal");
        Require(crystalObj != null && crystalObj.transform.Find("SkinModel") != null, "Crystal has no model.");
        Require(kitDrop.transform.Find("SkinModel") != null && magnetDrop.transform.Find("SkinModel") != null, "Drops have no model.");
        UnityEngine.Object.Destroy(kitDrop.gameObject); UnityEngine.Object.Destroy(magnetDrop.gameObject);
        Pass("Crystal, repair kit and magnet get models; skin is not distorted by a squashed parent.");
        Pass("Boss and special-weapon objects (Saw, Mine, Turret, Drone) get models; boss collider kept.");
        }
        else Pass("No generated enemy/special models in the set: those checks skipped (waiting for external models).");

        // Внешние модели врагов: каждая запись Enemy_* — модель появляется, смотрит вперёд (голова впереди корпуса), анимация подключена.
        int external = 0;
        foreach (string type in new[] { "Weak", "Fast", "tank", "Shooter", "Charger", "Bomber", "Healer", "Splitter", "Summoner", "Sniper", "Mortar", "Teleporter", "Buffer", "Armored" })
        {
            if (ModelSkins.CurrentSet == null || ModelSkins.CurrentSet.Find("Enemy_" + type) == null || ModelSkins.CurrentSet.Find("Saw") != null) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy_" + type + ".prefab");
            GameObject e = UnityEngine.Object.Instantiate(prefab, new Vector3(0f, 0f, -60f), Quaternion.identity);
            ModelSkins.ApplyEnemy(e, prefab.name);
            Transform sm = e.transform.Find("SkinModel");
            Require(sm != null, type + ": no SkinModel.");
            Require(sm.GetComponent<ModelWalkAnimator>() != null, type + ": no ModelWalkAnimator.");
            ArenaLayout.Instance.Tint(e, Color.red);
            Require(sm.GetComponentInChildren<Renderer>().sharedMaterial.GetTexture("_BaseMap") != null, type + ": tint overwrote the model material.");
            Transform head = null, bodyPart = null;
            foreach (Transform t in sm.GetComponentsInChildren<Transform>()) { if (t.name == "Head") head = t; if (t.name == "Body") bodyPart = t; }
            if (head != null && bodyPart != null)
            {
                Vector3 hc = head.GetComponent<Renderer>().bounds.center, bc = bodyPart.GetComponent<Renderer>().bounds.center;
                Vector3 d = hc - bc; d.y = 0f;
                Require(Vector3.Dot(d.normalized, e.transform.forward) > 0.7f, type + ": head is not in front (model faces away).");
            }
            Bounds sb = new Bounds(sm.position, Vector3.zero);
            foreach (Renderer r in sm.GetComponentsInChildren<Renderer>()) sb.Encapsulate(r.bounds);
            Require(sb.size.y > 0.2f && sb.size.y < 4f, type + ": odd model height " + sb.size.y.ToString("F2"));
            Require(e.GetComponent<Collider>() != null, type + ": collider lost.");
            external++;
            UnityEngine.Object.Destroy(e);
        }
        if (ModelSkins.CurrentSet != null && ModelSkins.CurrentSet.Find("Boss") != null && ModelSkins.CurrentSet.Find("Saw") == null)
        {
            BossController bossExt = BossController.Create(new Vector3(0f, 0f, -70f), player.transform);
            Transform bsm = bossExt.transform.Find("SkinModel");
            Require(bsm != null && bsm.GetComponent<ModelWalkAnimator>() != null, "Boss: no external model/animator.");
            Require(bossExt.GetComponent<Collider>() != null, "Boss lost its collider.");
            Bounds bb = new Bounds(bsm.position, Vector3.zero);
            foreach (Renderer r in bsm.GetComponentsInChildren<Renderer>()) bb.Encapsulate(r.bounds);
            Require(bb.size.y > 1.5f && bb.size.y < 6f, "Boss: odd model height " + bb.size.y.ToString("F2"));
            UnityEngine.Object.Destroy(bossExt.gameObject);
            external++;
        }
        Pass("External enemy models (" + external + "): skinned, head forward, animator, sane size, collider kept.");

        // Дев-разблокировка: при включённом флаге доступно любое оружие.
        MetaProgress.DevUnlockAllWeapons = true;
        for (int i = 0; i < shooter.weapons.Length; i++) Require(MetaProgress.IsWeaponUnlocked(i), "Dev unlock: weapon " + i + " locked.");
        MetaProgress.DevUnlockAllWeapons = false;
        Pass("Dev unlock flag opens every weapon.");

        // Снимок арены с боковой камерой у ящиков и генератора
        var camGo = new GameObject("ArenaShotCam");
        Camera cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.transform.position = new Vector3(-8f, 22f, -22f);
        cam.transform.LookAt(new Vector3(-15f, 0f, 22f));
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
        File.WriteAllBytes("docs/art/Skin_play_arena.png", tex.EncodeToPNG());
        cam.targetTexture = null; RenderTexture.active = null;
        UnityEngine.Object.Destroy(camGo);

        // Бой: автострельба и враги в течение ~12 с — не должно быть ошибок, оружие продолжает стрелять.
        yield return Wait(12f);
        {
            // Снимок: Weak и Fast рядом (без ИИ), чтобы увидеть, какая модель у какого врага.
            Vector3 hp = player.transform.position;
            var pair = new List<GameObject>();
            string[] names = { "Weak", "Fast" };
            for (int i = 0; i < 2; i++)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy_" + names[i] + ".prefab");
                GameObject e = UnityEngine.Object.Instantiate(pf, new Vector3(hp.x + (i == 0 ? -2.2f : 2.2f), pf.transform.position.y, hp.z + 5f), Quaternion.identity);
                ModelSkins.ApplyEnemy(e, pf.name);
                foreach (EnemyAI ai0 in e.GetComponents<EnemyAI>()) ai0.enabled = false;
                pair.Add(e);
            }
            yield return null;
            Shot("docs/art/Skin_enemies_pair.png");
            foreach (GameObject g in pair) UnityEngine.Object.Destroy(g);
        }
        yield return Wait(30f);
        var census = new Dictionary<string, string>();
        foreach (EnemyAI ai in UnityEngine.Object.FindObjectsOfType<EnemyAI>())
        {
            Health h = ai.GetComponent<Health>();
            bool hasSkin = ai.transform.Find("SkinModel") != null;
            Transform sk = ai.transform.Find("SkinModel"); MeshFilter mfx = sk != null ? sk.GetComponentInChildren<MeshFilter>() : null; string key = ai.name + " model=" + (mfx != null ? mfx.sharedMesh.name + "/v" + mfx.sharedMesh.vertexCount + "/" + sk.GetComponentInChildren<Renderer>().sharedMaterial.name + "/hasHead=" + (sk.Find("Head") != null) + "/" + string.Join(",", System.Array.ConvertAll(sk.GetComponentsInChildren<Transform>(), t => t.name)) : "none") + " cube=" + (ai.GetComponent<Renderer>() != null && ai.GetComponent<Renderer>().enabled) + " scale=" + ai.transform.localScale.x.ToString("F2");
            census[key] = (census.ContainsKey(key) ? census[key] + "," : "") + (h != null ? h.maxHealth.ToString() : "?");
        }
        foreach (var kv in census) lines.Add("CENSUS " + kv.Key + " maxHP=" + kv.Value);
        Require(health.CurrentHP > 0, "Hero died.");
        Pass("12 s of combat with skins: no hero death.");
        Require(errorLogs == 0, "Error logs during play: " + errorLogs);
        Pass("No Error/Exception logs during the run.");
    }
}
