using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Кооператив, этап 1 (11.11.2026): второй герой, предел разброса, камера, цели врагов, подъём поверженного, поражение.
// Batch: -batchmode -executeMethod CoopChecks.RunBatch (без -quit). Отчёт: Logs/coop-results.txt, кадр Logs/coop-split.png.
[InitializeOnLoad]
public static class CoopChecks
{
    private const string Request = "Logs/coop.request";
    private const string Report = "Logs/coop-results.txt";
    private static CheckRunner routine;
    private static readonly List<string> lines = new List<string>();
    private static double started;

    static CoopChecks()
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
            if (EditorApplication.timeSinceStartup - started > 120) throw new Exception("Check timed out.");
            if (!routine.MoveNext()) Finish(true, "Coop checks complete.");
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Pass(string text) { lines.Add("PASS: " + text); File.WriteAllLines(Report, lines); }
    private static void Require(bool condition, string text) { if (!condition) throw new Exception(text); }

    private static void Finish(bool success, string text)
    {
        EditorApplication.update -= Tick;
        CoopMode.Players = 1;
        lines.Add((success ? "PASS: " : "FAIL: ") + text);
        File.WriteAllLines(Report, lines);
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static IEnumerator WaitReal(double seconds)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < end) yield return null;
    }

    private static void Place(PlayerHealth hero, Vector3 position)
    {
        position.y = hero.transform.position.y;
        Rigidbody body = hero.GetComponent<Rigidbody>();
        body.position = position;
        hero.transform.position = position;
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        GameObject first = PlayerProgression.Instance.gameObject;

        // 0. Главное меню: «Кооператив» включает двух игроков, «Старт» — одного.
        yield return WaitReal(1.6); // появление меню (MenuIntro)
        GameObject coopButton = GameObject.Find("Кооператив"), startButton = GameObject.Find("Старт");
        Require(coopButton != null && startButton != null, "Menu buttons Старт/Кооператив missing.");
        CaptureFrame("coop-menu");
        coopButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return WaitReal(0.3);
        Require(CoopMode.Players == 2, "Coop button did not select 2 players.");
        CaptureFrame("coop-weapons");
        startButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Require(CoopMode.Players == 1, "Start button did not select 1 player.");
        coopButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Pass("Menu: «Кооператив» → 2 players, «Старт» → 1; frames Logs/coop-menu.png, coop-weapons.png.");

        // 0б. Этап 4: оружие выбирают по очереди (И1 — автомат, И2 — пистолет); «Назад» у И2 возвращает к И1.
        WeaponSelectScreen screen = first.GetComponent<StartMenu>().WeaponScreen;
        Require(screen.Picker == 0, "Weapon screen should start with player 1.");
        screen.Select(1);
        screen.Confirm();
        Require(screen.Picker == 1 && GameSession.Instance.State == GameRunState.Menu, "Player 1 pick should hand over to player 2.");
        CaptureFrame("coop-weapons-p2");
        first.GetComponent<StartMenu>().ShowWeapons();
        Require(screen.Picker == 0, "Reopened weapon screen should start with player 1 again.");
        screen.Select(1);
        screen.Confirm();
        screen.Select(0);
        screen.Confirm();
        yield return WaitReal(0.3);
        Require(GameSession.Instance.State == GameRunState.Playing, "Run did not start after player 2 picked: " + GameSession.Instance.State);
        Require(CoopMode.SecondWeapon == 0, "Second weapon not stored: " + CoopMode.SecondWeapon);
        WaveManager waves = UnityEngine.Object.FindObjectOfType<WaveManager>();
        if (waves != null) waves.StopSpawning();
        yield return WaitReal(0.6);

        // 1. Второй герой: копия без «менеджеров», своё управление, то же оружие.
        Require(CoopMode.Heroes.Count == 2 && CoopMode.Active, "Expected 2 heroes, got " + CoopMode.Heroes.Count);
        PlayerHealth p1 = first.GetComponent<PlayerHealth>();
        PlayerHealth p2 = CoopMode.Partner(p1);
        Require(p2 != null && p2.name == "Player 2", "Player 2 missing.");
        Require(p2.GetComponent<PlayMovement>().playerIndex == 1, "Player 2 index.");
        Require(p2.GetComponent<StartMenu>() == null && p2.GetComponent<GameSession>() == null &&
            p2.GetComponent<CombatHud>() == null &&
            p2.GetComponent<ArenaEvents>() == null, "Player 2 kept session components.");
        Require(PlayerProgression.Instance.gameObject == first && GameSession.Instance.gameObject == first, "Singletons moved to player 2.");
        Require(p2.GetComponent<AutoShoter>().enabled && p2.CurrentHP == p2.maxHP && p2.maxHP > 0, "Player 2 weapon/health: " + p2.CurrentHP + "/" + p2.maxHP);
        Require(GameObject.Find("Coop HUD P2") != null, "Player 2 panel missing.");
        Require(p2.GetComponent<AutoShoter>().WeaponFx != p1.GetComponent<AutoShoter>().WeaponFx || p1.GetComponent<AutoShoter>().WeaponFx == null,
            "Both heroes share one weapon animation.");
        AutoShoter s1 = p1.GetComponent<AutoShoter>(), s2 = p2.GetComponent<AutoShoter>();
        // WeaponData — сериализуемый класс: у копии свой массив, сравниваем с записью своего же массива.
        Require(s1.baseWeapon == s1.weapons[1] && s2.baseWeapon == s2.weapons[0],
            "Weapons: P1 " + WeaponSelectScreen.Title(s1.baseWeapon, 1) + ", P2 " + WeaponSelectScreen.Title(s2.baseWeapon, 0));
        Require(s1.TryGetMuzzle(out Vector3 mz1) && s2.TryGetMuzzle(out Vector3 mz2) &&
            Vector3.Distance(mz1, p1.transform.position) < 2f && Vector3.Distance(mz2, p2.transform.position) < 2f,
            "Muzzles: each hero must shoot from own weapon.");
        Pass("Weapons picked in turn: player 1 «" + WeaponSelectScreen.Title(s1.baseWeapon, 1) + "», player 2 «" +
            WeaponSelectScreen.Title(s2.baseWeapon, 0) + "» (frame Logs/coop-weapons-p2.png).");
        Pass("Player 2 spawned " + WeaponTargets.Flat(p1.transform.position, p2.transform.position).ToString("0.0") +
            " m away, HP " + p2.CurrentHP + ", own panel, no session components.");

        // Без стрельбы и опыта: окно карточек не должно остановить проверку.
        foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>()) UnityEngine.Object.Destroy(enemy.gameObject);
        p1.GetComponent<AutoShoter>().enabled = false;
        p2.GetComponent<AutoShoter>().enabled = false;
        PlayMovement m1 = p1.GetComponent<PlayMovement>(), m2 = p2.GetComponent<PlayMovement>();

        // 1б. Этап 2: свой прогресс и бонусы у каждого героя.
        PlayerProgression g1 = p1.GetComponent<PlayerProgression>(), g2 = p2.GetComponent<PlayerProgression>();
        Require(g2 != null && g2.Secondary && !g1.Secondary && PlayerProgression.Instance == g1, "Player 2 progression / singleton.");
        Require(g1.Stats != g2.Stats && CombatStats.Primary == g1.Stats, "Heroes share one bonus set.");
        UpgradeDefinition armor = g2.catalog.entries.Find(d => d != null && d.effect == UpgradeEffect.Armor);
        Require(armor != null && g2.TestGrant(armor), "Armor card not granted to player 2.");
        Require(g2.Stats.Armor > 0f && Mathf.Approximately(g1.Stats.Armor, 0f) && g1.Rank(armor.id) == 0,
            "Armor leaked between heroes: P1 " + g1.Stats.Armor + ", P2 " + g2.Stats.Armor);
        int xp1 = g1.Experience, xp2 = g2.Experience;
        ExperiencePickup.Spawn(p2.transform.position + Vector3.forward * 1.2f, 3);
        yield return WaitReal(1.0);
        Require(g2.Experience == xp2 + 3 && g1.Experience == xp1, "Crystal near player 2: P1 xp " + g1.Experience + ", P2 xp " + g2.Experience);
        Pass("Own progression: armor card only on player 2 (" + Mathf.RoundToInt(g2.Stats.Armor * 100f) + "%), crystal near player 2 went to player 2.");

        // 1в. Этап 3: оба набрали уровень — два окна сразу, без паузы, у игроков разные карточки, переброс у каждого свой.
        g1.AddExperience(g1.ExperienceRequired - g1.Experience);
        g2.AddExperience(g2.ExperienceRequired - g2.Experience);
        yield return WaitReal(0.3);
        Require(g1.CoopWindow && g2.CoopWindow, "Both coop windows should be open: " + g1.Choosing + "/" + g2.Choosing);
        Require(GameSession.Instance.State == GameRunState.Playing && Time.timeScale > 0f, "Coop level-up paused the game: " + GameSession.Instance.State);
        foreach (UpgradeDefinition offer in g2.Offered)
            Require(!new List<UpgradeDefinition>(g1.Offered).Contains(offer), "Same card offered to both players: " + offer.id);
        int rerolls1 = g1.RerollsLeft;
        Require(g2.Reroll() && g2.RerollsLeft == 1 && g1.RerollsLeft == rerolls1, "Player 2 reroll.");
        foreach (UpgradeDefinition offer in g2.Offered)
            Require(!new List<UpgradeDefinition>(g1.Offered).Contains(offer), "Reroll gave player 2 a card of player 1: " + offer.id);
        Vector3 walkFrom = p1.transform.position;
        m1.CheckInput = Vector2.up;
        yield return WaitReal(0.6);
        m1.CheckInput = Vector2.zero;
        Require(WeaponTargets.Flat(walkFrom, p1.transform.position) > 1f, "Player 1 cannot move while choosing.");
        CaptureFrame("coop-cards");
        UpgradeDefinition pick = g2.Offered[0];
        int before2 = g2.Rank(pick.id), before1 = g1.Rank(pick.id);
        Require(g2.SelectOfferedUpgrade(0), "Player 2 pick failed.");
        Require(g1.SelectOfferedUpgrade(0), "Player 1 pick failed.");
        yield return WaitReal(0.3);
        Require(g2.Rank(pick.id) == before2 + 1 && g1.Rank(pick.id) == before1 && !g1.Choosing && !g2.Choosing &&
            GameSession.Instance.State == GameRunState.Playing && Time.timeScale > 0f, "Coop picks went wrong: " + pick.id);
        Pass("Level-up without pause: two windows at once with different cards (frame Logs/coop-cards.png), player 1 walks while choosing, " +
            "player 2 reroll does not spend player 1's; player 2 took «" + pick.title + "» only for player 2; levels " + g1.Level + " / " + g2.Level + ".");

        // 2. Предел разброса и камера: игрок 2 уходит вправо 7 с, игрок 1 стоит.
        float cameraHeight = Camera.main.transform.position.y;
        m1.CheckInput = Vector2.zero;
        m2.CheckInput = Vector2.right;
        yield return WaitReal(7.0);
        m2.CheckInput = Vector2.zero;
        yield return WaitReal(1.2);
        float spread = WeaponTargets.Flat(p1.transform.position, p2.transform.position);
        Require(spread <= CoopMode.MaxSpread + 1f, "Spread above the leash: " + spread);
        CoopMode.TryFrame(out Vector3 centre, out _);
        float cameraOff = WeaponTargets.Flat(Camera.main.transform.position, centre);
        float lift = Camera.main.transform.position.y - cameraHeight;
        Require(lift > 1f, "Camera did not zoom out: +" + lift);
        CaptureFrame("coop-split");
        Pass("Leash: spread " + spread.ToString("0.0") + " m (limit " + CoopMode.MaxSpread + "); camera +" + lift.ToString("0.0") +
            " m higher, " + cameraOff.ToString("0.0") + " m from the centre (horizontal, includes the tilt offset); frame Logs/coop-split.png.");

        // 3. Враг идёт к ближайшему живому герою.
        GameObject dummy = new GameObject("Coop Dummy Enemy");
        dummy.transform.position = p2.transform.position + Vector3.forward * 3f;
        EnemyAI ai = dummy.AddComponent<EnemyAI>();
        ai.target = p1.transform;
        yield return WaitReal(1.0);
        Require(ai.target == p2.transform, "Enemy kept the far hero as target.");
        UnityEngine.Object.Destroy(dummy);
        Pass("Enemy near player 2 retargeted from player 1 to player 2.");

        // 4. Поверженный герой не проигрывает бой, напарник поднимает его за ~3 с.
        p2.TakeDamage(100000);
        yield return WaitReal(0.3);
        Require(p2.IsDead && !GameSession.Instance.IsFinished && GameSession.Instance.State == GameRunState.Playing,
            "Downed player 2 ended the run: " + GameSession.Instance.State);
        Vector3 lying = p2.transform.position;
        m2.CheckInput = Vector2.left;
        yield return WaitReal(0.6);
        Require(WeaponTargets.Flat(lying, p2.transform.position) < 0.3f, "Downed hero moved.");
        Place(p1, p2.transform.position + Vector3.back * 1.2f);
        yield return WaitReal(CoopMode.ReviveSeconds + 0.8f);
        Require(!p2.IsDead && p2.CurrentHP > 0, "Player 2 not revived.");
        m2.CheckInput = Vector2.zero;
        Pass("Downed player 2: run continues, hero lies still; partner revive → HP " + p2.CurrentHP + "/" + p2.maxHP + ".");

        // 5. Поражение, только когда упали оба.
        yield return WaitReal(2.2); // неуязвимость после подъёма
        p2.TakeDamage(100000);
        yield return null;
        Require(!GameSession.Instance.IsFinished, "Run ended with one hero alive.");
        p1.TakeDamage(100000);
        yield return WaitReal(0.3);
        Require(GameSession.Instance.IsFinished && GameSession.Instance.State == GameRunState.Defeat, "No defeat after both fell.");
        Pass("Defeat only after both heroes fell.");
    }

    private static void CaptureFrame(string name)
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        RenderTexture texture = new RenderTexture(1280, 720, 24);
        List<Canvas> switched = new List<Canvas>();
        foreach (Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            switched.Add(canvas);
        }
        RenderTexture previous = camera.targetTexture;
        camera.targetTexture = texture;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = texture;
        Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        image.Apply();
        File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = previous;
        foreach (Canvas canvas in switched)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
        }
        UnityEngine.Object.Destroy(image);
        texture.Release();
    }
}
