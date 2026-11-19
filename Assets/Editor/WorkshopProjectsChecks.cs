using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class WorkshopProjectsChecks
{
    private const string Request = "Logs/workshop-projects.request", Report = "Logs/workshop-projects-results.txt";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> results = new List<string>();
    private static CheckRunner runner;
    private static double started;
    private static int errors;
    private static byte[] settings;
    private static bool hadSettings;
    static WorkshopProjectsChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Request)) EditorApplication.delayCall += Begin;
        };
    }
    public static void RunBatch()
    {
        RunFor("run");
    }
    public static void RunLayoutBatch()
    {
        RunFor("layout");
    }
    public static void RunEquipmentBatch() { RunFor("equipment"); }
    public static void RunEquipmentUiBatch() { RunFor("equipment-ui"); }
    public static void RunAnimationBatch() { RunFor("motion"); }
    private static void RunFor(string scope)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Request, scope);
        EditorSceneManager.OpenScene("Assets/Scenes/GreenArenaPlay.unity");
        EditorApplication.isPlaying = true;
    }
    private static void Begin()
    {
        string scope = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        hadSettings = File.Exists(GameText.SettingsPath);
        settings = hadSettings ? File.ReadAllBytes(GameText.SettingsPath) : null;
        started = EditorApplication.timeSinceStartup;
        results.Clear(); errors = 0;
        Application.logMessageReceived += OnLog;
        runner = new CheckRunner(scope == "equipment-ui" ? EquipmentUiChecks() : scope == "equipment" ? EquipmentChecks() : scope == "motion" ? MotionChecks() : scope == "layout" ? LayoutChecks() : Checks());
        EditorApplication.update += Tick;
    }
    private static void OnLog(string text, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; results.Add("RUNTIME " + text); }
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 240) throw new Exception("Workshop project checks timed out");
            if (!runner.MoveNext()) Finish(errors == 0, "Workshop projects complete; runtime errors=" + errors);
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    private static void Finish(bool ok, string detail)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        if (hadSettings) File.WriteAllBytes(GameText.SettingsPath, settings);
        else if (File.Exists(GameText.SettingsPath)) File.Delete(GameText.SettingsPath);
        results.Add((ok ? "PASS " : "FAIL ") + detail);
        File.WriteAllLines(Report, results);
        EditorApplication.Exit(ok ? 0 : 1);
    }
    private static void Check(bool ok, string detail) { if (!ok) throw new Exception(detail); }
    private static void Pass(string detail) { results.Add("PASS " + detail); File.WriteAllLines(Report, results); }
    private static IEnumerator Real(float seconds)
    {
        double until = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < until) yield return null;
    }
    private static void Seed(string key, int value) { MetaProgress.Data.ranks.Add(new MetaEntry { key = "workshop:" + key, value = value }); }
    private static object Field(object owner, string name) => owner.GetType().GetField(name, Private).GetValue(owner);
    private static void Stats(PlayerProgression hero) => typeof(PlayerProgression).GetMethod("ApplyStats", Private).Invoke(hero, null);
    private static void Validate(WorkshopTree tree)
    {
        Canvas.ForceUpdateCanvases();
        RectTransform selected = tree.transform.Find("Tree viewport/Tree content/Tree board/Node project:generator") as RectTransform;
        if (tree.SelectedTitle == WorkshopProjects.Title("generator"))
        {
            Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(tree.Scroll.viewport, selected);
            Rect r = tree.Scroll.viewport.rect;
            Check(b.min.x >= r.xMin - 1 && b.max.x <= r.xMax + 1 && b.min.y >= r.yMin - 1 && b.max.y <= r.yMax + 1, "Selected project is offscreen");
        }
        Check(tree.Scroll.horizontalScrollbar != null && tree.Scroll.verticalScrollbar != null, "Missing scrollbars");
        var goal = tree.transform.Find("Workshop tracked goal").GetComponentInChildren<Text>();
        Check(goal.rectTransform.rect.height >= 20f, "Goal strip too small");
    }

    private static Rect Box(RectTransform relative, RectTransform target)
    {
        Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(relative, target);
        return new Rect(b.min, b.size);
    }

    private static void ValidateTreeSpacing(WorkshopTree tree)
    {
        var board = (RectTransform)tree.transform.Find("Tree viewport/Tree content/Tree board");
        var root = (RectTransform)board.Find("Root");
        Check(Vector2.Distance(root.anchoredPosition, new Vector2(board.rect.width * .5f, -board.rect.height * .5f)) < .1f, "Workshop hub not at map centre");
        var labels = new List<RectTransform>();
        var groups = new List<Rect>();
        foreach (RectTransform rect in board.GetComponentsInChildren<RectTransform>())
        {
            if (rect.name.StartsWith("Weapon group ")) groups.Add(Box(board, rect));
            if (rect.name != "Node caption" && rect.name != "Node state") continue;
            var text = rect.GetComponentInChildren<Text>();
            if (text == null || string.IsNullOrEmpty(text.text)) continue;
            labels.Add(rect);
            Rect b = Box(board, rect);
            Check(b.xMin >= board.rect.xMin && b.xMax <= board.rect.xMax && b.yMin >= board.rect.yMin && b.yMax <= board.rect.yMax, "Node label outside map: " + text.text);
        }
        for (int i = 0; i < groups.Count; i++)
            for (int j = i + 1; j < groups.Count; j++) Check(!groups[i].Overlaps(groups[j]), "Weapon groups overlap");
        for (int i = 0; i < labels.Count; i++)
            for (int j = i + 1; j < labels.Count; j++)
            {
                Rect a = Box(board, labels[i]), b = Box(board, labels[j]);
                Check(!a.Overlaps(b), "Overlapping labels: " + labels[i].GetComponentInChildren<Text>().text + " / " + labels[j].GetComponentInChildren<Text>().text);
            }
        Check(board.GetComponentInChildren<WorkshopTreeLines>() == null && board.GetComponentInChildren<WorkshopTreePulses>() == null, "Global connecting lines still exist");
    }

    private static IEnumerator MotionChecks()
    {
        yield return LayoutChecks();
        var tree = Object.FindObjectOfType<WorkshopTree>();
        tree.CompletePresentation();
        float previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        tree.FocusSection("weapons");
        Check(tree.NavigationMoving, "Section navigation did not animate");
        Vector2 start = tree.Scroll.content.anchoredPosition;
        double deadline = EditorApplication.timeSinceStartup + 5f;
        while (tree.NavigationMoving && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(Vector2.Distance(start, tree.Scroll.content.anchoredPosition) > 1f, "Navigation not progressing during pause");
        Check(!tree.NavigationMoving, "Navigation did not converge");
        tree.CenterRoot();
        tree.Scroll.OnBeginDrag(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
        Check(!tree.NavigationMoving, "Mouse drag did not interrupt animation");
        tree.Scroll.OnEndDrag(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
        tree.FocusSection("hero"); tree.CompletePresentation();
        var diamonds = tree.GetComponentsInChildren<WorkshopDiamond>();
        var target = diamonds[0];
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
        target.OnPointerEnter(pointer);
        yield return Real(.25f);
        Check(target.Hovered && target.Picture.localScale.x > 1.03f, "Node hover not animated");
        target.OnPointerExit(pointer);
        target.GetComponent<Button>().onClick.Invoke();
        tree.CompletePresentation();
        tree.PurchaseSelected();
        Check(tree.GetComponentInChildren<WorkshopBurst>() != null, "Purchase effect missing");
        Directory.CreateDirectory("Logs/workshop-motion");
        for (int frame = 0; frame < 16; frame++)
        {
            WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-motion/frame-" + frame.ToString("00") + ".png", 1280, 720, null, false);
            yield return Real(.06f);
        }
        yield return Real(1f);
        Check(tree.GetComponentInChildren<WorkshopBurst>() == null, "Purchase effect was not cleaned up");
        var buttonMotion = tree.GetComponentInChildren<WorkshopButtonMotion>();
        buttonMotion.OnPointerEnter(pointer);
        yield return Real(.2f);
        Check(buttonMotion.transform.localScale.x > 1.01f, "Button hover not animated");
        buttonMotion.OnPointerDown(pointer);
        yield return Real(.2f);
        Check(buttonMotion.transform.localScale.x < 1f, "Button press not animated");
        buttonMotion.OnPointerUp(pointer); buttonMotion.OnPointerExit(pointer);
        Time.timeScale = previousTimeScale;
        tree.gameObject.SetActive(false); tree.gameObject.SetActive(true);
        yield return Real(.5f);
        Check(tree.GetComponent<CanvasGroup>().alpha > .99f, "Workshop did not fade back in");
        Pass("Unscaled navigation, drag interruption, icon/button hover/press, purchase cleanup and reopen fade; captured 16 actual UI frames");
    }

    private static Button EquipmentButton(WorkshopTree tree, string name)
    {
        foreach (Button button in tree.GetComponentsInChildren<Button>(true)) if (button.name == name) return button;
        throw new Exception("Missing equipment button: " + name);
    }

    private static void SelectWeaponNode(WorkshopTree tree, int weapon, WeaponElement element = WeaponElement.None)
    {
        string path = "Tree viewport/Tree content/Tree board/Weapon group " + weapon + "/Node weapon:" + weapon + (element == WeaponElement.None ? "" : ":" + element);
        tree.transform.Find(path).GetComponent<Button>().onClick.Invoke();
        tree.CompletePresentation();
    }

    private static IEnumerator EquipmentChecks()
    {
        yield return MotionChecks();
        var tree = Object.FindObjectOfType<WorkshopTree>();
        var hero = PlayerProgression.Instance;
        var shooter = hero.GetComponent<AutoShoter>();
        MetaProgress.UseMemory();
        Check(!WeaponTuning.Buy(-1) && !WeaponTuning.Buy(5) && !WeaponTuning.Buy(11) && !WeaponTuning.Buy(13), "Invalid/support tuning accepted");
        MetaProgress.Data.currency = 59;
        Check(!WeaponTuning.Buy(0) && MetaProgress.Currency == 59, "Unaffordable tuning accepted");
        MetaProgress.Data.currency = 20000;
        Check(!WeaponTuning.Buy(2), "Locked weapon tuning accepted");
        MetaProgress.DevUnlockAllWeapons = true;
        int count = 0;
        for (int weapon = 0; weapon < shooter.weapons.Length; weapon++)
        {
            if (!shooter.weapons[weapon].IsSelectable) continue;
            int paid = 0, before = MetaProgress.Currency;
            for (int rank = 0; rank < WeaponTuning.MaxRank; rank++)
            {
                paid += WeaponTuning.Cost(rank);
                Check(WeaponTuning.Buy(weapon) && WeaponTuning.Rank(weapon) == rank + 1, "Tuning rank purchase failed " + weapon);
            }
            Check(MetaProgress.Currency == before - paid && !WeaponTuning.Buy(weapon), "Tuning price/cap failure");
            Check(shooter.SelectWeapon(weapon), "Cannot select tuned weapon");
            shooter.SetProgressionMultipliers(1f, 1f);
            float expected = shooter.weapons[weapon].damage * 1.5f;
            Check(Mathf.Abs(shooter.ExactDamage - expected) < .01f, "Wrong tuned damage: " + weapon);
            Check(shooter.RollDamage(false) == shooter.weapons[weapon].damage, "Tuning leaked into support damage");
            shooter.SetProgressionMultipliers(1.4f, 1.2f);
            Check(Mathf.Abs(shooter.ExactDamage - expected * 1.4f) < .01f, "Tuning lost global bonus");
            count++;
        }
        Check(count == 10, "Not all ten main weapons tested");
        Pass("All 10 weapons: 5 ranks/costs/caps, invalid/support/locked/poor purchases rejected, global bonus composes, support roll excludes tuning");
        string savePath = "Logs/workshop-equipment-save.json";
        try
        {
            MetaProgress.Data.ranks.Add(new MetaEntry { key = "element:0:5", value = 2 });
            MetaProgress.Data.ranks.Add(new MetaEntry { key = "workshop:project:scanner", value = 1 });
            File.WriteAllText(savePath, JsonUtility.ToJson(MetaProgress.Data));
            MetaProgress.UseFile(savePath);
            Check(WeaponTuning.Rank(0) == 5 && WeaponTuning.Rank(10) == 5 && MetaProgress.Rank("element:0:5") == 2 && WorkshopProjects.Rank("scanner") == 1, "Tuning save damaged other ranks");
            File.WriteAllText(savePath, "{\"version\":1,\"currency\":400,\"ranks\":[{\"key\":\"meta_damage\",\"value\":2}],\"unlocked\":[\"weapon:2\"]}");
            MetaProgress.UseFile(savePath);
            Check(WeaponTuning.Rank(0) == 0 && MetaProgress.Rank("meta_damage") == 2 && MetaProgress.IsWeaponUnlocked(2), "Old save migration damaged state");
            Check(WeaponTuning.Buy(2), "Cannot save new tuning into old save");
            MetaProgress.UseFile(savePath);
            Check(WeaponTuning.Rank(2) == 1 && WeaponTuning.Rank(0) == 0 && MetaProgress.Currency == 340, "Persisted purchase is not isolated");
        }
        finally
        {
            MetaProgress.UseMemory();
            foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(savePath + suffix)) File.Delete(savePath + suffix);
        }
        Pass("Old/new meta v1 round trip preserves branch/project/body ranks; actual tuning purchase persists separately per weapon");
        MetaProgress.Data.currency = 10000;
        tree.Refresh();
        SelectWeaponNode(tree, 0);
        EquipmentButton(tree, "Weapon enhancement purchase").onClick.Invoke();
        Check(WeaponTuning.Rank(0) == 1 && WeaponTuning.Rank(1) == 0 && MetaProgress.Currency == 9940, "UI bought wrong tuning");
        SelectWeaponNode(tree, 0, WeaponElement.Revolver);
        EquipmentButton(tree, "Weapon branch purchase").onClick.Invoke();
        Check(WeaponElements.Rank(0, WeaponElement.Revolver) == 1 && WeaponElements.Equipped(0) == WeaponElement.Revolver && WeaponTuning.Rank(0) == 1, "UI branch purchase failed");
        EquipmentButton(tree, "Weapon branch equip").onClick.Invoke();
        Check(WeaponElements.Equipped(0) == WeaponElement.None, "Branch unequip failed");
        SelectWeaponNode(tree, 2);
        Check(!EquipmentButton(tree, "Weapon branch purchase").interactable, "Locked branch can be bought");
        EquipmentButton(tree, "Weapon enhancement purchase").onClick.Invoke();
        Check(MetaProgress.IsWeaponUnlocked(2) && WeaponTuning.Rank(2) == 0, "Weapon unlock mixed with tuning");
        EquipmentButton(tree, "Weapon enhancement purchase").onClick.Invoke();
        Check(WeaponTuning.Rank(2) == 1 && !EquipmentButton(tree, "Weapon branch purchase").interactable, "Locked branch blocked base enhancement");
        SelectWeaponNode(tree, 0);
        GameText.SetLanguage(GameLanguage.Russian);
        tree.FitTree();
        WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-equipment-overview-ru.png", 1600, 900, () => ValidateTreeSpacing(tree));
        tree.FocusSection("weapons");
        WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-equipment-weapons-ru.png", 1280, 720, () => ValidateTreeSpacing(tree));
        tree.FocusProject("generator");
        Check(((Button)Field(tree, "buy")).gameObject.activeInHierarchy, "Project details did not return after weapon page");
        SelectWeaponNode(tree, 2);
        GameText.SetLanguage(GameLanguage.English);
        tree.FitTree();
        WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-equipment-en-1024.png", 1024, 768, () => ValidateTreeSpacing(tree));
        SelectWeaponNode(tree, 1);
        WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-equipment-en-wide.png", 2560, 1080, () => ValidateTreeSpacing(tree));
        Pass("Actual UI unlock/enhance/branch/unequip, still-locked other branches, project page restore; RU/EN screenshots and 3 display formats");
        tree.gameObject.SetActive(false);
        var menu = hero.GetComponent<StartMenu>();
        menu.ShowWeapons();
        typeof(StartMenu).GetMethod("BeginGame", Private).Invoke(menu, new object[] { 0 });
        hero.GetComponent<PlayerHealth>().GrantInvulnerability(60f);
        Object.FindObjectOfType<WaveManager>().StopSpawning();
        hero.GetComponent<ArenaEvents>().enabled = false;
        shooter.SetProgressionMultipliers(1f, 1f);
        Check(Mathf.Abs(shooter.ExactDamage - shooter.weapons[0].damage * 1.1f) < .01f, "Run lost pistol tuning");
        foreach (int weapon in new[] { 0, 6 })
        {
            if (weapon == 6) { MetaProgress.Unlock(MetaProgress.WeaponKey(6)); Check(WeaponTuning.Buy(6), "Cannot buy rocket tuning"); }
            shooter.SelectWeapon(weapon); shooter.SetProgressionMultipliers(1f, 1f);
            GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dummy.name = "Workshop equipment target"; dummy.tag = "Enemy";
            dummy.transform.position = shooter.transform.position + new Vector3(6f, .8f, 0);
            Health health = dummy.AddComponent<Health>(); health.SetHealth(100000); health.experienceReward = 0;
            dummy.AddComponent<EnemyAI>().MovementLocked = true;
            int before = health.CurrentHealth;
            double deadline = EditorApplication.timeSinceStartup + 8f;
            while (health.CurrentHealth == before && EditorApplication.timeSinceStartup < deadline) yield return null;
            shooter.enabled = false;
            Check(health.CurrentHealth < before, "Tuned weapon did not hit a real target: " + weapon);
            Check(before - health.CurrentHealth >= Mathf.FloorToInt(shooter.ExactDamage), "Tuned hit smaller than base: " + weapon);
            Object.Destroy(dummy);
            foreach (Bullet shot in Object.FindObjectsOfType<Bullet>()) Object.Destroy(shot.gameObject);
            yield return Real(.2f);
        }
        Pass("Combined Play check: tuned pistol projectiles and tuned rocket explosion damage actual Health targets; no full-run balance test");
    }

    private static IEnumerator EquipmentUiChecks()
    {
        yield return LayoutChecks();
        var tree = Object.FindObjectOfType<WorkshopTree>();
        MetaProgress.UseMemory(); MetaProgress.DevUnlockAllWeapons = true; MetaProgress.Data.currency = 10000;
        Check(WeaponTuning.Buy(0) && WeaponTuning.Buy(0), "Cannot seed preview tuning");
        Check(WeaponElements.Buy(0, WeaponElement.Revolver), "Cannot seed preview branch");
        tree.Refresh();
        SelectWeaponNode(tree, 0);
        GameText.SetLanguage(GameLanguage.Russian);
        tree.FitTree();
        yield return Real(1f);
        WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-equipment-overview-ru.png", 1600, 900, () => ValidateTreeSpacing(tree));
        tree.FocusSection("weapons");
        yield return Real(.6f);
        WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-equipment-weapons-ru.png", 1280, 720, () =>
        {
            ValidateTreeSpacing(tree);
            foreach (var text in tree.Scroll.content.GetComponentsInChildren<Text>())
                if (text.transform.parent.name == "Node caption") Check(text.cachedTextGenerator.characterCountVisible >= text.text.Length, "Truncated caption: " + text.text);
        });
        GameText.SetLanguage(GameLanguage.English); SelectWeaponNode(tree, 2); tree.FitTree();
        WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-equipment-en-1024.png", 1024, 768, () => ValidateTreeSpacing(tree));
        SelectWeaponNode(tree, 1);
        WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-equipment-en-wide.png", 2560, 1080, () => ValidateTreeSpacing(tree));
        Pass("Final UI polish: cyan bases, settled overview, complete node captions, RU/EN/3 formats; gameplay not repeated");
    }

    private static IEnumerator LayoutChecks()
    {
        yield return null;
        MetaProgress.UseMemory(); MetaProgress.Data.currency = 10000;
        MetaProgress.DevUnlockAllWeapons = true;
        var hero = PlayerProgression.Instance;
        var menu = hero.GetComponent<StartMenu>(); menu.ShowWorkshop();
        var tree = Object.FindObjectOfType<WorkshopTree>();
        yield return null;
        int before = tree.NodeCount;
        int expected = MetaProgress.Catalog.Length + WorkshopProjects.Ids.Length;
        var shooter = hero.GetComponent<AutoShoter>();
        int weapons = 0, modules = 0;
        for (int i = 0; i < shooter.weapons.Length; i++)
            if (shooter.weapons[i] != null && shooter.weapons[i].IsSelectable) { weapons++; modules += WeaponElements.HasBranches(i) ? 3 : 4; }
        Check(before == expected + weapons + modules, "Layout lost existing nodes");
        GameText.SetLanguage(GameLanguage.Russian);
        tree.CenterRoot();
        WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-layout-centre-ru.png", 1280, 720, () =>
        {
            ValidateTreeSpacing(tree);
            var root = (RectTransform)tree.transform.Find("Tree viewport/Tree content/Tree board/Root");
            Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(tree.Scroll.viewport, root);
            Check(Vector2.Distance(b.center, tree.Scroll.viewport.rect.center) < 1f, "Hub not centred in viewport");
        });
        tree.FocusSection("weapons");
        WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-layout-weapons-ru.png", 1280, 720, () => ValidateTreeSpacing(tree));
        tree.FocusSection("projects");
        WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-layout-projects-ru.png", 1280, 720, () => ValidateTreeSpacing(tree));
        tree.FitTree();
        WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-layout-map-ru.png", 1280, 720, () =>
        {
            ValidateTreeSpacing(tree);
            var root = (RectTransform)tree.transform.Find("Tree viewport/Tree content/Tree board/Root");
            Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(tree.Scroll.viewport, root);
            Check(Vector2.Distance(b.center, tree.Scroll.viewport.rect.center) < 1f, "Fit overview hub not centred in viewport");
        });
        GameText.SetLanguage(GameLanguage.English);
        tree.FocusSection("weapons");
        WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-layout-weapons-en-1024.png", 1024, 768, () => ValidateTreeSpacing(tree));
        tree.CenterRoot();
        WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-layout-centre-wide.png", 2560, 1080, () => ValidateTreeSpacing(tree));
        Check(tree.NodeCount == before, "Navigation changed node count");
        Pass("All body/weapon/module/project nodes retained; centred hub, disjoint groups/text/lines; RU/EN and 3 display formats");
        tree.FocusSection("hero");
        int rank = MetaProgress.Rank(MetaProgress.Catalog[0].id);
        tree.PurchaseSelected();
        Check(MetaProgress.Rank(MetaProgress.Catalog[0].id) == rank + 1, "Relocated body node purchase failed");
        Pass("Existing purchase still works through relocated nodes; no gameplay changes tested");
    }
    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        GameSession.TestMode = false; GameSession.EndlessMode = false;
        var hero = PlayerProgression.Instance;
        var menu = hero.GetComponent<StartMenu>();
        var session = hero.GetComponent<GameSession>();
        var skills = hero.GetComponent<HeroAbilities>();
        Check(!skills.PulseAvailable && !skills.OverdriveAvailable && !WorkshopProjects.Buy("invalid"), "Fresh progression not locked");
        menu.ShowWorkshop();
        var tree = Object.FindObjectOfType<WorkshopTree>();
        yield return null;
        foreach (string id in WorkshopProjects.Ids)
        {
            tree.FocusProject(id);
            Check(tree.SelectedTitle == WorkshopProjects.Title(id) && !WorkshopProjects.CanBuy(id), "Missing/affordable fresh project " + id);
        }
        MetaProgress.Data.currency = 10000;
        MetaProgress.Data.runs = 3; MetaProgress.Data.totalKills = 1000;
        foreach (string key in new[] { "core", "boss", "research", "events", "caches" }) Seed(key, 30);
        tree.FocusProject("generator");
        tree.PurchaseSelected();
        Check(WorkshopProjects.PulseUnlocked && !WorkshopProjects.DriveUnlocked && MetaProgress.Currency == 9880, "Project UI purchase failed");
        tree.EquipSelected();
        tree.FitTree();
        GameText.SetLanguage(GameLanguage.Russian);
        WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-projects-ru.png", 1280, 720, () => Validate(tree));
        foreach (string id in WorkshopProjects.Ids)
        {
            tree.FocusProject(id);
            while (WorkshopProjects.Rank(id) < 3) { Check(WorkshopProjects.CanBuy(id), "Cannot finish seeded project " + id); tree.PurchaseSelected(); }
            int before = MetaProgress.Currency; tree.PurchaseSelected();
            Check(MetaProgress.Currency == before, "Completed project charged again");
        }
        foreach (int trophy in new[] { 1, 2, 3, 4 }) MetaProgress.Unlock("workshop:relic:" + trophy);
        WorkshopProjects.CycleOption("relics");
        Check(WorkshopProjects.Relic == 1, "Relic selection failed");
        Stats(hero);
        Check(Mathf.Abs(hero.Stats.Armor - .09f) < .001f && hero.GetComponent<PlayMovement>().SpeedMultiplier < 1f, "Brute relic bonus/tradeoff missing");
        WorkshopProjects.CycleOption("relics"); Stats(hero);
        Check(Mathf.Abs(hero.Stats.RegenPerSecond - .75f) < .001f && hero.Stats.ExperienceGain < 0f && hero.Stats.Armor == 0f, "Guardian relic stacked the old relic");
        WorkshopProjects.CycleOption("relics");
        WorkshopProjects.CycleOption("relics"); Stats(hero);
        Check(hero.Stats.CritChance >= .12f - .001f && hero.AttackSpeedMultiplier < 1f, "Sniper relic bonus/tradeoff missing");
        WorkshopProjects.CycleOption("relics"); Stats(hero);
        WorkshopProjects.CycleOption("drones"); WorkshopProjects.CycleOption("scanner");
        Check(WorkshopProjects.DroneMode == 1 && WorkshopProjects.ScannerMode == 1, "Project options failed");
        tree.FocusProject("generator");
        tree.ZoomAt(5f, RectTransformUtility.WorldToScreenPoint(null, tree.Scroll.viewport.TransformPoint(tree.Scroll.viewport.rect.center)));
        float zoom = tree.Zoom;
        Check(zoom > 1f && tree.Scroll.content.rect.width > tree.Scroll.viewport.rect.width, "Zoom did not expand navigable board");
        tree.Pan(new Vector2(-120f, 80f));
        Check(tree.Scroll.content.anchoredPosition.x < 0f, "Horizontal panning failed");
        tree.FocusProject("generator");
        GameText.SetLanguage(GameLanguage.English);
        WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-projects-en-1024.png", 1024, 768, () => Validate(tree));
        tree.FitTree();
        WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-projects-wide.png", 2560, 1080, () =>
        {
            Check(tree.Scroll.content.rect.height <= tree.Scroll.viewport.rect.height + 1f && tree.Scroll.content.rect.width <= tree.Scroll.viewport.rect.width + 1f, "Fit mode did not survive resize");
        });
        string path = "Logs/workshop-projects-save.json";
        try
        {
            File.WriteAllText(path, JsonUtility.ToJson(MetaProgress.Data));
            var restored = MetaProgress.Load(path);
            Check(restored.ranks.Exists(e => e.key == "workshop:project:scanner" && e.value == 3) && restored.ranks.Exists(e => e.key == "workshop:target" && e.value == 1), "Project save round trip failed");
            File.WriteAllText(path, "{\"version\":1,\"currency\":321,\"ranks\":[{\"key\":\"meta_health\",\"value\":2}],\"unlocked\":[\"weapon:2\"]}");
            restored = MetaProgress.Load(path);
            Check(restored.currency == 321 && restored.ranks[0].value == 2 && restored.unlocked.Contains("weapon:2"), "Old save changed");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
        Pass("All 15 stage purchases/limits/options, tracked goal, old/new saves, RU/EN, fit/zoom/pan/selection at 3 sizes");

        tree.gameObject.SetActive(false);
        MetaProgress.UseMemory(); MetaProgress.Data.currency = 10000; MetaProgress.Data.runs = 1;
        menu.ShowWeapons();
        typeof(StartMenu).GetMethod("BeginGame", Private).Invoke(menu, new object[] { 1 });
        hero.GetComponent<PlayerHealth>().GrantInvulnerability(200f);
        var waves = Object.FindObjectOfType<WaveManager>(); waves.StopSpawning();
        hero.GetComponent<AutoShoter>().enabled = false;
        hero.GetComponent<ArenaEvents>().enabled = false;
        typeof(GameSession).GetField("miniSpawned", Private).SetValue(session, true);
        Check(!skills.TryPulse() && !skills.TryOverdrive(), "Locked skills fired in real combat");
        Check(WorkshopProjects.Buy("generator") && skills.TryPulse() && !skills.TryOverdrive(), "Q stage did not unlock only Q");
        var mini = waves.SpawnMiniBoss(); Check(mini != null, "Cannot spawn Brute");
        yield return Real(.15f);
        mini.GetComponent<Health>().TakeDamage(1000000);
        yield return Real(.4f);
        Check(WorkshopProjects.Count("core") == 1 && MetaProgress.IsUnlocked("workshop:relic:1"), "Actual Brute kill did not persist core/trophy");
        Check(WorkshopProjects.Buy("generator") && skills.TryOverdrive(), "E stage did not unlock E");
        var events = hero.GetComponent<ArenaEvents>(); events.Begin(ArenaEventKind.SupplyDrop); events.End(true);
        Check(WorkshopProjects.Count("events") == 1, "Completed event not recorded");
        Check(WorkshopProjects.Buy("drones") && WorkshopProjects.Buy("scanner") && WorkshopProjects.Buy("weapons"), "Field projects did not unlock");
        Stats(hero);
        Check(hero.Stats.Armor == 0f, "Station granted armor without a drone");
        var droneCard = hero.catalog.entries.Find(d => d.effect == UpgradeEffect.DroneCount);
        Check(droneCard != null, "Drone card missing");
        ((Dictionary<string, int>)Field(hero, "ranks"))[droneCard.id] = 1;
        WorkshopProjects.CycleOption("drones"); Stats(hero);
        Check(Mathf.Abs(hero.Stats.Armor - .03f) < .001f, "Defender role did not apply with drone card");
        WorkshopProjects.CycleOption("drones"); Stats(hero);
        Check(hero.PickupRadiusMultiplier >= 1.15f && hero.Stats.Armor == 0f, "Collector role stacked the old role");
        CoopMode.Players = 2;
        var second = CoopMode.SpawnSecond(hero.gameObject, 1);
        yield return Real(.2f);
        var secondProgress = second.GetComponent<PlayerProgression>();
        secondProgress.ApplyMetaBonuses();
        Check(Mathf.Approximately(secondProgress.PickupRadiusMultiplier, 1f) && hero.PickupRadiusMultiplier >= 1.15f, "Drone role leaked into another hero without a drone");
        Check(second.GetComponent<HeroAbilities>().PulseAvailable && second.GetComponent<HeroAbilities>().OverdriveAvailable, "Account unlocks unavailable to player two");
        Object.Destroy(second); CoopMode.Players = 1;
        yield return null;
        for (int i = 0; i < 100; i++) WorkshopProjects.RecordKill(1);
        Stats(hero);
        Check(WorkshopProjects.Mastery(1) == 1 && hero.DamageMultiplier >= 1.05f, "Weapon mastery bonus missing");
        WorkshopProjects.Track("generator");
        var elapsed = typeof(GameSession).GetProperty("Elapsed");
        elapsed.SetValue(session, 91f);
        yield return Real(.3f);
        var cache = Object.FindObjectOfType<WorkshopProjectCache>(); Check(cache != null, "Scanner did not spawn its first signal");
        typeof(HeroOverhaulChecks).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "workshop-scanner-game", hero.transform, false });
        int cash = MetaProgress.Currency;
        Check(cache.Collect(hero) && !cache.Collect(hero) && MetaProgress.Currency == cash + 40, "Cache paid wrong reward or twice");
        Check(WorkshopProjects.Buy("scanner"), "Scanner stage 2 blocked");
        WorkshopProjects.CycleOption("scanner"); WorkshopProjects.CycleOption("scanner");
        elapsed.SetValue(session, 181f); yield return Real(.3f);
        cache = Object.FindObjectOfType<WorkshopProjectCache>(); Check(cache != null && cache.Collect(hero) && WorkshopProjects.Count("research") == 1, "Research cache failed");
        yield return null;
        Check(WorkshopProjects.Buy("scanner"), "Scanner stage 3 blocked");
        WorkshopProjects.CycleOption("scanner"); WorkshopProjects.CycleOption("scanner");
        elapsed.SetValue(session, 271f); yield return Real(.3f);
        cache = Object.FindObjectOfType<WorkshopProjectCache>();
        int level = hero.Level;
        Check(cache != null && cache.Collect(hero) && (hero.Experience > 0 || hero.Level > level), "Supply cache failed");
        int choices = 0;
        while (hero.Choosing && choices++ < 10) Check(hero.SelectOfferedUpgrade(0), "Cannot close cache XP choices");
        Pass("Actual Q/E locks, Brute loot, event completion, drone role replacement, mastery and 3 scanner rewards without duplicate collection");
        GameSession.EndlessMode = true; session.survivalSeconds = 1000000f;
        typeof(GameSession).GetMethod("SpawnBoss", Private).Invoke(session, null);
        yield return Real(.2f);
        Check(session.Boss != null, "Final boss missing");
        session.Boss.Health.TakeDamage(1000000);
        yield return Real(.5f);
        Check(WorkshopProjects.Count("boss") == 1, "Final boss core missing");
        typeof(PlayerHealth).GetField("invulnerableUntil", Private).SetValue(hero.GetComponent<PlayerHealth>(), 0f);
        hero.GetComponent<PlayerHealth>().TakeDamage(1000000);
        yield return Real(.4f);
        Check(session.IsFinished && WorkshopProjects.Count("boss") == 1 && WorkshopProjects.Count("research") == 1 && WorkshopProjects.Count("caches") == 3, "Defeat lost collected components");
        Pass("Actual boss core and subsequent defeat retain project components/cache rewards; test uses only memory meta");
    }
}
