using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Эволюция оружия (11.11.2026): условие (урон 5+ и парная карточка), карточка только в награде сундука,
// превращение автомата в «Пулемёт» (темп, пробивание, золотое свечение и пули). Кадры Logs/evolution-card.png, evolution-shots.png.
// Пакетный запуск: -batchmode -executeMethod EvolutionChecks.RunBatch (без -quit). Отчёт: Logs/evolution-results.txt.
[InitializeOnLoad]
public static class EvolutionChecks
{
    private const string Request = "Logs/evolution.request";
    private const string Report = "Logs/evolution-results.txt";
    private static CheckRunner routine;
    private static readonly List<string> lines = new List<string>();
    private static double started;

    static EvolutionChecks()
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
            if (EditorApplication.timeSinceStartup - started > 90) throw new Exception("Проверка не уложилась во время.");
            if (!routine.MoveNext()) Finish(true, "Проверки эволюции пройдены.");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Pass(string text) { lines.Add("ПРОЙДЕНО: " + text); File.WriteAllLines(Report, lines); }
    private static void Require(bool condition, string text) { if (!condition) throw new Exception(text); }

    private static void Finish(bool success, string text)
    {
        EditorApplication.update -= Tick;
        lines.Add((success ? "ПРОЙДЕНО: " : "ОШИБКА: ") + text);
        File.WriteAllLines(Report, lines);
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static IEnumerator WaitReal(double seconds)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < end) yield return null;
    }

    private static void Grant(PlayerProgression progression, UpgradeEffect effect, int times)
    {
        UpgradeDefinition card = progression.catalog.entries.Find(d => d != null && d.effect == effect);
        Require(card != null, "Нет карточки " + effect);
        for (int i = 0; i < times; i++) Require(progression.TestGrant(card), "Не выдалась карточка " + card.title);
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        GameObject hero = PlayerProgression.Instance.gameObject;
        yield return WaitReal(1.6);
        hero.GetComponent<StartMenu>().SendMessage("BeginGame", (object)1);   // автомат
        WaveManager waves = UnityEngine.Object.FindObjectOfType<WaveManager>();
        waves.StopSpawning();
        yield return WaitReal(0.4);
        foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>()) UnityEngine.Object.Destroy(enemy.gameObject);
        PlayerProgression progression = PlayerProgression.Instance;
        AutoShoter gun = hero.GetComponent<AutoShoter>();
        gun.enabled = false;
        Require(GameSession.Instance.State == GameRunState.Playing && gun.WeaponIndex == 1 && gun.CanEvolve, "Бой с автоматом не начался.");

        // 1. Условие: урон 5 рангов + «Темп стрельбы» 3 ранга.
        Require(progression.ReadyEvolution() == null, "Эволюция готова без карточек.");
        Grant(progression, UpgradeEffect.Damage, 5);
        Grant(progression, UpgradeEffect.AttackSpeed, 2);
        Require(progression.ReadyEvolution() == null, "Эволюция готова при темпе 2 из 3.");
        Grant(progression, UpgradeEffect.AttackSpeed, 1);
        UpgradeDefinition evo = progression.ReadyEvolution();
        Require(evo != null && evo.id == "evo_1", "Эволюция автомата не готова после урона 5 и темпа 3.");
        Pass("Условие: «Урон пуль» 5 и «Темп стрельбы» 3 → готова «" + evo.title + "» (при темпе 2 — нет).");

        // 2. В обычном выборе уровня эволюции нет.
        for (int i = 0; i < 40; i++)
        {
            progression.RollOffers();
            foreach (UpgradeDefinition offer in progression.Offered)
                Require(offer.effect != UpgradeEffect.Evolution, "Эволюция попала в обычный выбор уровня.");
        }
        Pass("В 40 обычных выборах уровня эволюции нет — только в сундуке.");

        // 3. Сундук: эволюция первой картой; выбор превращает автомат в «Пулемёт».
        float damageBefore = gun.ExactDamage;
        Require(progression.OfferChestReward(), "Окно сундука не открылось.");
        Require(progression.Offered.Count == 3 && progression.Offered[0] == evo, "Эволюция не на первом месте в сундуке.");
        yield return WaitReal(0.3);
        CaptureUi("evolution-card");
        Require(progression.SelectOfferedUpgrade(0), "Выбор эволюции не сработал.");
        yield return WaitReal(0.4);
        Require(gun.Evolved && progression.ReadyEvolution() == null && progression.Rank("evo_1") == 1, "Автомат не эволюционировал.");
        Require(GameSession.Instance.State == GameRunState.Playing, "После сундука бой не продолжился.");
        Transform muzzle = ModelSkins.FindMuzzle(hero.transform);
        Renderer weapon = muzzle != null ? muzzle.parent.GetComponentInChildren<Renderer>() : null;
        Require(weapon != null && weapon.material.IsKeywordEnabled("_EMISSION"), "У модели оружия нет свечения.");
        Pass("Сундук: эволюция первой картой (кадр Logs/evolution-card.png); выбран «Пулемёт», модель светится, урон " +
             damageBefore.ToString("0.0") + " (у Пулемёта прибавка — темп и пробивание).");

        // 4. Золотые пули.
        waves.wave = Mathf.Max(waves.wave, 6);
        GameObject target = waves.SpawnElite();
        Require(target != null, "Цель не появилась.");
        target.GetComponent<EnemyAI>().MovementLocked = true;
        target.transform.position = hero.transform.position + hero.transform.forward * 7f;
        target.GetComponent<Health>().SetHealth(100000);
        gun.enabled = true;
        int golden = 0;
        for (int i = 0; i < 30 && golden == 0; i++)
        {
            yield return WaitReal(0.05);
            foreach (Bullet b in UnityEngine.Object.FindObjectsOfType<Bullet>())
                foreach (Renderer r in b.GetComponentsInChildren<Renderer>())
                    if (!(r is TrailRenderer) && r.sharedMaterial != null && r.sharedMaterial.color.r > 0.9f && r.sharedMaterial.color.b < 0.4f) golden++;
        }
        Require(golden > 0, "Нет золотых пуль после эволюции.");
        CaptureWorld("evolution-shots", hero.transform.position + Vector3.up * 5f - hero.transform.forward * 4f,
            hero.transform.position + hero.transform.forward * 3.5f + Vector3.up * 0.8f);
        Pass("Пулемёт стреляет золотыми пулями (" + golden + "); кадр Logs/evolution-shots.png.");

        // 5. Спецоружие: условие у ракетницы и веер снарядов после эволюции; у остальных — выстрел без ошибок.
        gun.enabled = false;
        foreach (WeaponProjectile shot in UnityEngine.Object.FindObjectsOfType<WeaponProjectile>()) UnityEngine.Object.Destroy(shot.gameObject);
        var results = new List<string>();
        foreach (WeaponKind kind in new[] { WeaponKind.Rocket, WeaponKind.Ice, WeaponKind.Boomerang, WeaponKind.Arc, WeaponKind.Flame, WeaponKind.Rail })
        {
            int index = Array.FindIndex(gun.weapons, w => w != null && w.kind == kind && w.IsSelectable);
            Require(index >= 0 && gun.SelectWeapon(index), "Нет оружия " + kind);
            yield return null;
            Require(gun.CanEvolve && !gun.Evolved && gun.EvolutionSlot >= 4, "У " + kind + " нет эволюции.");
            if (kind == WeaponKind.Rocket)
            {
                // Урон уже 5 рангов, темп 3 — эволюция ракетницы готова.
                UpgradeDefinition card = progression.ReadyEvolution();
                Require(card != null && card.id == "evo_" + gun.EvolutionSlot, "Эволюция ракетницы не готова при уроне 5 и темпе 3.");
            }
            gun.Evolve();
            Require(gun.Evolved, kind + " не эволюционировала.");
            SpecialWeapons main = Array.Find(hero.GetComponents<SpecialWeapons>(), s => !s.support);
            int before = kind == WeaponKind.Boomerang ? UnityEngine.Object.FindObjectsOfType<WeaponSaw>().Length
                : UnityEngine.Object.FindObjectsOfType<WeaponProjectile>().Length;
            main.Fire(10, target.transform);
            yield return null;
            int after = kind == WeaponKind.Boomerang ? UnityEngine.Object.FindObjectsOfType<WeaponSaw>().Length
                : UnityEngine.Object.FindObjectsOfType<WeaponProjectile>().Length;
            int made = after - before;
            if (kind == WeaponKind.Rocket || kind == WeaponKind.Ice) Require(made == 3, kind + ": снарядов за выстрел " + made + " вместо 3.");
            if (kind == WeaponKind.Boomerang) Require(made == 2, "Пил за бросок " + made + " вместо 2.");
            results.Add(AutoShoter.Evolutions[gun.EvolutionSlot].title + (made > 0 ? " (" + made + ")" : ""));
            foreach (WeaponProjectile shot in UnityEngine.Object.FindObjectsOfType<WeaponProjectile>()) UnityEngine.Object.Destroy(shot.gameObject);
            foreach (WeaponSaw saw in UnityEngine.Object.FindObjectsOfType<WeaponSaw>()) UnityEngine.Object.Destroy(saw.gameObject);
            yield return WaitReal(0.2);
        }
        Pass("Спецоружие: ракетница готова к эволюции при уроне 5 и темпе 3; выстрелы после эволюции — " + string.Join(", ", results) + ".");
    }

    private static void CaptureUi(string name)
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        var switched = new List<Canvas>();
        foreach (Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            switched.Add(canvas);
        }
        Render(camera, name);
        foreach (Canvas canvas in switched) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
    }

    private static void CaptureWorld(string name, Vector3 from, Vector3 look)
    {
        var go = new GameObject("Evolution Check Camera");
        Camera camera = go.AddComponent<Camera>();
        camera.fieldOfView = 45f;
        go.transform.position = from;
        go.transform.rotation = Quaternion.LookRotation(look - from, Vector3.up);
        Render(camera, name);
        UnityEngine.Object.Destroy(go);
    }

    private static void Render(Camera camera, string name)
    {
        var texture = new RenderTexture(1280, 720, 24);
        RenderTexture previous = camera.targetTexture;
        camera.targetTexture = texture;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = texture;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        image.Apply();
        File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = previous;
        UnityEngine.Object.Destroy(image);
        texture.Release();
    }
}
