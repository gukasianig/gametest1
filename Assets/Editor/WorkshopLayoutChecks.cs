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
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
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
                        "Overlapping labels: " + labels[i].parent.name + " / " + labels[j].parent.name);
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
        Check(tree != null && tree.NodeCount == 69, "Missing nodes");
        for (int weapon = 0; weapon < 13; weapon++)
        {
            Transform group = Find(tree.transform, "Weapon group " + weapon);
            Check(group != null && group.GetComponentsInChildren<Button>().Length == 5, "Weapon/module grouping " + weapon);
            for (int e = 1; e <= 4; e++)
                Check(Find(group, "Node weapon:" + weapon + ":" + (WeaponElement)e) != null, "Module belongs to wrong weapon");
        }
        Pass("All 13 weapon clusters contain exactly their own weapon and four modules");

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
        Check(buy.interactable, "Unlocking weapon did not enable its module purchase");
        buy.onClick.Invoke();
        Check(WeaponElements.Rank(2, WeaponElement.Frost) == 1 && equip.interactable, "Module purchase did not update UI");
        Pass("Locked, available, bought and equipped module states update through actual UI buttons");

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
        Find(tree.transform, "Node weapon:12:Poison").GetComponent<Button>().onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        RectTransform last = (RectTransform)Find(tree.transform, "Node weapon:12:Poison");
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
    }
}
