using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Отдельная сцена для проб рельефа и объектов: Assets/Scenes/ReliefTest.unity.
// Меню Arena → Create Relief test scene; batch: -executeMethod ReliefTestSceneSetup.CreateBatch -quit
// В сцене нет игры: Unity Terrain 200×200 с холмами (лепится кистями) и все FBX из Assets/Art/Packs, расставленные рядами.
public static class ReliefTestSceneSetup
{
    const string Dir = "Assets/Art/Relief";
    const string ScenePath = "Assets/Scenes/ReliefTest.unity";
    const float Size = 200f, Height = 30f;

    [MenuItem("Arena/Create Relief test scene")]
    public static void Create()
    {
        Directory.CreateDirectory(Dir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        TerrainLayer layer = MakeLayer();
        TerrainData data = new TerrainData();
        AssetDatabase.CreateAsset(data, AssetDatabase.GenerateUniqueAssetPath(Dir + "/ReliefTestData.asset"));
        data.heightmapResolution = 257;
        data.size = new Vector3(Size, Height, Size);
        data.terrainLayers = new[] { layer };
        float[,] heights = new float[257, 257];
        for (int y = 0; y < 257; y++)
            for (int x = 0; x < 257; x++)
                heights[y, x] = 0.04f + Mathf.PerlinNoise(x * 0.025f + 7f, y * 0.025f + 7f) * 0.12f +
                                Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.02f;
        data.SetHeights(0, 0, heights);
        int a = data.alphamapResolution;
        float[,,] alpha = new float[a, a, 1];
        for (int y = 0; y < a; y++) for (int x = 0; x < a; x++) alpha[y, x, 0] = 1f;
        data.SetAlphamaps(0, 0, alpha);

        GameObject go = Terrain.CreateTerrainGameObject(data);
        go.name = "Relief Terrain";
        go.transform.position = new Vector3(-Size / 2f, 0f, -Size / 2f);
        Terrain terrain = go.GetComponent<Terrain>();
        Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (shader != null) terrain.materialTemplate = new Material(shader);

        // Все модели из Assets/Art/Packs, ряд за рядом, на высоте рельефа.
        List<GameObject> models = new List<GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art/Packs" }))
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (model != null) models.Add(model);
        }
        GameObject parent = new GameObject("Test Objects");
        // Паки (деревья, лес, космос) — один FBX с десятками объектов: раскладываем каждый вложенный объект отдельно.
        List<Transform> items = new List<Transform>();
        foreach (GameObject model in models)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent.transform);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            List<Transform> parts = new List<Transform>();
            foreach (Transform child in instance.transform)
                if (child.GetComponentInChildren<Renderer>() != null) parts.Add(child);
            if (parts.Count > 1)
            {
                foreach (Transform part in parts) { part.SetParent(parent.transform, true); items.Add(part); }
                Object.DestroyImmediate(instance);
            }
            else items.Add(instance.transform);
        }
        int perRow = 10;
        for (int i = 0; i < items.Count; i++)
        {
            float x = -85f + (i % perRow) * 18f;
            float z = -85f + (i / perRow) * 18f;
            Transform item = items[i];
            item.rotation = Quaternion.identity;
            item.position = new Vector3(x, 0f, z);
            Bounds b = new Bounds(item.position, Vector3.zero);
            bool first = true;
            foreach (Renderer r in item.GetComponentsInChildren<Renderer>())
            { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            float ground = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
            item.position += new Vector3(x - b.center.x, ground - b.min.y, z - b.center.z);
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 25f, -80f);
            cam.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            cam.farClipPlane = 800f;
            if (cam.GetComponent<FlyCamera>() == null) cam.gameObject.AddComponent<FlyCamera>();
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/relief-test.txt", "ok files=" + models.Count + " objects=" + items.Count);
    }

    static TerrainLayer MakeLayer()
    {
        string png = Dir + "/Ground.png";
        if (!File.Exists(png))
        {
            Texture2D tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float v = 0.3f + Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.15f;
                    tex.SetPixel(x, y, new Color(v * 0.8f, v * 1.1f, v * 0.7f));
                }
            File.WriteAllBytes(png, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(png);
        }
        string path = Dir + "/Ground.terrainlayer";
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
        layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        layer.tileSize = new Vector2(10f, 10f);
        EditorUtility.SetDirty(layer);
        return layer;
    }

    public static void CreateBatch()
    {
        Create();
        EditorApplication.Exit(0);
    }
}
