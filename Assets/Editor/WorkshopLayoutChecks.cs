using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// UI-only regression for owner-reported workshop layout problems. No battle or player save mutations.
[InitializeOnLoad]
public static class WorkshopLayoutChecks
{
    private const string Request = "Logs/workshop-layout.request";
    private const string Report = "Logs/workshop-layout-results.txt";
    private static CheckRunner routine;
    private static double started;
    private static readonly List<string> results = new List<string>();

    static WorkshopLayoutChecks()
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
    private static void Begin()
    {
        File.Delete(Request);
        results.Clear();
        started = EditorApplication.timeSinceStartup;
        routine = new CheckRunner(Checks());
        EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 120) throw new Exception("UI check timed out");
            if (!routine.MoveNext()) Finish(true, "Workshop layout checks complete");
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    private static void Finish(bool success, string detail)
    {
        EditorApplication.update -= Tick;
        results.Add((success ? "PASS " : "FAIL ") + detail);
        File.WriteAllLines(Report, results);
        EditorApplication.Exit(success ? 0 : 1);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Pass(string message) { results.Add("PASS " + message); File.WriteAllLines(Report, results); }
    private static Transform Find(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent) { Transform found = Find(child, name); if (found != null) return found; }
        return null;
    }
    private static Rect Bounds(RectTransform relativeTo, RectTransform rect)
    {
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(relativeTo, rect);
        return new Rect(bounds.min, bounds.size);
    }
    private static void Validate(WorkshopTree tree)
    {
        RectTransform board = (RectTransform)Find(tree.transform, "Tree board");
        RectTransform viewport = tree.Scroll.viewport;
        // The complete drawing board fits the viewport width, even on 4:3 screens.
        Rect boardBounds = Bounds(viewport, board);
        Check(boardBounds.xMin >= viewport.rect.xMin - 1 && boardBounds.xMax <= viewport.rect.xMax + 1,
            "Tree board clipped horizontally: " + boardBounds + " vs " + viewport.rect);
        var labels = new List<RectTransform>();
        foreach (Transform child in board.GetComponentsInChildren<Transform>())
            if (child.name == "Node caption" || child.name == "Node state") labels.Add((RectTransform)child);
        for (int i = 0; i < labels.Count; i++)
            for (int j = i + 1; j < labels.Count; j++)
            {
                Rect a = Bounds(board, labels[i]), b = Bounds(board, labels[j]);
                if (a.Overlaps(b))
                {
                    Rect intersection = Rect.MinMaxRect(Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin),
                        Mathf.Min(a.xMax, b.xMax), Mathf.Min(a.yMax, b.yMax));
                    Check(intersection.width < .5f || intersection.height < .5f,
                        "Overlapping labels: " + labels[i].parent.name + " " + labels[i].name + " " + a + " / " + labels[j].parent.name + " " + labels[j].name + " " + b);
                }
            }
        foreach (RectTransform label in labels)
        {
            Rect rect = Bounds(board, label);
            Check(rect.xMin >= -1 && rect.xMax <= board.rect.width + 1, "Label outside drawing board: " + label.parent.name);
        }
        Check(tree.Scroll.verticalScrollbar != null && tree.Scroll.verticalScrollbar.handleRect != null, "Visible scrollbar missing");
    }
    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        StartMenu menu = PlayerProgression.Instance.GetComponent<StartMenu>();
        menu.ShowWorkshop();
        WorkshopTree tree = UnityEngine.Object.FindObjectOfType<WorkshopTree>();
        Check(tree != null && tree.NodeCount == 54, "Missing nodes");
        AutoShoter workshopShooter = PlayerProgression.Instance.GetComponent<AutoShoter>();
        for (int weapon = 0; weapon < 13; weapon++)
        {
            if (!workshopShooter.weapons[weapon].IsSelectable) continue;
            Transform group = Find(tree.transform, "Weapon group " + weapon);
            Check(group != null && group.GetComponentsInChildren<Button>().Length == 5, "Weapon/module grouping " + weapon);
            for (int e = 1; e <= 4; e++)
                Check(Find(group, "Node weapon:" + weapon + ":" + (WeaponElement)e) != null, "Module belongs to wrong weapon");
        }
        Pass("All selectable weapon clusters contain exactly their own weapon and four modules");

        // Select a locked module and check that the UI does not call its preview an owned effect.
        Find(tree.transform, "Node weapon:2:Frost").GetComponent<Button>().onClick.Invoke();
        Button buy = Find(tree.transform, "Tree purchase").GetComponent<Button>();
        Button equip = Find(tree.transform, "Tree equip").GetComponent<Button>();
        Check(!buy.interactable && !equip.interactable, "Locked weapon module is actionable");
        bool prerequisiteText = false;
        foreach (Text label in tree.GetComponentsInChildren<Text>())
            if (label.text == "Сначала открой оружие") prerequisiteText = true;
        Check(prerequisiteText, "Missing weapon prerequisite explanation");
        MetaProgress.Data.currency = 2000;
        MetaProgress.TryUnlock("weapon:2");
        tree.Refresh();
        if (WeaponElements.Locked)
        {
            // Улучшения оружия временно закрыты ключом: покупка недоступна даже у открытого оружия.
            Check(!buy.interactable && !equip.gameObject.activeSelf, "Locked module is purchasable");
            Check(Find(tree.transform, "Lock key") != null, "Lock key icon missing");
            Pass("Weapon upgrades are temporarily locked: key icons shown, purchase and equip disabled");
            Find(tree.transform, "Node weapon:0").GetComponent<Button>().onClick.Invoke();
            WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-keys.png", 1600, 900, null);
        }
        else
        {
            Check(buy.interactable, "Unlocking weapon did not enable its module purchase");
            buy.onClick.Invoke();
            Check(WeaponElements.Rank(2, WeaponElement.Frost) == 1 && equip.interactable, "Module purchase did not update UI");
            Pass("Locked, available, bought and equipped module states update through actual UI buttons");
        }

        Canvas canvas = tree.GetComponentInParent<Canvas>();
        // Three distinct aspect ratios. Only two top snapshots and one bottom snapshot are retained.
        foreach (Vector2Int size in new[] { new Vector2Int(1600, 900), new Vector2Int(1024, 768), new Vector2Int(2560, 1080) })
        {
            tree.Scroll.verticalNormalizedPosition = 1f;
            Find(tree.transform, "Node weapon:0").GetComponent<Button>().onClick.Invoke();
            WorkshopTreeChecks.Capture(canvas, "Logs/workshop-layout-" + size.x + ".png", size.x, size.y, () => Validate(tree));
            Pass("No overlapping labels or horizontal clipping at " + size.x + "x" + size.y);
            yield return null;
        }
        int lastWeapon = Array.FindLastIndex(workshopShooter.weapons, w => w != null && w.IsSelectable);
        Find(tree.transform, "Node weapon:" + lastWeapon + ":Poison").GetComponent<Button>().onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        RectTransform last = (RectTransform)Find(tree.transform, "Node weapon:" + lastWeapon + ":Poison");
        Rect lastBounds = Bounds(tree.Scroll.viewport, last);
        Check(lastBounds.yMin >= tree.Scroll.viewport.rect.yMin - 1 && lastBounds.yMax <= tree.Scroll.viewport.rect.yMax + 1,
            "Selected final module is not revealed by scrolling");
        WorkshopTreeChecks.Capture(canvas, "Logs/workshop-layout-bottom.png", 1600, 900, () =>
        {
            Validate(tree);
            Rect visibleLast = Bounds(tree.Scroll.viewport, last);
            Check(visibleLast.yMin >= tree.Scroll.viewport.rect.yMin - 1 && visibleLast.yMax <= tree.Scroll.viewport.rect.yMax + 1,
                "Resizing hid the selected last module in the rendered UI");
        });
        Pass("Final weapon/module is reachable and selecting an offscreen node reveals its whole label");

        // Картинки оружия остаются после выхода из мастерской и повторного входа, а также после перезагрузки сцены
        // (возврат в меню после забега). Раньше OnDestroy уничтожал общие иконки Resources.
        Find(tree.transform, "‹  НАЗАД").GetComponent<Button>().onClick.Invoke();
        yield return null;
        menu.ShowWorkshop();
        yield return null;
        CheckPictures(tree, workshopShooter, "after reopening");
        UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        PlayerProgression previous = PlayerProgression.Instance;
        UnityEngine.SceneManagement.SceneManager.LoadScene(scene.name);
        double deadline = EditorApplication.timeSinceStartup + 30;
        while (PlayerProgression.Instance == null || PlayerProgression.Instance == previous ||
            !PlayerProgression.Instance.GetComponent<StartMenu>().WaitingForWeapon)
        {
            Check(EditorApplication.timeSinceStartup < deadline, "Menu did not reload");
            yield return null;
        }
        yield return null;
        menu = PlayerProgression.Instance.GetComponent<StartMenu>();
        menu.ShowWorkshop();
        tree = UnityEngine.Object.FindObjectOfType<WorkshopTree>();
        yield return null;
        CheckPictures(tree, PlayerProgression.Instance.GetComponent<AutoShoter>(), "after scene reload");
        WorkshopTreeChecks.Capture(tree.GetComponentInParent<Canvas>(), "Logs/workshop-after-reload.png", 1600, 900, null);
        Pass("Weapon pictures stay visible after reopening the workshop and after reloading the scene");

        // Новые модели оружия в руке: модель есть, дуло впереди героя и не ниже кисти; лист кадров 5×2.
        AutoShoter hero = PlayerProgression.Instance.GetComponent<AutoShoter>();
        var sheet = new Texture2D(5 * 320, 2 * 320, TextureFormat.RGB24, false);
        RenderTexture shot = RenderTexture.GetTemporary(320, 320, 24);
        Camera look = new GameObject("Weapon check camera").AddComponent<Camera>();
        look.targetTexture = shot;
        look.fieldOfView = 32f;
        int slot = 0;
        var report = new List<string>();
        for (int w = 0; w < hero.weapons.Length; w++)
        {
            if (hero.weapons[w] == null || !hero.weapons[w].IsSelectable) continue;
            Check(hero.SelectWeapon(w), "Cannot select weapon " + w);
            yield return null;
            yield return null;
            Transform socket = Find(hero.transform, "WeaponSocket");
            Check(socket != null && socket.Find("SkinModel") != null, "No weapon model in hand: " + w);
            Check(ModelSkins.TryGetMuzzle(out Vector3 muzzle), "No muzzle for weapon " + w);
            Vector3 local = hero.transform.InverseTransformPoint(muzzle);
            Vector3 hand = hero.transform.InverseTransformPoint(socket.position);
            report.Add(w + " " + ModelSkins.WeaponKey(hero.weapons[w], w) + " muzzle " + local.ToString("0.00") + " hand " + hand.ToString("0.00"));
            Check(local.z > hand.z + 0.15f, "Muzzle is not in front of the hand: weapon " + w + " " + local + " / " + hand);
            Vector3 target = socket.position + hero.transform.forward * 0.25f;
            look.transform.position = target + hero.transform.right * 1.5f + hero.transform.forward * 0.5f + Vector3.up * 0.45f;
            look.transform.LookAt(target);
            look.Render();
            RenderTexture.active = shot;
            sheet.ReadPixels(new Rect(0, 0, 320, 320), slot % 5 * 320, (1 - slot / 5) * 320);
            RenderTexture.active = null;
            slot++;
        }
        sheet.Apply();
        File.WriteAllBytes("Logs/weapon-models-in-hand.png", sheet.EncodeToPNG());
        File.WriteAllLines("Logs/weapon-models-in-hand.txt", report);
        UnityEngine.Object.Destroy(look.gameObject);
        RenderTexture.ReleaseTemporary(shot);
        Pass(slot + " weapon models in hand: muzzle in front of the hand (sheet Logs/weapon-models-in-hand.png)");
    }

    private static void CheckPictures(WorkshopTree tree, AutoShoter shooter, string when)
    {
        for (int weapon = 0; weapon < shooter.weapons.Length; weapon++)
        {
            if (shooter.weapons[weapon] == null || !shooter.weapons[weapon].IsSelectable) continue;
            Transform node = Find(tree.transform, "Node weapon:" + weapon);
            Check(node != null, "Weapon node missing " + when + ": " + weapon);
            Image picture = null;
            foreach (Image image in node.GetComponentsInChildren<Image>(true))
                if (image.transform.parent == node && image.name == "Artwork") { picture = image; break; }
            Check(picture != null && picture.enabled && picture.sprite != null && picture.sprite.texture != null,
                "Weapon picture missing " + when + ": " + weapon);
        }
    }
}
