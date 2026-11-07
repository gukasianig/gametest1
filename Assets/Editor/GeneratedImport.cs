using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Импорт моделей, сгенерированных в Blender (tools/blender): палитра, материал URP, подстановка материала, контрольные кадры.
public static class GeneratedImport
{
    const string Root = "Assets/Art/Generated";

    public static void RunAll()
    {
        AssetDatabase.Refresh();
        string texPath = Root + "/Palette.png";
        var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (ti != null)
        {
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.sRGBTexture = true;
            ti.SaveAndReimport();
        }
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        string matPath = Root + "/M_Palette.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.SetTexture("_BaseMap", tex);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(mat);

        int models = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            var mi = AssetImporter.GetAtPath(p) as ModelImporter;
            if (mi == null) continue;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(p))
                if (o is Material em) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), em.name), mat);
            mi.SaveAndReimport();
            models++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("GeneratedImport: models=" + models);
    }

    // Модель спереди и сбоку на тёмном фоне. Камера стоит на +Z и смотрит на -Z: видно то, куда модель «смотрит» (+Z).
    public static void Preview()
    {
        RunAll();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var l = new GameObject("L").AddComponent<Light>();
        l.type = LightType.Directional; l.intensity = 1.2f; l.transform.rotation = Quaternion.Euler(50, 150, 0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
        var cam = new GameObject("C").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.25f, 0.27f, 0.3f);
        cam.fieldOfView = 30f;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Enemy_Weak.fbx");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        Debug.Log("GeneratedImport: size=" + b.size.ToString("F2") + " center=" + b.center.ToString("F2"));
        cam.transform.position = b.center + new Vector3(2.5f, 1.6f, 4.5f);
        cam.transform.LookAt(b.center);
        Directory.CreateDirectory("docs/art");
        var rt = new RenderTexture(800, 800, 24);
        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(800, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 800, 800), 0, 0); tex.Apply();
        File.WriteAllBytes("docs/art/Generated_Enemy_Weak.png", tex.EncodeToPNG());
        Debug.Log("GeneratedImport: preview done");
    }
}
