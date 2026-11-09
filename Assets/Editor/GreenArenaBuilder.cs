using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Сборка зелёной арены 400×400 м из ассетов Codex (Assets/Art/ArenaGreen) в отдельной сцене GreenArena.
// Меню Arena → Build Green Arena; batch: -executeMethod GreenArenaBuilder.BuildBatch -quit
// Герой/враги/игра в сцену не входят: это площадка для оценки вида. Центр 70×70 м ровный, холмы и горы по краям.
public static class GreenArenaBuilder
{
    const string Root = "Assets/Art/ArenaGreen";
    const string ScenePath = "Assets/Scenes/GreenArena.unity";
    const string DataDir = Root + "/_Build";
    const float Size = 400f, Height = 80f, Below = 8f;
    const int Seed = 20261108;

    static readonly string[] Layers = { "grass_lush", "grass_dark", "dirt_path", "dirt_rich", "rock_gray", "sand_fine", "mud_wet", "grass_meadow" };
    static System.Random rng;
    static Terrain terrain;
    static Material atlas, water, foliage;

    [MenuItem("Arena/Build Green Arena")]
    public static void Build()
    {
        rng = new System.Random(Seed);
        Directory.CreateDirectory(DataDir);
        PrepareTextures();
        PrepareMaterials();
        int fixedTerrain = FixTerrainModels();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        BuildTerrain();
        PaintTerrain();

        Transform plants = new GameObject("Plants").transform, rocks = new GameObject("Rocks").transform,
            buildings = new GameObject("Buildings").transform, waterRoot = new GameObject("Water").transform,
            props = new GameObject("Props").transform, far = new GameObject("Far Mountains").transform;

        int count = 0;
        pondCircles.Clear();
        count += Ponds(waterRoot);   // сначала вода: от неё зависят берега, трава и расстановка
        PaintShores();
        // Трава и цветы — детали Terrain (GPU instancing): густо, дёшево и можно дорисовать кистью Paint Details.
        int grass = PlantDetails();
        count += Scatter("plants", new[] { "weeds_mixed", "flower_patch", "moss_patch" }, 260, 0f, 8f, 25f, 0.9f, 1.3f, plants, true, 0f);
        count += Scatter("plants", new[] { "fern", "bush_leafy", "bush_flowering", "bush_berry" }, 320, 40f, 6f, 25f, 0.9f, 1.4f, plants, false, 0f);
        count += Scatter("plants", new[] { "tree_oak", "tree_birch", "tree_pine", "tree_spruce", "tree_willow", "tree_small_sapling" }, 170, 62f, 14f, 25f, 0.9f, 1.3f, plants, false, 0f);
        count += Scatter("plants", new[] { "tree_dead", "stump_big", "log_fallen", "root_cluster", "mushroom_small_set" }, 50, 55f, 10f, 28f, 0.9f, 1.2f, plants, false, 0f);
        // Камни (08.11): модели нормальных пропорций (высота ≈0,7 ширины), но прежние 0,4–1,2 м тонули в траве до 1,7 м,
        // а плоские плиты rock_slab_flat читались «лепёшками». Теперь крупнее, вытянуты по высоте, слегка наклонены и утоплены,
        // есть кучи (крупный + 2–5 мелких), трава под камнями убрана. Плиты в луга не ставятся.
        count += Scatter("rocks", new[] { "rock_pebble_set" }, 70, 0f, 7f, 35f, 1f, 1.5f, rocks, true, 0f);
        count += Rocks(new[] { "rock_small" }, 130, 0f, 7f, 35f, 1.3f, 1.8f, 1.15f, 1.45f, 8f, rocks);
        count += Rocks(new[] { "rock_medium", "rock_mossy_medium" }, 90, 40f, 9f, 32f, 1.4f, 1.9f, 1.2f, 1.5f, 8f, rocks);
        count += Rocks(new[] { "rock_large", "rock_cluster", "rock_arch_natural" }, 34, 75f, 22f, 40f, 1.1f, 1.45f, 1.1f, 1.3f, 5f, rocks);
        count += Rocks(new[] { "rock_spire" }, 6, 90f, 22f, 40f, 1f, 1.3f, 1f, 1f, 3f, rocks);
        count += RockPiles(46, 28f, 14f, 30f, rocks);
        int clearedGrass = ClearDetailsUnder(rocks);
        count += Scatter("buildings", new[] { "house_wood_small", "house_wood_medium", "barn", "windmill", "tower_watch_wood", "well_stone", "shed_tool", "ruins_wall_set", "ruins_arch", "gazebo", "tent_camp_2", "campfire_pit" }, 14, 70f, 20f, 8f, 1f, 1f, buildings, false, 0f);
        count += Scatter("structures", new[] { "haystack", "cart_wood", "barrels_cluster", "crates_stack_3", "signpost_01", "scarecrow", "log_pile" }, 24, 55f, 12f, 15f, 1f, 1.1f, props, false, 0f);
        count += Mountains(far);
        EnvironmentSetup();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/green-arena.txt", "ok objects=" + count + " grassDetails=" + grass + " clearedUnderRocks=" + clearedGrass + " fixedTerrain=" + fixedTerrain);
    }

