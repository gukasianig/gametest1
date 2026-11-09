using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Проверка всех моделей Assets/Art/ArenaGreen:
//  • геометрия: открытые края (дыры), вырожденные треугольники, вывернутые нормали, UV вне атласа, пустые UV;
//  • листы-превью 6×5 (ракурс 3/4 сверху) для глазной проверки. Порядок — Logs/models/index.txt.
// batch: -executeMethod GreenArenaModelCheck.Run -quit
public static class GreenArenaModelCheck
{
    const string Root = "Assets/Art/ArenaGreen";
    public const int Tile = 256, Cols = 6, Rows = 5;
    static float Yaw = 35f;

    public static void Run()
    {
        Directory.CreateDirectory("Logs/models");
        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (!p.Contains("/_Build/")) paths.Add(p);
        }
        paths.Sort();
        Geometry(paths);
        Sheets(paths);
        EditorApplication.Exit(0);
    }

    // Листы только для исправленных копий рельефа (Assets/Art/ArenaGreen/_Build/Fixed). batch: -executeMethod GreenArenaModelCheck.RunFixed -quit
    public static void RunFixed()
    {
        Directory.CreateDirectory("Logs/models");
        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root + "/_Build/Fixed" })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        paths.Sort();
        Sheets(paths, "fixed_");
        EditorApplication.Exit(0);
    }

    // Шесть моделей, переделанных в Blender: крупные превью 3x2. batch: -executeMethod GreenArenaModelCheck.RunRemade -quit
    public static void RunRemade()
    {
        Directory.CreateDirectory("Logs/models");
        var paths = new List<string>
        {
            Root + "/buildings/windmill.fbx", Root + "/structures/road_dirt_cross.fbx", Root + "/terrain/plateau_step.fbx",
            Root + "/terrain/ramp_dirt.fbx", Root + "/terrain/waterfall_cliff.fbx", Root + "/water/river_fork_Y.fbx",
        };
        Geometry(paths);
        Sheets(paths, "remade_", 512, 3, 2);
        Yaw = 215f;
        Sheets(paths, "remadeB_", 512, 3, 2);
        EditorApplication.Exit(0);
    }

    // Четыре модели снарядов. batch: -executeMethod GreenArenaModelCheck.RunProjectiles -quit
    public static void RunProjectiles()
    {
        Directory.CreateDirectory("Logs/models");
        var paths = new List<string>();
        foreach (string n in new[] { "bullet", "rocket", "grenade", "ice_shard" }) paths.Add("Assets/Resources/Projectiles/" + n + ".fbx");
        Yaw = 215f;
        Sheets(paths, "proj_", 512, 2, 2);
        EditorApplication.Exit(0);
    }

    static void Geometry(List<string> paths)
    {
        var sb = new StringBuilder("model;tris;openEdges;degenerate;inwardNormals%;uvOutside%;uvSpread;submeshes;problems\n");
        foreach (string p in paths)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            int tris = 0, open = 0, degenerate = 0, inward = 0, uvOut = 0, uvCount = 0, subs = 0;
            float uvMinX = 1, uvMaxX = 0, uvMinY = 1, uvMaxY = 0;
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
            {
                Mesh m = mf.sharedMesh;
                if (m == null) continue;
                subs += m.subMeshCount;
                Vector3[] v = m.vertices;
                Vector2[] uv = m.uv;
                int[] t = m.triangles;
                tris += t.Length / 3;
                Vector3 centre = m.bounds.center;
                // Рёбра по позициям (вершины с разными UV/нормалями в одной точке — одна вершина).
                var key = new Dictionary<Vector3, int>();
                int[] id = new int[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 q = new Vector3(Mathf.Round(v[i].x * 1e4f), Mathf.Round(v[i].y * 1e4f), Mathf.Round(v[i].z * 1e4f));
                    if (!key.TryGetValue(q, out id[i])) { id[i] = key.Count; key[q] = id[i]; }
                }
                var edges = new Dictionary<long, int>();
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    Vector3 n = Vector3.Cross(b - a, c - a);
                    if (n.sqrMagnitude < 1e-14f) { degenerate++; continue; }
                    Vector3 mid = (a + b + c) / 3f;
                    if (Vector3.Dot(n, mid - centre) < 0f) inward++;
                    int[] ids = { id[t[i]], id[t[i + 1]], id[t[i + 2]] };
                    for (int e = 0; e < 3; e++)
                    {
                        int x = ids[e], y = ids[(e + 1) % 3];
                        long k = x < y ? ((long)x << 32) | (uint)y : ((long)y << 32) | (uint)x;
                        edges[k] = edges.TryGetValue(k, out int cnt) ? cnt + 1 : 1;
                    }
                }
                foreach (int cnt in edges.Values) if (cnt == 1) open++;
                foreach (Vector2 u in uv)
                {
                    uvCount++;
                    if (u.x < -0.01f || u.x > 1.01f || u.y < -0.01f || u.y > 1.01f) uvOut++;
                    uvMinX = Mathf.Min(uvMinX, u.x); uvMaxX = Mathf.Max(uvMaxX, u.x);
                    uvMinY = Mathf.Min(uvMinY, u.y); uvMaxY = Mathf.Max(uvMaxY, u.y);
                }
            }
            float inwardPct = tris > 0 ? inward * 100f / tris : 0f, uvOutPct = uvCount > 0 ? uvOut * 100f / uvCount : 100f;
            float spread = uvCount > 0 ? Mathf.Max(uvMaxX - uvMinX, uvMaxY - uvMinY) : 0f;
            var problems = new List<string>();
            if (tris == 0) problems.Add("NO_MESH");
            if (uvCount == 0) problems.Add("NO_UV");
            if (uvOutPct > 1f) problems.Add("UV_OUTSIDE");
            if (open > 0) problems.Add("OPEN_EDGES");
            if (degenerate > 0) problems.Add("DEGENERATE");
            if (inwardPct > 45f) problems.Add("NORMALS_INWARD?");
            sb.AppendLine(p.Replace(Root + "/", "") + ";" + tris + ";" + open + ";" + degenerate + ";" + inwardPct.ToString("0") + ";" +
                uvOutPct.ToString("0") + ";" + spread.ToString("0.###") + ";" + subs + ";" + string.Join(" ", problems));
        }
        File.WriteAllText("Logs/models/geometry.csv", sb.ToString());
    }

    static void Sheets(List<string> paths, string prefix = "", int Tile = GreenArenaModelCheck.Tile, int Cols = GreenArenaModelCheck.Cols, int Rows = GreenArenaModelCheck.Rows)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var light = new GameObject("Sun").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        light.intensity = 1.2f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
        var cam = new GameObject("Cam").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.78f, 0.8f, 0.84f);
        cam.fieldOfView = 30f;
        var rt = new RenderTexture(Tile, Tile, 24);
        cam.targetTexture = rt;
        int perSheet = Cols * Rows;
        var index = new StringBuilder();
        for (int s = 0; s * perSheet < paths.Count; s++)
        {
            var sheet = new Texture2D(Cols * Tile, Rows * Tile, TextureFormat.RGB24, false);
            for (int k = 0; k < perSheet; k++)
            {
                int i = s * perSheet + k;
                Color[] blank = new Color[Tile * Tile];
                for (int b = 0; b < blank.Length; b++) blank[b] = Color.white;
                int px = (k % Cols) * Tile, py = (Rows - 1 - k / Cols) * Tile;
                if (i >= paths.Count) { sheet.SetPixels(px, py, Tile, Tile, blank); continue; }
                index.AppendLine("sheet " + s + " row " + (k / Cols + 1) + " col " + (k % Cols + 1) + ": " + paths[i].Replace(Root + "/", ""));
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]));
                Bounds bnd = new Bounds(go.transform.position, Vector3.zero); bool first = true;
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) { if (first) { bnd = r.bounds; first = false; } else bnd.Encapsulate(r.bounds); }
                float radius = Mathf.Max(0.2f, bnd.extents.magnitude);
                Vector3 dir = Quaternion.Euler(28f, Yaw, 0f) * Vector3.back;
                cam.transform.position = bnd.center + dir * radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;   // dir смотрит вверх-назад: камера сверху
                cam.transform.LookAt(bnd.center);
                cam.nearClipPlane = radius * 0.05f; cam.farClipPlane = radius * 10f;
                cam.Render();
                RenderTexture.active = rt;
                var tile = new Texture2D(Tile, Tile, TextureFormat.RGB24, false);
                tile.ReadPixels(new Rect(0, 0, Tile, Tile), 0, 0);
                RenderTexture.active = null;
                Color[] pix = tile.GetPixels();
                // Тонкая рамка между ячейками.
                for (int y = 0; y < Tile; y++) for (int x = 0; x < Tile; x++)
                    if (x == 0 || y == 0) pix[y * Tile + x] = new Color(0.3f, 0.3f, 0.3f);
                sheet.SetPixels(px, py, Tile, Tile, pix);
                Object.DestroyImmediate(tile);
                Object.DestroyImmediate(go);
            }
            sheet.Apply();
            File.WriteAllBytes("Logs/models/" + prefix + "sheet_" + s.ToString("00") + ".png", sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }
        File.WriteAllText("Logs/models/" + prefix + "index.txt", index.ToString());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
    }
}
