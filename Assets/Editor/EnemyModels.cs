using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Внешние модели врагов (Codex): материал URP из палитры, подстановка, записи ModelSkinSet. Вызывается из KenneySkinSetup.Create.
public static class EnemyModels
{
    struct Item { public string key, folder, fbx, palette; public float yaw; }

    static readonly Item[] Items =
    {
        It("Enemy_Weak", "CaveImp", "CaveImp_D3-01.fbx", "CaveImp_Palette.png"),
        It("Enemy_Fast", "CaveRunner", "CaveRunner_D3-02.fbx", "CaveRunner_Palette.png"),
        It("Enemy_tank", "Brute", "CaveBrute_D3-03.fbx", "CaveBrute_Palette.png"),
        It("Enemy_Shooter", "Spitter", "CaveSpitter_D3-04.fbx", "CaveSpitter_Palette.png"),
        It("Enemy_Charger", "Charger", "CaveCharger_D3-05.fbx", "CaveCharger_Palette.png"),
        It("Enemy_Bomber", "Bomber", "CaveBomber_D3-06.fbx", "CaveBomber_Palette.png"),
        It("Enemy_Healer", "Healer", "CaveHealer_D3-07.fbx", "CaveHealer_Palette.png"),
        It("Enemy_Splitter", "Splitter", "CaveSplitter_D3-08.fbx", "CaveSplitter_Palette.png"),
        It("Enemy_Summoner", "Broodmother", "CaveBroodmother_D3-09.fbx", "CaveBroodmother_Palette.png"),
        It("Enemy_Sniper", "Sniper", "CaveSniper_D3-10.fbx", "CaveSniper_Palette.png"),
        It("Enemy_Mortar", "Mortar", "CaveMortar_D3-11.fbx", "CaveMortar_Palette.png"),
        It("Enemy_Teleporter", "Blinkbat", "CaveBlinkbat_D3-12.fbx", "CaveBlinkbat_Palette.png"),
        It("Enemy_Buffer", "Amplifier", "CaveAmplifier_D3-13.fbx", "CaveAmplifier_Palette.png"),
        It("Enemy_Armored", "Armored", "CaveArmored_D3-14.fbx", "CaveArmored_Palette.png"),
        It("Boss", "Guardian", "CaveGuardian_D3-15.fbx", "CaveGuardian_Palette.png"),
    };

    static Item It(string key, string folder, string fbx, string palette) { return new Item { key = key, folder = folder, fbx = fbx, palette = palette, yaw = 180f }; }

    public static void Import()
    {
        AssetDatabase.Refresh();
        foreach (Item it in Items)
        {
            string dir = "Assets/Art/Enemies/" + it.folder;
            var ti = AssetImporter.GetAtPath(dir + "/" + it.palette) as TextureImporter;
            if (ti != null) { ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false; ti.SaveAndReimport(); }
            string matPath = dir + "/M_" + it.folder + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + it.palette));
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);
            string fbx = dir + "/" + it.fbx;
            var mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbx))
                if (o is Material em) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), em.name), mat);
            mi.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
    }

    public static void AddEntries(List<ModelSkinSet.Entry> all)
    {
        foreach (Item it in Items)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Enemies/" + it.folder + "/" + it.fbx);
            if (model == null) continue;
            all.RemoveAll(e => e.key == it.key);
            all.Add(new ModelSkinSet.Entry { key = it.key, model = model, eulerOffset = new Vector3(0f, it.yaw, 0f) });
        }
    }

    // Диагностика: иерархия, габариты и направление «носа» (центр самой передней части).
    public static void Inspect()
    {
        Import();
        foreach (Item it in Items)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Enemies/" + it.folder + "/" + it.fbx);
            var sb = new System.Text.StringBuilder("EnemyModels " + it.key + ": ");
            Bounds all = new Bounds(); bool first = true;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                sb.Append(r.name + " m=" + (r.sharedMaterial != null ? r.sharedMaterial.name + (r.sharedMaterial.GetTexture("_BaseMap") != null ? "+tex" : "-notex") : "null") + " c=" + r.bounds.center.ToString("F2") + " s=" + r.bounds.size.ToString("F2") + " mat=" + (r.sharedMaterial != null ? r.sharedMaterial.shader.name : "null") + "; ");
                if (first) { all = r.bounds; first = false; } else all.Encapsulate(r.bounds);
            }
            sb.Append(" TOTAL c=" + all.center.ToString("F2") + " s=" + all.size.ToString("F2"));
            sb.Append(" | transforms: ");
            foreach (Transform t in model.GetComponentsInChildren<Transform>()) sb.Append(t.name + "(" + t.localEulerAngles.ToString("F0") + ") ");
            Debug.Log(sb.ToString());
        }
    }
}

public static class EnemyNameDiag
{
    public static void Run()
    {
        foreach (string t in new[] { "Weak", "Fast", "tank", "Shooter", "Charger", "Bomber", "Healer", "Splitter", "Summoner", "Sniper", "Mortar", "Teleporter", "Buffer", "Armored" })
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy_" + t + ".prefab");
            var go = Object.Instantiate(p);
            Debug.Log("NAMEDIAG file=Enemy_" + t + " prefab.name=" + p.name + " instance=" + go.name + " renderers=" + go.GetComponentsInChildren<Renderer>().Length);
            Object.DestroyImmediate(go);
        }
    }
}

public static class EnemyLineup
{
    // Все 15 моделей в ряд (вид сверху-сбоку, как в игре), снимок docs/art/Enemy_lineup.png.
    public static void Run()
    {
        EnemyModels.Import();
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        var l = new GameObject("L").AddComponent<Light>();
        l.type = LightType.Directional; l.intensity = 1.3f; l.transform.rotation = Quaternion.Euler(50, 150, 0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
        string[] folders = { "CaveImp/CaveImp_D3-01", "CaveRunner/CaveRunner_D3-02", "Brute/CaveBrute_D3-03", "Spitter/CaveSpitter_D3-04", "Charger/CaveCharger_D3-05", "Bomber/CaveBomber_D3-06", "Healer/CaveHealer_D3-07", "Splitter/CaveSplitter_D3-08", "Broodmother/CaveBroodmother_D3-09", "Sniper/CaveSniper_D3-10", "Mortar/CaveMortar_D3-11", "Blinkbat/CaveBlinkbat_D3-12", "Amplifier/CaveAmplifier_D3-13", "Armored/CaveArmored_D3-14", "Guardian/CaveGuardian_D3-15" };
        for (int i = 0; i < folders.Length; i++)
        {
            var m = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Enemies/" + folders[i] + ".fbx");
            var go = (GameObject)Object.Instantiate(m);
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * go.transform.rotation;   // перёд к камере (+Z смотрит на камеру)
            go.transform.position = new Vector3((i % 5) * 2.6f - 5.2f, 0f, -(i / 5) * 2.6f);
        }
        var cam = new GameObject("C").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.2f, 0.22f, 0.26f);
        cam.fieldOfView = 42f;
        cam.transform.position = new Vector3(0f, 9f, 7.5f);
        cam.transform.LookAt(new Vector3(0f, 0.4f, -2.2f));
        var rt = new RenderTexture(1600, 900, 24);
        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
        System.IO.File.WriteAllBytes("docs/art/Enemy_lineup.png", tex.EncodeToPNG());
        Debug.Log("EnemyLineup done");
    }
}