    // ---------- текстуры и материалы ----------

    static void PrepareTextures()
    {
        foreach (string path in Directory.GetFiles(Root + "/ground", "*.png", SearchOption.AllDirectories))
        {
            string p = path.Replace('\\', '/');
            var imp = AssetImporter.GetAtPath(p) as TextureImporter;
            if (imp == null) continue;
            bool changed = false;
            if (p.EndsWith("_normal.png") && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; changed = true; }
            if ((p.EndsWith("_rough.png") || p.EndsWith("_height.png")) && imp.sRGBTexture) { imp.sRGBTexture = false; changed = true; }
            if (p.Contains("/decals/") && !imp.alphaIsTransparency) { imp.alphaIsTransparency = true; changed = true; }
            if (changed) imp.SaveAndReimport();
        }
        var atlasImp = AssetImporter.GetAtPath(Root + "/ArenaGreen_Atlas.png") as TextureImporter;
        if (atlasImp != null && atlasImp.mipmapEnabled == false) { atlasImp.mipmapEnabled = true; atlasImp.SaveAndReimport(); }
    }

    static Material MakeMaterial(string path, Texture2D tex, float smooth, bool transparent, Color tint)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        if (tex != null) m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", 0f);
        if (transparent)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = 3000;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static void PrepareMaterials()
    {
        atlas = MakeMaterial(DataDir + "/M_ArenaGreen.mat", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/ArenaGreen_Atlas.png"), 0.1f, false, Color.white);
        water = MakeMaterial(DataDir + "/M_ArenaWater.mat", null, 0.92f, true, new Color(0.16f, 0.42f, 0.5f, 0.72f));
        foliage = MakeMaterial(DataDir + "/M_ArenaGreenFoliage.mat", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/ArenaGreen_Atlas.png"), 0.1f, false, Color.white);
        foliage.SetFloat("_Cull", 0f);                 // двусторонний
        foliage.doubleSidedGI = true;
        foliage.enableInstancing = true;               // для деталей Terrain
        EditorUtility.SetDirty(foliage);
        AssetDatabase.SaveAssets();

        // Все FBX получают общий материал с атласом; материалы воды — прозрачный.
        List<string> names = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) continue;
            bool changed = false;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(o is Material em)) continue;
                if (!names.Contains(em.name)) names.Add(em.name);
                bool isWater = em.name.ToLowerInvariant().Contains("water");
                // Растения у Codex — односторонние плоскости (листья, травинки, кувшинки): с изнанки они пропадали. Для них материал без отсечения граней.
                Material target = isWater ? water : path.Contains("/plants/") ? foliage : atlas;
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), em.name);
                if (!imp.GetExternalObjectMap().ContainsKey(id) || imp.GetExternalObjectMap()[id] != target)
                { imp.AddRemap(id, target); changed = true; }
            }
            if (changed) imp.SaveAndReimport();
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/green-arena-materials.txt", string.Join("\n", names));
    }

    // ---------- рельеф ----------

    static float Noise(float x, float z, float scale, float offset) { return Mathf.PerlinNoise(x * scale + offset, z * scale + offset); }

    static void BuildTerrain()
    {
        TerrainData data = new TerrainData();
        AssetDatabase.CreateAsset(data, AssetDatabase.GenerateUniqueAssetPath(DataDir + "/GreenArenaTerrain.asset"));
        int n = 513;
        data.heightmapResolution = n;
        data.size = new Vector3(Size, Height, Size);
        List<TerrainLayer> layers = new List<TerrainLayer>();
        foreach (string name in Layers) layers.Add(MakeLayer(name));
        data.terrainLayers = layers.ToArray();

        float[,] h = new float[n, n];
        float flat = Below / Height;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float wx = (x / (float)(n - 1) - 0.5f) * Size, wz = (y / (float)(n - 1) - 0.5f) * Size;
                float d = Mathf.Sqrt(wx * wx + wz * wz);
                float edge = Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz));
                // Ровная боевая площадка в центре, дальше плавный подъём холмов.
                float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(48f, 150f, d));
                float hills = (Noise(wx, wz, 0.011f, 11f) * 0.75f + Noise(wx, wz, 0.035f, 3f) * 0.25f);
                float meters = rise * (3f + hills * 14f) + Mathf.Max(0f, Noise(wx, wz, 0.02f, 90f) - 0.55f) * 6f * rise;
                // Горный вал по краю арены.
                float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(150f, 198f, edge));
                meters += rim * (18f + Noise(wx, wz, 0.03f, 50f) * 30f);
                h[y, x] = flat + meters / Height;
            }
        data.SetHeights(0, 0, h);

        GameObject go = Terrain.CreateTerrainGameObject(data);
        go.name = "Green Arena Terrain";
        go.transform.position = new Vector3(-Size / 2f, -Below, -Size / 2f);
        terrain = go.GetComponent<Terrain>();
        Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (shader != null) terrain.materialTemplate = new Material(shader);
        terrain.heightmapPixelError = 4f;
    }

    static TerrainLayer MakeLayer(string name)
    {
        string path = DataDir + "/" + name + ".terrainlayer";
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
        string dir = Root + "/ground/" + name + "/" + name;
        layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "_color.png");
        layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "_normal.png");
        layer.smoothness = 0.1f;
        layer.normalScale = 0.8f;
        layer.tileSize = new Vector2(name.StartsWith("rock") ? 12f : name.StartsWith("dirt") || name.StartsWith("sand") ? 10f : 8f,
            name.StartsWith("rock") ? 12f : name.StartsWith("dirt") || name.StartsWith("sand") ? 10f : 8f);
        EditorUtility.SetDirty(layer);
        return layer;
    }

    static void PaintTerrain()
    {
        TerrainData data = terrain.terrainData;
        int a = data.alphamapResolution, layers = Layers.Length;
        float[,,] map = new float[a, a, layers];
        for (int y = 0; y < a; y++)
            for (int x = 0; x < a; x++)
            {
                float nx = x / (float)(a - 1), nz = y / (float)(a - 1);
                float wx = (nx - 0.5f) * Size, wz = (nz - 0.5f) * Size;
                float d = Mathf.Sqrt(wx * wx + wz * wz);
                float slope = data.GetSteepness(nx, nz);
                float altitude = data.GetInterpolatedHeight(nx, nz) - Below;
                float n1 = Noise(wx, wz, 0.02f, 5f), n2 = Noise(wx, wz, 0.07f, 30f);
                float[] w = new float[layers];
                w[0] = 1f;                                                    // grass_lush
                w[1] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.75f, n1)) * 0.8f;   // тёмные пятна
                w[7] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.8f, n2)) * 0.7f;    // луговые пятна
                w[2] = Mathf.Clamp01(1f - d / 16f + (n2 - 0.5f) * 0.6f);      // утоптанная площадка в центре
                w[3] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(14f, 26f, slope));          // грунт на склонах
                w[4] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(28f, 40f, slope)) * 1.6f;   // скала на крутом
                w[4] += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, 45f, altitude)) * 0.8f;
                // Песок и грязь — только у прудов (PaintShores), не по высоте: ровный центр тоже на высоте 0.
                float sum = 0f;
                for (int i = 0; i < layers; i++) sum += w[i];
                for (int i = 0; i < layers; i++) map[y, x, i] = w[i] / sum;
            }
        data.SetAlphamaps(0, 0, map);
    }

    // ---------- расстановка ----------

    static GameObject Load(string folder, string prefix)
    {
        List<string> options = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets(prefix + " t:Model", new[] { Root + "/" + folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(path);
            if (file == prefix || (file.StartsWith(prefix + "_") && file.Length > prefix.Length + 1 && char.IsDigit(file[prefix.Length + 1]))) options.Add(path);
        }
        if (options.Count == 0) return null;
        string chosen = options[rng.Next(options.Count)];
        // Для рельефа есть исправленные копии (FixTerrainModels) — берём их.
        GameObject fixedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DataDir + "/Fixed/" + Path.GetFileNameWithoutExtension(chosen) + ".prefab");
        return fixedPrefab != null ? fixedPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(chosen);
    }

    // ---------- исправление моделей рельефа ----------

    // У кусков рельефа Codex (скалы, горы, холмы, каньон и т.д.) развёртка боковых граней растянута через несколько ячеек атласа 8×8,
    // и стены выглядят как полосатый забор. Делаем исправленную копию: трансформ корня запекается (метры, Y-вверх),
    // треугольники-стены (|нормаль.y| < 0,45) получают UV внутри одной ячейки нужного цвета (камень, земля или песок) со случайным сдвигом —
    // остаётся «фасеточный» вид, как у остальных моделей. Исходный FBX не меняется.
    static readonly Vector2 SwatchRock = new Vector2(4, 6), SwatchDirt = new Vector2(1, 7), SwatchSand = new Vector2(3, 7);   // столбец, строка сверху

    static Vector2 SwatchUV(Vector2 cell, System.Random r)
    {
        float u = (cell.x + 0.2f + (float)r.NextDouble() * 0.6f) / 8f;
        float v = 1f - (cell.y + 0.2f + (float)r.NextDouble() * 0.6f) / 8f;
        return new Vector2(u, v);
    }

    static int FixTerrainModels()
    {
        string dir = DataDir + "/Fixed";
        Directory.CreateDirectory(dir);
        int fixedCount = 0;
        var r = new System.Random(7);
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/terrain" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            // Модели, переделанные в Blender (docs/art/Arena/GreenPack/remake_arena_models.py): UV уже по ячейкам, у waterfall_cliff есть отдельная вода.
            if (name == "plateau_step" || name == "ramp_dirt" || name == "waterfall_cliff")
            {
                AssetDatabase.DeleteAsset(dir + "/" + name + ".prefab");
                AssetDatabase.DeleteAsset(dir + "/" + name + "_mesh.asset");
                continue;
            }
            MeshFilter mf = source.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Vector2 wall = name.Contains("dune") ? SwatchSand :
                name.Contains("hill") || name.Contains("crater") || name.Contains("plateau") || name.Contains("sinkhole") ||
                name.Contains("bank") || name.Contains("ramp") || name.Contains("ledge") ? SwatchDirt : SwatchRock;
            Matrix4x4 m = source.transform.localToWorldMatrix * (mf.transform == source.transform ? Matrix4x4.identity :
                source.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix);
            Mesh src = mf.sharedMesh;
            Vector3[] v = src.vertices;
            Vector2[] uv = src.uv;
            int[] t = src.triangles;
            // Разворачиваем в несвязанные треугольники: у каждой грани свои вершины и своя UV.
            var nv = new Vector3[t.Length];
            var nuv = new Vector2[t.Length];
            var nt = new int[t.Length];
            int walls = 0;
            for (int i = 0; i < t.Length; i += 3)
            {
                for (int k = 0; k < 3; k++) { nv[i + k] = m.MultiplyPoint3x4(v[t[i + k]]); nuv[i + k] = uv.Length > 0 ? uv[t[i + k]] : Vector2.zero; nt[i + k] = i + k; }
                Vector3 n = Vector3.Cross(nv[i + 1] - nv[i], nv[i + 2] - nv[i]).normalized;
                if (Mathf.Abs(n.y) < 0.45f)
                {
                    Vector2 p = SwatchUV(wall, r);
                    nuv[i] = nuv[i + 1] = nuv[i + 2] = p;
                    walls++;
                }
            }
            var mesh = new Mesh { indexFormat = nv.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.vertices = nv; mesh.uv = nuv; mesh.triangles = nt;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string meshPath = dir + "/" + name + "_mesh.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = atlas;
            PrefabUtility.SaveAsPrefabAsset(go, dir + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
            if (walls > 0) fixedCount++;
        }
        AssetDatabase.SaveAssets();
        return fixedCount;
    }

    static int Scatter(string folder, string[] prefixes, int count, float minRadius, float margin, float maxSlope, float minScale, float maxScale,
        Transform parent, bool lowOnly, float lift)
    {
        int made = 0, attempts = 0;
        while (made < count && attempts < count * 12)
        {
            attempts++;
            float edgeLimit = Size / 2f - margin - 4f;
            float x = (float)(rng.NextDouble() * 2 - 1) * edgeLimit, z = (float)(rng.NextDouble() * 2 - 1) * edgeLimit;
            float d = Mathf.Sqrt(x * x + z * z);
            if (d < minRadius) continue;
            if (!lowOnly && d < minRadius) continue;
            float nx = (x + Size / 2f) / Size, nz = (z + Size / 2f) / Size;
            if (terrain.terrainData.GetSteepness(nx, nz) > maxSlope) continue;
            float ground = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
            if (InPond(x, z, 3f)) continue;                                // не в воде и не на самом берегу
            if (ground > 22f && !prefixes[0].StartsWith("rock")) continue; // выше леса растений нет
            GameObject prefab = Load(folder, prefixes[rng.Next(prefixes.Length)]);
            if (prefab == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = new Vector3(x, ground + lift, z);
            go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) * prefab.transform.rotation;
            go.transform.localScale = prefab.transform.localScale * Mathf.Lerp(minScale, maxScale, (float)rng.NextDouble());
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            made++;
        }
        return made;
    }

    static float GroundAt(float x, float z) { return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y; }

    static bool TryBounds(GameObject go, out Bounds b)
    {
        b = default;
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return false;
        b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return true;
    }

    // Камень: ширина ×sx, высота ×sy по МИРОВОЙ вертикали, наклон до tilt°, низ — по самой низкой точке земли под камнем, утоплен на sink высоты.
    // Корень FBX Codex повёрнут (−90° по X, масштаб ×100): «высота» модели — не локальная Y корня, поэтому ось ищется по повороту префаба.
    static GameObject PlaceRock(GameObject prefab, Transform parent, float x, float z, float tilt, float sx, float sy, float sink)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        float tx = (float)(rng.NextDouble() * 2 - 1) * tilt, tz = (float)(rng.NextDouble() * 2 - 1) * tilt;
        go.transform.rotation = Quaternion.Euler(tx, (float)rng.NextDouble() * 360f, tz) * prefab.transform.rotation;
        Vector3 up = Quaternion.Inverse(prefab.transform.rotation) * Vector3.up;
        float ax = Mathf.Abs(up.x), ay = Mathf.Abs(up.y), az = Mathf.Abs(up.z);
        int axis = ax > ay && ax > az ? 0 : ay > az ? 1 : 2;
        Vector3 s = prefab.transform.localScale * sx;
        s[axis] = prefab.transform.localScale[axis] * sy;
        go.transform.localScale = s;
        go.transform.position = new Vector3(x, 0f, z);
        if (TryBounds(go, out Bounds b))
        {
            float r = 0.35f * Mathf.Min(b.size.x, b.size.z), ground = GroundAt(x, z);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                ground = Mathf.Min(ground, GroundAt(x + Mathf.Cos(a) * r, z + Mathf.Sin(a) * r));
            }
            go.transform.position = new Vector3(x, ground - b.min.y - sink * b.size.y, z);
        }
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
        return go;
    }

    static float Range(float a, float b) { return Mathf.Lerp(a, b, (float)rng.NextDouble()); }

    // Одиночные камни: ширина minScale..maxScale, высота дополнительно ×minStretch..maxStretch.
    static int Rocks(string[] prefixes, int count, float minRadius, float margin, float maxSlope, float minScale, float maxScale,
        float minStretch, float maxStretch, float tilt, Transform parent)
    {
        int made = 0, attempts = 0;
        while (made < count && attempts < count * 12)
        {
            attempts++;
            float edgeLimit = Size / 2f - margin - 4f;
            float x = (float)(rng.NextDouble() * 2 - 1) * edgeLimit, z = (float)(rng.NextDouble() * 2 - 1) * edgeLimit;
            if (Mathf.Sqrt(x * x + z * z) < minRadius) continue;
            if (terrain.terrainData.GetSteepness((x + Size / 2f) / Size, (z + Size / 2f) / Size) > maxSlope) continue;
            if (InPond(x, z, 3f)) continue;
            GameObject prefab = Load("rocks", prefixes[rng.Next(prefixes.Length)]);
            if (prefab == null) continue;
            float sx = Range(minScale, maxScale);
            PlaceRock(prefab, parent, x, z, tilt, sx, sx * Range(minStretch, maxStretch), 0.06f);
            made++;
        }
        return made;
    }

    // Кучи: крупный камень + 2–5 мелких вокруг, частично перекрываются, наклонены и утоплены сильнее. Каждый камень — прямой
    // потомок Rocks (GreenArenaObstacles ставит блокер на каждый крупный). Имена «RockPile NN …» — для кадров проверки.
    static int RockPiles(int count, float minRadius, float margin, float maxSlope, Transform parent)
    {
        string[] mains = { "rock_large", "rock_large", "rock_mossy_medium", "rock_medium", "rock_cluster" };
        string[] parts = { "rock_medium", "rock_mossy_medium", "rock_medium", "rock_small", "rock_small" };
        var centres = new List<Vector2>();
        int made = 0, piles = 0, attempts = 0;
        while (piles < count && attempts < count * 20)
        {
            attempts++;
            float edgeLimit = Size / 2f - margin - 4f;
            float x = (float)(rng.NextDouble() * 2 - 1) * edgeLimit, z = (float)(rng.NextDouble() * 2 - 1) * edgeLimit;
            if (Mathf.Sqrt(x * x + z * z) < minRadius) continue;
            if (terrain.terrainData.GetSteepness((x + Size / 2f) / Size, (z + Size / 2f) / Size) > maxSlope) continue;
            if (InPond(x, z, 9f)) continue;
            bool crowded = false;
            foreach (Vector2 c in centres) if ((c - new Vector2(x, z)).sqrMagnitude < 18f * 18f) { crowded = true; break; }
            if (crowded) continue;
            string mainName = mains[rng.Next(mains.Length)];
            GameObject prefab = Load("rocks", mainName);
            if (prefab == null) continue;
            bool large = mainName == "rock_large" || mainName == "rock_cluster";
            float sx = large ? Range(1f, 1.35f) : Range(1.8f, 2.3f);
            GameObject main = PlaceRock(prefab, parent, x, z, 7f, sx, sx * Range(1.15f, 1.4f), 0.08f);
            main.name = "RockPile " + piles.ToString("00") + " " + main.name;
            made++;
            float radius = 1.5f;
            if (TryBounds(main, out Bounds mb)) radius = 0.5f * Mathf.Max(mb.size.x, mb.size.z);
            int n = 2 + rng.Next(4);
            float start = (float)rng.NextDouble() * 360f;
            for (int i = 0; i < n; i++)
            {
                float a = (start + i * 360f / n + Range(-25f, 25f)) * Mathf.Deg2Rad, dist = radius * Range(0.55f, 0.95f);
                float px = x + Mathf.Cos(a) * dist, pz = z + Mathf.Sin(a) * dist;
                if (InPond(px, pz, 2f)) continue;
                string partName = parts[rng.Next(parts.Length)];
                GameObject partPrefab = Load("rocks", partName);
                if (partPrefab == null) continue;
                float ps = partName == "rock_small" ? Range(1.9f, 2.8f) : Range(0.95f, 1.45f);
                GameObject part = PlaceRock(partPrefab, parent, px, pz, 22f, ps, ps * Range(1f, 1.35f), 0.15f);
                part.name = "RockPile " + piles.ToString("00") + " " + part.name;
                made++;
            }
            centres.Add(new Vector2(x, z));
            piles++;
        }
        return made;
    }

    // Трава-детали росла сквозь камни и закрывала их низ — камень читался плоским пятном. Под каждым камнем выше 0,3 м детали убираются.
    static int ClearDetailsUnder(Transform rocks)
    {
        TerrainData data = terrain.terrainData;
        int res = data.detailResolution, cleared = 0;
        var circles = new List<Vector3>();
        foreach (Transform t in rocks)
            if (TryBounds(t.gameObject, out Bounds b) && b.size.y > 0.3f)
                circles.Add(new Vector3(b.center.x, b.center.z, 0.42f * Mathf.Min(b.size.x, b.size.z)));
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
        {
            int[,] map = data.GetDetailLayer(0, 0, res, res, layer);
            foreach (Vector3 c in circles)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt((c.x - c.z + Size / 2f) / Size * res)), x1 = Mathf.Min(res - 1, Mathf.CeilToInt((c.x + c.z + Size / 2f) / Size * res));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((c.y - c.z + Size / 2f) / Size * res)), z1 = Mathf.Min(res - 1, Mathf.CeilToInt((c.y + c.z + Size / 2f) / Size * res));
                for (int zi = z0; zi <= z1; zi++)
                    for (int xi = x0; xi <= x1; xi++)
                    {
                        float wx = (xi + 0.5f) / res * Size - Size / 2f, wz = (zi + 0.5f) / res * Size - Size / 2f;
                        if ((wx - c.x) * (wx - c.x) + (wz - c.y) * (wz - c.y) > c.z * c.z || map[zi, xi] == 0) continue;
                        cleared += map[zi, xi];
                        map[zi, xi] = 0;
                    }
            }
            data.SetDetailLayer(0, 0, layer, map);
        }
        return cleared;
    }

    static int Mountains(Transform parent)
    {
        int made = 0;
        for (int i = 0; i < 16; i++)
        {
            float angle = i / 16f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.15f;
            float radius = 250f + (float)rng.NextDouble() * 30f;
            GameObject prefab = Load("terrain", "mountain_far");
            if (prefab == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = new Vector3(Mathf.Cos(angle) * radius, 10f, Mathf.Sin(angle) * radius);
            go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) * prefab.transform.rotation;
            go.transform.localScale = prefab.transform.localScale * (0.9f + (float)rng.NextDouble() * 0.5f);
            made++;
        }
        return made;
    }

    static readonly List<Vector3> pondCircles = new List<Vector3>();   // x, z, радиус

    static bool InPond(float x, float z, float margin)
    {
        foreach (Vector3 c in pondCircles)
        {
            float dx = x - c.x, dz = z - c.y, r = c.z + margin;
            if (dx * dx + dz * dz < r * r) return true;
        }
        return false;
    }

    // Ямка под пруд: внутри радиуса опускаем землю на depth, к краю плавно возвращаем.
    // Площадка под пруд: до 1,1 радиуса земля выравнивается на уровень ground (борт чаши вровень), внутри 0,9 — яма глубиной depth,
    // от 1,1 до 1,7 радиуса — плавный возврат к исходному склону.
    static void CarvePond(float cx, float cz, float radius, float depth, float ground)
    {
        TerrainData data = terrain.terrainData;
        int n = data.heightmapResolution;
        float cell = Size / (n - 1), reach = radius * 1.8f;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((cx - reach + Size / 2f) / cell), 0, n - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((cx + reach + Size / 2f) / cell), 0, n - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((cz - reach + Size / 2f) / cell), 0, n - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((cz + reach + Size / 2f) / cell), 0, n - 1);
        int w = x1 - x0 + 1, h = z1 - z0 + 1;
        float[,] heights = data.GetHeights(x0, z0, w, h);
        float flatLevel = (ground + Below) / Height;
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float wx = (x0 + i) * cell - Size / 2f, wz = (z0 + j) * cell - Size / 2f;
                float d = Mathf.Sqrt((wx - cx) * (wx - cx) + (wz - cz) * (wz - cz)) / radius;
                float pit = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 0.9f, d));
                float desired = flatLevel - pit * depth / Height;
                float blend = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.1f, 1.7f, d));
                heights[j, i] = Mathf.Lerp(heights[j, i], desired, blend);
            }
        data.SetHeights(x0, z0, heights);
    }

    // Песок у кромки воды и мокрая грязь чуть дальше, с рваным краем по шуму.
    static void PaintShores()
    {
        TerrainData data = terrain.terrainData;
        int a = data.alphamapResolution, layers = Layers.Length;
        float[,,] map = data.GetAlphamaps(0, 0, a, a);
        for (int y = 0; y < a; y++)
            for (int x = 0; x < a; x++)
            {
                float wx = (x / (float)(a - 1) - 0.5f) * Size, wz = (y / (float)(a - 1) - 0.5f) * Size;
                float sand = 0f, mud = 0f;
                foreach (Vector3 c in pondCircles)
                {
                    float d = Mathf.Sqrt((wx - c.x) * (wx - c.x) + (wz - c.y) * (wz - c.y)) - c.z + (Noise(wx, wz, 0.15f, 71f) - 0.5f) * 3f;
                    sand = Mathf.Max(sand, Mathf.Clamp01(1f - d / 2f));
                    mud = Mathf.Max(mud, Mathf.Clamp01(1f - Mathf.Abs(d - 2.5f) / 2f) * 0.5f);
                }
                if (sand <= 0f && mud <= 0f) continue;
                float keep = 1f - Mathf.Max(sand, mud);
                for (int i = 0; i < layers; i++) map[y, x, i] *= keep;
                map[y, x, 5] += sand * (1f - mud * 0.3f);
                map[y, x, 6] += Mathf.Max(sand, mud) - sand * (1f - mud * 0.3f);
                float sum = 0f;
                for (int i = 0; i < layers; i++) sum += map[y, x, i];
                for (int i = 0; i < layers; i++) map[y, x, i] /= Mathf.Max(0.0001f, sum);
            }
        data.SetAlphamaps(0, 0, map);
    }

    // Пруды в низких пологих местах за боевой зоной; чаша модели ставится на землю.
    static int Ponds(Transform parent)
    {
        // Вытянутые oxbow_lagoon и marsh_patch не ставим: берег рисуется кругом и для них получается огромным.
        string[] names = { "pond_L", "pond_M", "pond_M", "pond_S", "pond_S", "pond_S", "spring_pool" };
        int made = 0;
        foreach (string file in names)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f, radius = 70f + (float)rng.NextDouble() * 70f;
                float x = Mathf.Cos(angle) * radius, z = Mathf.Sin(angle) * radius;
                float nx = (x + Size / 2f) / Size, nz = (z + Size / 2f) / Size;
                if (terrain.terrainData.GetSteepness(nx, nz) > 4f) continue;
                string path = Root + "/water/" + file + ".fbx";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { path = Root + "/water/" + file + "_01.fbx"; prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); }
                if (prefab == null) break;
                float pondRadius = 4f;
                foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>())
                    pondRadius = Mathf.Max(pondRadius, (r.bounds.extents.x + r.bounds.extents.z) * 0.5f);
                if (InPond(x, z, pondRadius + 8f)) continue;   // пруды не налезают друг на друга
                pondCircles.Add(new Vector3(x, z, pondRadius));
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                float ground = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
                // Модель пруда — плоская чаша с бортом ≈0,5 м: утапливаем её так, чтобы край был вровень с землёй, а под ней продавливаем рельеф.
                float pondHeight = 0.5f;
                foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>()) pondHeight = Mathf.Max(0.1f, r.bounds.max.y);
                CarvePond(x, z, pondRadius, pondHeight + 0.15f, ground);
                go.transform.position = new Vector3(x, ground - pondHeight + 0.04f, z);
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) * prefab.transform.rotation;
                made++;
                break;
            }
        }
        return made;
    }

    // ---------- трава через детали Terrain ----------

    // FBX от Codex экспортированы без Apply Transform: в корне поворот −90° по X и масштаб ×100, а сам меш в сантиметрах и Z-вверх.
    // Детали Terrain берут только меш, поэтому запекаем трансформ корня в новый меш (метры, Y-вверх) и делаем маленький prefab.
    static GameObject BakeDetail(string fbxName)
    {
        string fbx = Root + "/plants/" + fbxName + ".fbx";
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        MeshFilter mf = source != null ? source.GetComponentInChildren<MeshFilter>() : null;
        if (mf == null || mf.sharedMesh == null) return null;
        string dir = DataDir + "/Details";
        Directory.CreateDirectory(dir);
        Matrix4x4 m = Matrix4x4.TRS(Vector3.zero, source.transform.rotation, source.transform.localScale) *
                      (mf.transform == source.transform ? Matrix4x4.identity : mf.transform.localToWorldMatrix);
        Mesh src = mf.sharedMesh;
        Mesh mesh = Object.Instantiate(src);
        Vector3[] v = src.vertices, n = src.normals;
        float minY = float.PositiveInfinity;
        for (int i = 0; i < v.Length; i++) { v[i] = m.MultiplyPoint3x4(v[i]); minY = Mathf.Min(minY, v[i].y); }
        for (int i = 0; i < v.Length; i++) v[i].y -= minY;
        for (int i = 0; i < n.Length; i++) n[i] = m.MultiplyVector(n[i]).normalized;
        mesh.vertices = v;
        if (n.Length == v.Length) mesh.normals = n;
        mesh.RecalculateBounds();
        string meshPath = dir + "/" + fbxName + "_mesh.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
        var go = new GameObject(fbxName);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = foliage;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, dir + "/" + fbxName + ".prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    static int PlantDetails()
    {
        atlas.enableInstancing = true;
        EditorUtility.SetDirty(atlas);
        string[] names = { "grass_clump_short_01", "grass_clump_short_02", "grass_clump_short_03", "grass_clump_short_04",
            "grass_clump_tall_01", "grass_clump_tall_02", "grass_wild_seedy_01", "grass_wild_seedy_02",
            "flower_single_set_01", "flower_single_set_03", "flower_single_set_05" };
        var protos = new List<DetailPrototype>();
        var kinds = new List<int>();   // 0 короткая, 1 высокая, 2 цветы
        foreach (string name in names)
        {
            GameObject prefab = BakeDetail(name);
            if (prefab == null) continue;
            protos.Add(new DetailPrototype
            {
                prototype = prefab, usePrototypeMesh = true, useInstancing = true, renderMode = DetailRenderMode.VertexLit,
                minWidth = 0.8f, maxWidth = 1.3f, minHeight = 0.8f, maxHeight = 1.35f, noiseSpread = 0.4f,
                healthyColor = Color.white, dryColor = new Color(0.86f, 0.86f, 0.78f)
            });
            kinds.Add(name.Contains("flower") ? 2 : name.Contains("short") ? 0 : 1);
        }
        TerrainData data = terrain.terrainData;
        const int res = 512;
        // В Unity 2022.2+ новая TerrainData по умолчанию в режиме Coverage: там значение ячейки — доля 0..255, и «1» почти ничего не рисует.
        // Нам нужен счёт экземпляров на ячейку.
        data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
        data.SetDetailResolution(res, 32);
        data.detailPrototypes = protos.ToArray();
        var layers = new int[protos.Count][,];
        for (int i = 0; i < layers.Length; i++) layers[i] = new int[res, res];
        int total = 0;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float nx = (x + 0.5f) / res, nz = (y + 0.5f) / res;
                float wx = (nx - 0.5f) * Size, wz = (nz - 0.5f) * Size;
                if (data.GetSteepness(nx, nz) > 30f) continue;
                float alt = data.GetInterpolatedHeight(nx, nz) - Below;
                if (alt > 26f || InPond(wx, wz, 1.5f)) continue;
                float d = Mathf.Sqrt(wx * wx + wz * wz);
                float patch = Noise(wx, wz, 0.05f, 17f), tallPatch = Noise(wx, wz, 0.03f, 61f), flowers = Noise(wx, wz, 0.08f, 33f);
                float centre = Mathf.InverseLerp(10f, 22f, d);   // самая середина — утоптанная, трава реже
                for (int i = 0; i < layers.Length; i++)
                {
                    double r = rng.NextDouble();
                    float chance = kinds[i] == 0 ? (0.25f + patch * 0.35f) * Mathf.Lerp(0.35f, 1f, centre)
                        : kinds[i] == 1 ? Mathf.Clamp01((tallPatch - 0.5f) * 1.6f) * centre * 0.8f
                        : Mathf.Clamp01((flowers - 0.62f) * 2.2f) * 0.6f;
                    if (r < chance) { layers[i][y, x] = 1; total++; }
                }
            }
        for (int i = 0; i < layers.Length; i++) data.SetDetailLayer(0, 0, i, layers[i]);
        terrain.detailObjectDistance = 140f;
        terrain.detailObjectDensity = 1f;
        return total;
    }

    // ---------- горизонт ----------

    static void Horizon()
    {
        // Полосы земли вокруг квадрата Terrain (кольцо), чтобы между краем и горами не было «дыры» к небу.
        Material outer = MakeMaterial(DataDir + "/M_OuterGround.mat", null, 0.05f, false, new Color(0.24f, 0.38f, 0.2f));
        var root = new GameObject("Outer Ground").transform;
        float half = Size / 2f, far = 900f, y = 12f;
        Vector3[] centres = { new Vector3(0f, y, half + (far - half) / 2f), new Vector3(0f, y, -half - (far - half) / 2f),
            new Vector3(half + (far - half) / 2f, y, 0f), new Vector3(-half - (far - half) / 2f, y, 0f) };
        Vector3[] sizes = { new Vector3(far * 2f, 1f, far - half), new Vector3(far * 2f, 1f, far - half),
            new Vector3(far - half, 1f, half * 2f), new Vector3(far - half, 1f, half * 2f) };
        for (int i = 0; i < 4; i++)
        {
            GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "Outer strip " + i;
            Object.DestroyImmediate(strip.GetComponent<Collider>());
            strip.transform.SetParent(root, false);
            strip.transform.position = centres[i] - Vector3.up * 0.5f;
            strip.transform.localScale = sizes[i];
            strip.GetComponent<MeshRenderer>().sharedMaterial = outer;
        }
        // Нижняя половина неба в цвет тумана — без коричневой полосы на горизонте.
        Shader sky = Shader.Find("Skybox/Procedural");
        if (sky != null)
        {
            string path = DataDir + "/M_Sky.mat";
            Material skyMat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (skyMat == null) { skyMat = new Material(sky); AssetDatabase.CreateAsset(skyMat, path); }
            skyMat.SetColor("_GroundColor", RenderSettings.fogColor);
            skyMat.SetColor("_SkyTint", new Color(0.55f, 0.68f, 0.85f));
            skyMat.SetFloat("_AtmosphereThickness", 0.8f);
            EditorUtility.SetDirty(skyMat);
            RenderSettings.skybox = skyMat;
        }
    }

    static void EnvironmentSetup()
    {
        Light sun = Object.FindObjectOfType<Light>();
        if (sun != null)
        {
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.shadows = LightShadows.Soft;
        }
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 150f;
        RenderSettings.fogEndDistance = 520f;
        RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.9f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.7f, 0.9f);
        RenderSettings.ambientEquatorColor = new Color(0.45f, 0.5f, 0.45f);
        RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.22f);
        Horizon();

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 30f, -60f);
            cam.transform.rotation = Quaternion.Euler(22f, 0f, 0f);
            cam.farClipPlane = 1500f;
            cam.clearFlags = CameraClearFlags.Skybox;
            if (cam.GetComponent<FlyCamera>() == null) cam.gameObject.AddComponent<FlyCamera>();
        }
    }

    public static void BuildBatch()
    {
        Build();
        EditorApplication.Exit(0);
    }
}
