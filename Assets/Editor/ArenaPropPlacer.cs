using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Окно «Arena → Prop Placer»: выберите модель из списка, включите «Ставить кликом» и кликайте по полу в Scene View.
// Объекты попадают в корень "Arena Props (edit me)" (создаётся сам). Ctrl+Z отменяет, Esc или ПКМ выключает режим.
public class ArenaPropPlacer : EditorWindow
{
    static readonly string[] Folders = { "Assets/Art/Kenney" };
    List<GameObject> models = new List<GameObject>();
    GameObject selected;
    string filter = "";
    Vector2 scroll;
    bool placing;
    float size = 2f;
    bool randomYaw = true, randomScale = true, solid;
    float yaw;
    float snap;
    float lift;
    bool stack = true;

    [MenuItem("Arena/Prop Placer")]
    static void Open() { GetWindow<ArenaPropPlacer>("Prop Placer").Show(); }

    void OnEnable() { Reload(); SceneView.duringSceneGui += OnScene; }
    void OnDisable() { SceneView.duringSceneGui -= OnScene; placing = false; }

    void Reload()
    {
        models.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:GameObject", Folders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".fbx") || path.EndsWith(".prefab") || path.EndsWith(".glb") || path.EndsWith(".obj"))
                models.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }
        models = models.Where(m => m != null).OrderBy(m => m.name).ToList();
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("1) Выберите модель. 2) Включите «Ставить кликом». 3) Кликайте по полу в Scene View. Esc / ПКМ — выйти из режима.", MessageType.Info);
        filter = EditorGUILayout.TextField("Поиск", filter);
        size = EditorGUILayout.Slider("Размер, м (длинная сторона)", size, 0.3f, 15f);
        randomScale = EditorGUILayout.Toggle("Случайный масштаб ±15%", randomScale);
        randomYaw = EditorGUILayout.Toggle("Случайный поворот", randomYaw);
        if (!randomYaw) yaw = EditorGUILayout.Slider("Поворот", yaw, 0f, 360f);
        snap = EditorGUILayout.Slider("Сетка (0 = выкл), м", snap, 0f, 5f);
        solid = EditorGUILayout.Toggle("Препятствие (solid)", solid);
        stack = EditorGUILayout.Toggle("Ставить поверх других моделей", stack);
        lift = EditorGUILayout.Slider("Высота над поверхностью, м", lift, 0f, 20f);
        EditorGUILayout.LabelField("Выбрано: " + (selected != null ? selected.name : "—"));
        bool was = placing;
        placing = GUILayout.Toggle(placing, placing ? "● Ставить кликом: ВКЛ" : "Ставить кликом: выкл", "Button", GUILayout.Height(28));
        if (placing && !was) SceneView.RepaintAll();
        if (GUILayout.Button("Обновить список")) Reload();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        int shown = 0;
        float width = position.width - 24f;
        int perRow = Mathf.Max(1, Mathf.FloorToInt(width / 84f));
        EditorGUILayout.BeginHorizontal();
        foreach (GameObject m in models)
        {
            if (filter.Length > 0 && m.name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (shown > 0 && shown % perRow == 0) { EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal(); }
            shown++;
            Texture2D preview = AssetPreview.GetAssetPreview(m);
            var content = new GUIContent(preview != null ? preview : Texture2D.grayTexture, m.name);
            bool pick = GUILayout.Toggle(selected == m, content, "Button", GUILayout.Width(80), GUILayout.Height(80));
            if (pick && selected != m) { selected = m; placing = true; }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
        if (AssetPreview.IsLoadingAssetPreviews()) Repaint();
    }

    // Точка под курсором: пол (y=0) или верх ближайшей модели из корня декора (если stack).
    static bool Ground(Event e, bool stack, out Vector3 point)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        point = Vector3.zero;
        if (Mathf.Abs(ray.direction.y) < 0.0001f) return false;
        float t = -ray.origin.y / ray.direction.y;
        if (t <= 0f) return false;
        point = ray.origin + ray.direction * t;
        if (stack)
        {
            ArenaPropsRoot root = Object.FindObjectOfType<ArenaPropsRoot>(true);
            float best = t;
            if (root != null)
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                {
                    Bounds b = r.bounds;
                    if (b.max.y < 0.1f || !b.IntersectRay(ray, out float d) || d <= 0f || d >= best) continue;
                    best = d;
                    Vector3 hit = ray.origin + ray.direction * d;
                    point = new Vector3(hit.x, b.max.y, hit.z);
                }
        }
        return true;
    }

    void OnScene(SceneView view)
    {
        DrawGround();
        if (!placing || selected == null) return;
        Event e = Event.current;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if (!Ground(e, stack, out Vector3 p)) return;
        if (snap > 0f) p = new Vector3(Mathf.Round(p.x / snap) * snap, p.y, Mathf.Round(p.z / snap) * snap);

        Handles.color = solid ? new Color(1f, 0.4f, 0.2f) : new Color(0.4f, 0.9f, 1f);
        Handles.DrawWireDisc(p + Vector3.up * lift, Vector3.up, size * 0.5f);
        Handles.Label(p + Vector3.up * 0.5f, selected.name);
        view.Repaint();

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { placing = false; e.Use(); Repaint(); return; }
        if (e.type == EventType.MouseDown && e.button == 1 && !e.alt) { placing = false; e.Use(); Repaint(); return; }
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt) { Place(p); e.Use(); }
    }

    // Пол арены в редакторе (в игре он создаётся при старте): полупрозрачная плоскость 220×220 м, чтобы было на чём ставить.
    static void DrawGround()
    {
        const float h = 110f;
        Handles.DrawSolidRectangleWithOutline(new[] { new Vector3(-h, 0, -h), new Vector3(-h, 0, h), new Vector3(h, 0, h), new Vector3(h, 0, -h) },
            new Color(0.3f, 0.5f, 0.45f, 0.12f), new Color(0.9f, 0.7f, 0.2f, 0.9f));
    }

    void Place(Vector3 p)
    {
        ArenaPropsRoot root = Object.FindObjectOfType<ArenaPropsRoot>(true);
        if (root == null)
        {
            var rg = new GameObject("Arena Props (edit me)");
            Undo.RegisterCreatedObjectUndo(rg, "Create arena props root");
            root = rg.AddComponent<ArenaPropsRoot>();
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(selected, root.transform);
        Undo.RegisterCreatedObjectUndo(go, "Place arena prop");
        go.name = "Decor_" + selected.name;
        go.transform.rotation = Quaternion.Euler(0f, randomYaw ? Random.value * 360f : yaw, 0f);
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) { Undo.DestroyObjectImmediate(go); return; }
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        float max = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (max > 0.0001f) go.transform.localScale = Vector3.one * (size * (randomScale ? Random.Range(0.85f, 1.15f) : 1f) / max);
        b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        go.transform.position += new Vector3(p.x - b.center.x, p.y + lift - b.min.y, p.z - b.center.z);
        if (solid) Undo.AddComponent<ArenaProp>(go).solid = true;
        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
    }
}
