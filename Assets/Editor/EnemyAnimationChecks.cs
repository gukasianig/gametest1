using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Проверка анимаций врагов в Play Mode (тест-арена): колени при ходьбе, позы атак, снаряды «живого оружия»,
// взрыв подрывника, смерти по типам, разделение. Делает кадры крупным планом.
// Batch (Unity закрыт, без -quit): -batchmode -executeMethod EnemyAnimationChecks.RunBatch
// Отчёт: Logs/enemy-anim-results.txt, кадры: docs/art/EnemyAnim/*.png.
[InitializeOnLoad]
public static class EnemyAnimationChecks
{
    private const string Request = "Logs/enemy-anim.request";
    private const string Report = "Logs/enemy-anim-results.txt";
    private const string Frames = "docs/art/EnemyAnim";
    private static CheckRunner routine;
    private static readonly List<string> lines = new List<string>();
    private static double started;
    private static int errorLogs;
    private static Transform hero;
    private static Camera cam;

    static EnemyAnimationChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Request)) EditorApplication.delayCall += Begin;
            if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Tick;
        };
    }

    [MenuItem("Checks/Enemy animation checks")]
    public static void RunInEditor()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Request, "editor");
        EditorApplication.isPlaying = true;
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
        bool batch = File.ReadAllText(Request) == "batch";
        File.Delete(Request);
        lines.Clear();
        errorLogs = 0;
        Application.logMessageReceived += OnLog;
        started = EditorApplication.timeSinceStartup;
        routine = new CheckRunner(Checks());
        exitWhenDone = batch;
        EditorApplication.update += Tick;
    }

    private static bool exitWhenDone;

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception) { errorLogs++; lines.Add("LOG " + type + ": " + message + " | " + stack.Split('\n')[0]); }
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 240) throw new Exception("Check timed out.");
            if (!routine.MoveNext())
            {
                Require(errorLogs == 0, errorLogs + " error log(s) during the check.");
                Finish(true, "Enemy animation checks complete.");
            }
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Pass(string text) { lines.Add("PASS: " + text); File.WriteAllLines(Report, lines); }
    private static void Note(string text) { lines.Add("NOTE: " + text); File.WriteAllLines(Report, lines); }
    private static void Require(bool condition, string text) { if (!condition) throw new Exception(text); }

    private static void Finish(bool success, string text)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        lines.Add((success ? "PASS: " : "FAIL: ") + text);
        File.WriteAllLines(Report, lines);
        if (exitWhenDone) EditorApplication.Exit(success ? 0 : 1);
        else EditorApplication.isPlaying = false;
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

    // Ждать условия (не дольше seconds); результат — в flag.
    private static IEnumerator Until(Func<bool> condition, float seconds, bool[] flag)
    {
        float end = Time.time + seconds;
        double cap = EditorApplication.timeSinceStartup + 60;
        flag[0] = false;
        while (Time.time < end && EditorApplication.timeSinceStartup < cap)
        {
            if (PlayerProgression.Instance != null && PlayerProgression.Instance.Choosing) PlayerProgression.Instance.SelectOfferedUpgrade(0);
            if (condition()) { flag[0] = true; yield break; }
            yield return null;
        }
    }

    private static void Shot(string name, Vector3 focus, float distance = 9f)
    {
        cam.transform.position = focus + new Vector3(0f, distance, -distance * 0.75f);
        cam.transform.LookAt(focus + Vector3.up * 0.4f);
        var rt = new RenderTexture(960, 540, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(Frames, name + ".png"), tex.EncodeToPNG());
        cam.targetTexture = null;
        RenderTexture.active = null;
        UnityEngine.Object.Destroy(rt);
        UnityEngine.Object.Destroy(tex);
    }

    private static GameObject Spawn(string type, Vector3 offset)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy_" + type + ".prefab");
        Require(prefab != null, "Prefab missing: " + type);
        Vector3 position = hero.position + offset;
        GameObject e = UnityEngine.Object.Instantiate(prefab, new Vector3(position.x, 0f, position.z), Quaternion.LookRotation(-offset.normalized));
        ModelSkins.ApplyEnemy(e, prefab.name);
        float bottom = float.PositiveInfinity;
        foreach (Renderer r in e.GetComponentsInChildren<Renderer>()) bottom = Mathf.Min(bottom, r.bounds.min.y);
        if (!float.IsInfinity(bottom)) e.transform.position += Vector3.up * (0f - bottom);
        e.GetComponent<EnemyAI>().target = hero;
        return e;
    }

    private static Transform Part(GameObject enemy, string name)
    {
        Transform skin = enemy.transform.Find("SkinModel");
        if (skin == null) return null;
        foreach (Transform t in skin.GetComponentsInChildren<Transform>()) if (t.name == name) return t;
        return null;
    }

    private static int Count<T>() where T : UnityEngine.Object { return UnityEngine.Object.FindObjectsOfType<T>().Length; }

    private static void ClearEnemies()
    {
        foreach (EnemyAI ai in UnityEngine.Object.FindObjectsOfType<EnemyAI>()) UnityEngine.Object.Destroy(ai.gameObject);
        foreach (EnemyProjectile p in UnityEngine.Object.FindObjectsOfType<EnemyProjectile>()) UnityEngine.Object.Destroy(p.gameObject);
    }

    private static void Kill(GameObject enemy)
    {
        Health h = enemy.GetComponent<Health>();
        h.TakeDamage(h.CurrentHealth + 100000);
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        GameObject player = PlayerProgression.Instance.gameObject;
        hero = player.transform;
        player.GetComponent<StartMenu>().BeginTestArena();
        yield return null;
        Require(GameSession.Instance.State == GameRunState.Playing && GameSession.TestMode, "Test arena did not start.");
        player.GetComponent<PlayerHealth>().IncreaseMaxHealth(100000);
        player.GetComponent<AutoShoter>().enabled = false;   // герой не стреляет: враги живут, пока их не убьёт проверка
        Directory.CreateDirectory(Frames);
        cam = new GameObject("Enemy anim check camera").AddComponent<Camera>();
        cam.enabled = false;
        cam.fieldOfView = 45f;
        bool[] ok = new bool[1];

        // 1. Модели разрезаны: голени и челюсти на месте.
        string[] withJaw = { "Weak", "Fast", "tank", "Shooter", "Charger", "Summoner", "Mortar", "Armored" };
        string[] all = { "Weak", "Fast", "tank", "Shooter", "Charger", "Bomber", "Healer", "Splitter", "Summoner", "Sniper", "Mortar", "Teleporter", "Buffer", "Armored" };
        foreach (string type in all)
        {
            GameObject e = Spawn(type, new Vector3(0f, 0f, -40f));
            bool legs = Part(e, "Leg_FL") != null || Part(e, "Leg_L") != null || Part(e, "Leg_L1") != null;
            if (legs) Require(Part(e, "Shin_FL") != null || Part(e, "Shin_L") != null || Part(e, "Shin_L1") != null, type + ": legs without Shin parts (old FBX?).");
            if (Array.IndexOf(withJaw, type) >= 0) Require(Part(e, "Jaw") != null && Part(e, "Jaw").parent == Part(e, "Head"), type + ": no Jaw under Head.");
            UnityEngine.Object.Destroy(e);
        }
        yield return null;
        Pass("All 14 enemy models have Shin_* knee parts where they have legs; " + withJaw.Length + " have Jaw under Head.");

        // 2. Ходьба: колени сгибаются; кадр группы.
        var walkers = new List<GameObject>();
        string[] walkTypes = { "Weak", "Fast", "tank", "Armored", "Healer", "Buffer" };
        for (int i = 0; i < walkTypes.Length; i++)
            walkers.Add(Spawn(walkTypes[i], new Vector3(-7.5f + i * 3f, 0f, 11f)));
        yield return Wait(0.2f);
        var shinStart = new Dictionary<Transform, Quaternion>();
        foreach (GameObject w in walkers)
        {
            Transform shin = Part(w, "Shin_FL") ?? Part(w, "Shin_L");
            if (shin != null) shinStart[shin] = shin.localRotation;
        }
        // Колено прямое половину шага: смотрим наибольший изгиб за 0.8 с, а не два отдельных кадра.
        var maxBend = new Dictionary<Transform, float>();
        float until = Time.time + 0.8f;
        while (Time.time < until)
        {
            foreach (var pair in shinStart)
                if (pair.Key != null)
                    maxBend[pair.Key] = Mathf.Max(maxBend.TryGetValue(pair.Key, out float m) ? m : 0f, Quaternion.Angle(pair.Key.localRotation, pair.Value));
            yield return null;
        }
        int bent = 0;
        var detail = new List<string>();
        foreach (var pair in maxBend)
        {
            if (pair.Value > 8f) bent++;
            detail.Add(pair.Key.root.name.Replace("(Clone)", "") + " " + pair.Value.ToString("F0") + "°");
        }
        Require(bent >= shinStart.Count - 1 && bent > 0, "Knees do not bend while walking: " + string.Join(", ", detail));
        Shot("01-walk", hero.position + new Vector3(0f, 0f, 7f), 7f);
        Pass("Walking: knees bend on " + bent + "/" + shinStart.Count + " walkers (max bend: " + string.Join(", ", detail) + ").");

        // 3. Смерти: лёгкие разлетаются, тяжёлые валятся; трупы исчезают.
        Vector3 deathFocus = hero.position + new Vector3(0f, 0f, 6f);
        foreach (GameObject w in walkers) if (w != null) Kill(w);
        yield return null;
        int corpses = Count<EnemyCorpse>();
        Require(corpses == walkers.Count, "Corpses: " + corpses + " of " + walkers.Count);
        yield return Wait(0.12f);
        Shot("02-death-a", deathFocus, 7f);
        yield return Wait(0.3f);
        Shot("03-death-b", deathFocus, 7f);
        yield return Wait(2.2f);
        Require(Count<EnemyCorpse>() == 0, "Corpses stay after 2.6 s: " + Count<EnemyCorpse>());
        Pass("Deaths: " + corpses + " corpses (shatter/topple) played and removed within 2.6 s.");
        ClearEnemies();

        // 4. Стрелок: раздувает горло, плюёт кислотой.
        GameObject shooter = Spawn("Shooter", new Vector3(0f, 0f, 9f));
        EnemyAbility shooterAbility = shooter.GetComponent<EnemyAbility>();
        Transform throat = Part(shooter, "Throat");
        Vector3 throatRest = throat != null ? throat.localScale : Vector3.one;
        yield return Until(() => shooterAbility.Telegraphing, 6f, ok);
        Require(ok[0], "Shooter never attacked.");
        yield return Wait(0.45f);
        Require(throat == null || throat.localScale.x > throatRest.x * 1.25f, "Shooter throat does not swell while aiming.");
        Shot("04-shooter-aim", shooter.transform.position, 5f);
        yield return Until(() => UnityEngine.Object.FindObjectOfType<EnemyProjectile>() != null, 1f, ok);
        Require(ok[0], "Shooter fired nothing.");
        EnemyProjectile spit = UnityEngine.Object.FindObjectOfType<EnemyProjectile>();
        Require(spit.style == EnemyProjectile.Style.Acid && spit.GetComponent<TrailRenderer>() != null, "Shooter shot is not an acid glob with a trail.");
        yield return Wait(0.12f);
        Shot("05-shooter-spit", (shooter.transform.position + hero.position) * 0.5f, 6f);
        Pass("Shooter: throat swells while aiming, spits an acid glob with a trail.");
        ClearEnemies();

        // 5. Таран: замах (присед, пыль), рывок со следом.
        GameObject charger = Spawn("Charger", new Vector3(0f, 0f, 10f));
        EnemyAbility chargerAbility = charger.GetComponent<EnemyAbility>();
        yield return Until(() => chargerAbility.Telegraphing, 6f, ok);
        Require(ok[0], "Charger never attacked.");
        yield return Wait(0.5f);
        Shot("06-charger-windup", charger.transform.position, 5f);
        yield return Until(() => !chargerAbility.Telegraphing, 1f, ok);
        yield return Wait(0.18f);
        Transform trail = charger.transform.Find("Dash trail");
        Require(trail != null && trail.GetComponent<TrailRenderer>().emitting, "Charger dash has no trail.");
        Shot("07-charger-dash", charger.transform.position, 6f);
        Pass("Charger: windup with dust, dash with trail and puffs.");
        ClearEnemies();

        // 6. Снайпер: игла.
        GameObject sniper = Spawn("Sniper", new Vector3(0f, 0f, 15f));
        EnemyAbility sniperAbility = sniper.GetComponent<EnemyAbility>();
        yield return Until(() => sniperAbility.Telegraphing, 6f, ok);
        Require(ok[0], "Sniper never aimed.");
        yield return Wait(0.6f);
        Shot("08-sniper-aim", sniper.transform.position, 5f);
        yield return Until(() => UnityEngine.Object.FindObjectOfType<EnemyProjectile>() != null, 1.5f, ok);
        Require(ok[0] && UnityEngine.Object.FindObjectOfType<EnemyProjectile>().style == EnemyProjectile.Style.Quill, "Sniper shot is not a quill.");
        yield return Wait(0.08f);
        Shot("09-sniper-quill", (sniper.transform.position + hero.position) * 0.5f, 8f);
        Pass("Sniper: aims low and fires a bone quill.");
        ClearEnemies();

        // 7. Миномётчик: спора по дуге.
        GameObject mortar = Spawn("Mortar", new Vector3(0f, 0f, 14f));
        EnemyAbility mortarAbility = mortar.GetComponent<EnemyAbility>();
        yield return Until(() => mortarAbility.Telegraphing, 6f, ok);
        Require(ok[0], "Mortar never fired.");
        yield return Until(() => GameObject.Find("Mortar spore") != null, 1.2f, ok);
        Require(ok[0], "Mortar launched no spore shell.");
        yield return Wait(0.45f);
        Shot("10-mortar-arc", (mortar.transform.position + hero.position) * 0.5f, 10f);
        yield return Until(() => GameObject.Find("Mortar spore") == null, 1.5f, ok);
        Require(ok[0], "Mortar spore never landed.");
        Shot("11-mortar-impact", hero.position, 6f);
        Pass("Mortar: spore shell arcs to the ring and bursts.");
        ClearEnemies();

        // 8. Подрывник: раздувается, взрывается.
        GameObject bomber = Spawn("Bomber", new Vector3(0f, 0f, 5f));
        EnemyAbility bomberAbility = bomber.GetComponent<EnemyAbility>();
        yield return Until(() => bomberAbility.Telegraphing, 8f, ok);
        Require(ok[0], "Bomber never started to explode.");
        Vector3 bomberSize = bomber.transform.Find("SkinModel").localScale;
        yield return Wait(0.8f);
        Require(bomber.transform.Find("SkinModel").localScale.magnitude > bomberSize.magnitude * 1.1f, "Bomber does not swell.");
        Vector3 blast = bomber.transform.position;
        Shot("12-bomber-swell", blast, 5f);
        yield return Until(() => bomber == null, 1f, ok);
        Require(ok[0], "Bomber did not explode.");
        yield return null;
        int pieces = Count<FxPiece>();
        Require(pieces > 25 && Count<FxLight>() > 0 && Count<FxRing>() > 0, "Explosion too thin: " + pieces + " pieces.");
        Shot("13-explosion-a", blast, 6f);
        yield return Wait(0.15f);
        Shot("14-explosion-b", blast, 6f);
        yield return Wait(0.35f);
        Shot("15-explosion-c", blast, 6f);
        Pass("Bomber: swells and blinks, explosion with " + pieces + " pieces, light, rings, burst corpse.");
        yield return Wait(1.5f);
        ClearEnemies();

        // 9. Разделяющийся: половины разъезжаются, осколки вырастают.
        GameObject splitter = Spawn("Splitter", new Vector3(0f, 0f, 6f));
        yield return Wait(0.3f);
        Vector3 splitAt = splitter.transform.position;
        Kill(splitter);
        yield return null;
        var fragments = new List<GameObject>();
        foreach (EnemyAI ai in UnityEngine.Object.FindObjectsOfType<EnemyAI>()) if (ai.name == "Splitter fragment") fragments.Add(ai.gameObject);
        Require(fragments.Count == 2, "Splitter fragments: " + fragments.Count);
        Transform fragmentSkin = fragments[0].transform.Find("SkinModel");
        yield return Wait(0.08f);
        float early = fragmentSkin.lossyScale.magnitude;
        Shot("16-split-a", splitAt, 5f);
        yield return Wait(0.15f);
        Shot("17-split-b", splitAt, 5f);
        yield return Wait(0.45f);
        float late = fragmentSkin.lossyScale.magnitude;
        Shot("18-split-c", splitAt, 5f);
        Require(early < late * 0.3f, "Fragments do not grow in: " + early.ToString("F3") + " → " + late.ToString("F3"));
        Pass("Splitter: corpse halves part, fragments pop in (" + early.ToString("F2") + " → " + late.ToString("F2") + ").");
        ClearEnemies();

        // 10. Телепортёр: складывается и исчезает; смерть — падение.
        GameObject bat = Spawn("Teleporter", new Vector3(0f, 0f, 12f));
        EnemyAbility batAbility = bat.GetComponent<EnemyAbility>();
        yield return Until(() => batAbility.Telegraphing, 6f, ok);
        if (ok[0])
        {
            yield return Wait(0.45f);
            Shot("19-teleport-blink", bat.transform.position, 5f);
            Pass("Teleporter: blink pose during the telegraph.");
        }
        else Note("Teleporter did not blink within 6 s (range/cooldown); blink frame skipped.");
        yield return Wait(0.6f);
        Vector3 batAt = bat.transform.position;
        Kill(bat);
        yield return Wait(0.25f);
        Shot("20-bat-fall", batAt, 5f);
        ClearEnemies();

        // 11. Призыватель: каст и вылупление миньонов; попадание даёт вздрагивание без ошибок.
        GameObject brood = Spawn("Summoner", new Vector3(0f, 0f, 10f));
        EnemyAbility broodAbility = brood.GetComponent<EnemyAbility>();
        yield return Until(() => broodAbility.Telegraphing, 6f, ok);
        Require(ok[0], "Summoner never cast.");
        yield return Wait(0.5f);
        Shot("21-summoner-cast", brood.transform.position, 5f);
        yield return Wait(0.5f);
        Require(broodAbility.LiveMinions > 0, "Summoner made no minions.");
        brood.GetComponent<Health>().TakeDamage(1);
        yield return Wait(0.03f);
        Shot("22-summoner-flinch", brood.transform.position, 5f);
        Pass("Summoner: cast pose, minions pop in, flinch on hit.");
        ClearEnemies();
        yield return Wait(0.5f);

        UnityEngine.Object.Destroy(cam.gameObject);
        player.GetComponent<AutoShoter>().enabled = true;
    }
}
