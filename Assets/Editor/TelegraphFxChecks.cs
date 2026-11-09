using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Короткая проверка кругов-предупреждений врагов и взрыва мортиры в GreenArenaPlay: круг полупрозрачный и лежит
// на земле, внутри растёт заливка, снаряд мортиры взрывается. Кадры Logs/telegraph-*.png, отчёт Logs/telegraph-results.txt.
// Batch: -batchmode -projectPath . -executeMethod TelegraphFxChecks.RunBatch (без -quit).
[InitializeOnLoad]
public static class TelegraphFxChecks
{
    private const string Request = "Logs/telegraph.request";
    private const string Report = "Logs/telegraph-results.txt";
    private static CheckRunner routine;
    private static double started;
    private static readonly List<string> lines = new List<string>();

    static TelegraphFxChecks()
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

    // Только выстрелы автомата крупным планом: пули вылетают из дула и летят горизонтально (Logs/shots-*.png).
    public static void RunShotsBatch()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Request, "shots");
        EditorSceneManager.OpenScene("Assets/Scenes/GreenArenaPlay.unity");
        EditorApplication.isPlaying = true;
    }

    private static void Begin()
    {
        bool shots = File.ReadAllText(Request).Trim() == "shots";
        File.Delete(Request);
        lines.Clear();
        started = EditorApplication.timeSinceStartup;
        routine = new CheckRunner(shots ? Shots() : Checks());
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 120) throw new Exception("Telegraph check timed out");
            if (!routine.MoveNext()) Finish(true, "Telegraph and mortar effects check complete");
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    private static void Finish(bool ok, string text)
    {
        EditorApplication.update -= Tick;
        lines.Add((ok ? "PASS " : "FAIL ") + text);
        File.WriteAllLines(Report, lines);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static void Check(bool condition, string text) { if (!condition) throw new Exception(text); }

    private static IEnumerator Wait(float seconds)
    {
        float until = Time.time + seconds;
        while (Time.time < until) yield return null;
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        StartMenu menu = PlayerProgression.Instance.GetComponent<StartMenu>();
        menu.SendMessage("BeginGame", (object)0);
        yield return Wait(0.5f);
        WaveManager waves = UnityEngine.Object.FindObjectOfType<WaveManager>();
        if (waves != null) waves.StopSpawning();
        foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>()) UnityEngine.Object.Destroy(enemy.gameObject);
        PlayerHealth heroHealth = PlayerProgression.Instance.GetComponent<PlayerHealth>();
        Transform hero = PlayerProgression.Instance.transform;
        PlayMovement movement = hero.GetComponent<PlayMovement>();
        if (movement != null) movement.enabled = false;
        // Герой не стреляет: иначе он убивает подрывника раньше взрыва (убитый подрывник не взрывается — так задумано).
        hero.GetComponent<AutoShoter>().enabled = false;

        foreach (string kind in new[] { "Mortar", "Bomber" })
        {
            Vector3 offset = kind == "Mortar" ? hero.forward * 9f + hero.right * 3f : hero.right * 2.5f;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy_" + kind + ".prefab");
            Check(prefab != null, "No prefab " + kind);
            Vector3 point = hero.position + offset;
            point.y = GroundHeight.At(point) + 0.5f;
            GameObject enemy = UnityEngine.Object.Instantiate(prefab, point, Quaternion.identity);
            EnemyAI ai = enemy.GetComponent<EnemyAI>();
            ai.target = hero;
            ai.MovementLocked = true;
            EnemyAbility ability = enemy.GetComponent<EnemyAbility>();
            yield return null;
            yield return null;
            double ready = EditorApplication.timeSinceStartup + 5;
            while (!ability.Ready) { Check(EditorApplication.timeSinceStartup < ready, kind + ": ability did not start"); yield return null; }
            string method = kind == "Mortar" ? "Mortar" : "Explode";
            var start = (IEnumerator)typeof(EnemyAbility).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ability, null);
            heroHealth.GrantInvulnerability(5f);
            ability.StartCoroutine(start);
            yield return Wait(kind == "Mortar" ? 0.9f : 0.7f);
            Check(ability.Telegraphing, kind + ": no telegraph ring");
            TelegraphFill fill = UnityEngine.Object.FindObjectOfType<TelegraphFill>();
            Check(fill != null && fill.transform.localScale.x > 0.5f, kind + ": telegraph fill is not growing");
            LineRenderer ring = ability.GetComponentInChildren<LineRenderer>();
            Vector3 p = ring.GetPosition(0);
            float above = p.y - GroundHeight.At(p);
            Check(above > 0.05f && above < 0.3f, kind + ": ring is not on the ground (" + above.ToString("0.00") + " m)");
            Check(ring.sharedMaterial.renderQueue >= 3000, kind + ": ring material is not transparent");
            Capture("Logs/telegraph-" + kind.ToLower() + "-warning.png");
            lines.Add("PASS " + kind + ": ring " + above.ToString("0.00") + " m above ground, transparent, fill " + fill.transform.localScale.x.ToString("0.0") + " m");
            File.WriteAllLines(Report, lines);
            float until = Time.time + 1.2f;
            bool exploded = false;
            while (Time.time < until)
            {
                // Огненные шары взрыва — FxPiece; ловим кадр, когда они появились.
                if (!exploded && UnityEngine.Object.FindObjectsOfType<FxPiece>().Length > 6)
                {
                    exploded = true;
                    yield return null;
                    yield return null;
                    Capture("Logs/telegraph-" + kind.ToLower() + "-blast.png");
                }
                yield return null;
            }
            Check(exploded, kind + ": no explosion pieces after impact (enemy alive: " + (enemy != null) + ", telegraph: " + (enemy != null && ability.Telegraphing) + ", pieces: " + UnityEngine.Object.FindObjectsOfType<FxPiece>().Length + ", state: " + GameSession.Instance.State + ")");
            Check(UnityEngine.Object.FindObjectOfType<TelegraphFill>() == null, kind + ": fill left after the attack");
            lines.Add("PASS " + kind + ": explosion seen, fill removed");
            File.WriteAllLines(Report, lines);
            if (enemy != null) UnityEngine.Object.Destroy(enemy);
            yield return Wait(0.8f);
        }

        // Рикошет вместе с пробиванием: враг вплотную впереди, второй — в стороне (не на линии выстрела).
        // Раньше пробивание тратилось первым, пуля уходила в пустоту, второй враг не получал урона.
        AutoShoter shooter = hero.GetComponent<AutoShoter>();
        Vector3 basePoint = hero.position + hero.forward * 1.6f;
        Health near = Dummy(basePoint);
        Health side = Dummy(hero.position + hero.forward * 2.5f + hero.right * 3.5f);
        yield return Wait(0.2f);
        int sideBefore = side.CurrentHealth;
        var bulletObject = UnityEngine.Object.Instantiate(shooter.weapons[1].bulletPrefab,
            new Vector3(hero.position.x, near.transform.position.y, hero.position.z) + hero.forward * 0.3f, Quaternion.LookRotation(hero.forward));
        Bullet bullet = bulletObject.GetComponent<Bullet>();
        bullet.speed = 22f;
        bullet.lifeTime = 0.55f;
        bullet.damage = 10;
        bullet.pierce = 1;
        bullet.ricochet = 1;
        bullet.SetOwner(hero);
        yield return Wait(0.9f);
        Check(near.CurrentHealth < near.maxHealth, "Ricochet: the close enemy was not hit");
        Check(side.CurrentHealth < sideBefore, "Ricochet with pierce: the side enemy was not hit");
        lines.Add("PASS Pierce + ricochet: close enemy hit, bullet bounced to the side enemy (" + sideBefore + " -> " + side.CurrentHealth + ")");
        File.WriteAllLines(Report, lines);
        UnityEngine.Object.Destroy(side.gameObject);

        // Огнемёт: струя частиц из сопла, без старой сферы.
        int flameIndex = Array.FindIndex(shooter.weapons, w => w != null && w.kind == WeaponKind.Flame);
        Check(flameIndex >= 0 && shooter.SelectWeapon(flameIndex), "Cannot select the flamethrower");
        shooter.enabled = true;
        near.transform.position = hero.position + hero.forward * 3f;
        yield return Wait(0.7f);
        FlameJet jet = UnityEngine.Object.FindObjectOfType<FlameJet>();
        Check(jet != null && jet.Emitting, "Flamethrower: no flame jet");
        int particles = 0;
        foreach (ParticleSystem ps in jet.GetComponentsInChildren<ParticleSystem>()) particles += ps.particleCount;
        Check(particles > 30, "Flamethrower: too few flame particles " + particles);
        Capture("Logs/flame-jet.png");
        lines.Add("PASS Flamethrower: particle jet with " + particles + " particles (frame Logs/flame-jet.png)");
        File.WriteAllLines(Report, lines);

        // Склон: пятно взрыва, кольцо и заливка ложатся по наклону и не уходят краем в землю.
        Vector3 slope = Vector3.zero;
        float steepest = 1f;
        for (int x = -60; x <= 60; x += 3)
            for (int z = -60; z <= 60; z += 3)
            {
                Vector3 p = hero.position + new Vector3(x, 0f, z);
                float up = GroundHeight.Normal(p).y;
                if (up < steepest && up > 0.8f) { steepest = up; slope = p; }
            }
        Check(steepest < 0.985f, "No sloped ground found near the hero");
        slope.y = GroundHeight.At(slope);
        EnemyFx.GroundSplat(slope, 1.6f, new Color(0.45f, 0.3f, 0.2f), 6f);
        EnemyFx.Ring(slope + Vector3.up * 0.1f, 2.6f, new Color(1f, 0.55f, 0.3f), 6f, 0.35f);
        yield return Wait(0.4f);
        // Край пятна (диаметр 3,2 м) над землёй во всех направлениях.
        GameObject splat = null;
        foreach (FxPiece piece in UnityEngine.Object.FindObjectsOfType<FxPiece>()) if (piece.flat) splat = piece.gameObject;
        Check(splat != null, "Slope splat missing");
        float worst = 99f;
        for (int i = 0; i < 16; i++)
        {
            Vector3 edge = splat.transform.TransformPoint(new Vector3(Mathf.Cos(i * Mathf.PI / 8f), 0.5f, Mathf.Sin(i * Mathf.PI / 8f)) * 0.48f);
            worst = Mathf.Min(worst, edge.y - GroundHeight.At(edge));
        }
        Check(worst > -0.02f, "Slope splat edge is under the ground by " + (-worst).ToString("0.00") + " m");
        Camera view = Camera.main;
        view.GetComponent<TopDownCamera>().enabled = false;
        view.transform.SetPositionAndRotation(slope + new Vector3(0f, 9f, -5f), Quaternion.LookRotation(new Vector3(0f, -9f, 5f)));
        Capture("Logs/slope-splat.png");
        lines.Add("PASS Slope " + (Mathf.Acos(steepest) * Mathf.Rad2Deg).ToString("0") + "°: splat edge " + worst.ToString("0.00") + " m above ground (frame Logs/slope-splat.png)");
        File.WriteAllLines(Report, lines);
    }

    private static IEnumerator Shots()
    {
        yield return null;
        MetaProgress.UseMemory();
        PlayerProgression.Instance.GetComponent<StartMenu>().SendMessage("BeginGame", (object)1);
        yield return Wait(0.5f);
        UnityEngine.Object.FindObjectOfType<WaveManager>().StopSpawning();
        foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>()) UnityEngine.Object.Destroy(enemy.gameObject);
        Transform hero = PlayerProgression.Instance.transform;
        hero.GetComponent<PlayerHealth>().GrantInvulnerability(100f);
        PlayMovement movement = hero.GetComponent<PlayMovement>();
        if (movement != null) movement.enabled = false;
        Dummy(hero.position + hero.forward * 7f + hero.right * 2f);
        yield return Wait(1.2f);
        Camera view = Camera.main;
        view.GetComponent<TopDownCamera>().enabled = false;
        Vector3 muzzle;
        Check(ModelSkins.TryGetMuzzle(out muzzle), "No muzzle");
        // Сбоку-сверху от героя: видно ствол и вылет пуль.
        view.transform.SetPositionAndRotation(hero.position - hero.right * 3.2f + hero.forward * 1.5f + Vector3.up * 2.6f, Quaternion.identity);
        view.transform.LookAt(hero.position + hero.forward * 2.5f + Vector3.up * 1f);
        float worstHeight = 0f;
        int seen = 0;
        for (int i = 0; i < 3; i++)
        {
            yield return Wait(0.07f);
            Capture("Logs/shots-" + i + ".png");
            ModelSkins.TryGetMuzzle(out muzzle);
            foreach (Bullet bullet in UnityEngine.Object.FindObjectsOfType<Bullet>())
            {
                seen++;
                worstHeight = Mathf.Max(worstHeight, Mathf.Abs(bullet.transform.position.y - muzzle.y));
            }
        }
        lines.Add("Bullets seen " + seen + ", max height drift from muzzle " + worstHeight.ToString("0.00") + " m, muzzle " + muzzle.ToString("0.00") + " hero " + hero.position.ToString("0.00"));
        File.WriteAllLines(Report, lines);
    }

    private static Health Dummy(Vector3 position)
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = "Fx check target";
        item.tag = "Enemy";
        position.y = GroundHeight.At(position) + 0.5f;
        item.transform.position = position;
        Health health = item.AddComponent<Health>();
        health.SetHealth(10000);
        health.experienceReward = 0;
        item.AddComponent<EnemyAI>().MovementLocked = true;
        return health;
    }

    private static void Capture(string path)
    {
        Camera camera = Camera.main;
        var texture = new RenderTexture(1280, 720, 24);
        RenderTexture previous = camera.targetTexture;
        camera.targetTexture = texture;
        camera.Render();
        RenderTexture.active = texture;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = previous;
        RenderTexture.active = null;
        UnityEngine.Object.Destroy(image);
        texture.Release();
    }
}
