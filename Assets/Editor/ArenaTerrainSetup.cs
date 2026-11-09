// 09.11.2026: SampleScene (заводская арена) удалена; эти пакетные методы больше не работают — оставлены для истории.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Подключение Blender-модели арены (Assets/Art/Arena/ArenaFactory.fbx): материал с палитрой, объект в SampleScene.
// Меню Arena → Setup Blender terrain; batch: -executeMethod ArenaTerrainSetup.SetupBatch -quit
public static class ArenaTerrainSetup
{
    const string Dir = "Assets/Art/Arena";
    const string FbxPath = Dir + "/ArenaFactory.fbx";
    const string RootName = "Arena Terrain (Blender)";

    [MenuItem("Arena/Setup Blender terrain")]
    public static void Setup()
    {
        AssetDatabase.Refresh();
        var ti = AssetImporter.GetAtPath(Dir + "/ArenaFactory_Palette.png") as TextureImporter;
        if (ti != null) { ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false; ti.sRGBTexture = true; ti.SaveAndReimport(); }
        string matPath = Dir + "/M_ArenaFactory.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/ArenaFactory_Palette.png"));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.12f);
        EditorUtility.SetDirty(mat);

        var mi = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            if (o is Material em) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), em.name), mat);
        mi.importCameras = false; mi.importLights = false;
        mi.SaveAndReimport();
        AssetDatabase.SaveAssets();

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (model == null) { Debug.LogError("ArenaTerrainSetup: no model " + FbxPath); return; }
        ArenaTerrain existing = Object.FindObjectOfType<ArenaTerrain>(true);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        var holder = new GameObject(RootName);
        holder.AddComponent<ArenaTerrain>();
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, holder.transform);
        inst.transform.localPosition = Vector3.zero;
        EditorSceneManager.MarkSceneDirty(holder.scene);

        Bounds b = new Bounds(); bool first = true;
        foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
        { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/arena-terrain-setup.txt",
            "renderers=" + holder.GetComponentsInChildren<Renderer>().Length + " bounds=" + b.center + " size=" + b.size +
            " rotation=" + inst.transform.localEulerAngles + " scale=" + inst.transform.localScale);
    }

    // Убрать объёмное поле из сцены (модель остаётся в Assets/Art/Arena).
    public static void RemoveBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        ArenaTerrain t = Object.FindObjectOfType<ArenaTerrain>(true);
        if (t != null) { UnityEngine.SceneManagement.Scene sc = t.gameObject.scene; Object.DestroyImmediate(t.gameObject); EditorSceneManager.MarkSceneDirty(sc); }
        EditorSceneManager.SaveOpenScenes();
        EditorApplication.Exit(0);
    }

    public static void SetupBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        Setup();
        EditorSceneManager.SaveOpenScenes();
        EditorApplication.Exit(0);
    }
}
