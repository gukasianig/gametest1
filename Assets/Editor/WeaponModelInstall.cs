using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Подключает авторские модели оружия из Blender (docs/art/Weapons/<Имя>/) вместо моделей Kenney:
// копирует FBX и палитру в Assets/Art/Weapons/<Имя>/, делает URP-материал (палитра; части *Glow* светятся),
// записывает модель в Resources/ModelSkinSet (ключ Weapon0..3 / Kind_*), ставит иконки из docs/art/Weapons/Icons
// в Resources/UI/Weapons (.meta и GUID иконок сохраняются). Повторный запуск безопасен.
// Меню Tools > Install Blender Weapon Models; batch: -executeMethod WeaponModelInstall.RunBatch -quit
public static class WeaponModelInstall
{
    const string Source = "docs/art/Weapons";
    const string Target = "Assets/Art/Weapons";
    const string IconTarget = "Assets/Resources/UI/Weapons";
    const string Report = "Logs/weapon-model-install.txt";

    static readonly string[,] Map =
    {
        { "Weapon0", "Pistol" }, { "Weapon1", "Rifle" }, { "Weapon2", "Shotgun" }, { "Weapon3", "HeavyRifle" },
        { "Kind_Boomerang", "SawLauncher" }, { "Kind_Rocket", "RocketLauncher" }, { "Kind_Arc", "ArcBlaster" },
        { "Kind_Flame", "Flamethrower" }, { "Kind_Ice", "IceCannon" }, { "Kind_Rail", "Railgun" },
    };

    [MenuItem("Tools/Install Blender Weapon Models")]
    public static void Install()
    {
        var log = new StringBuilder();
        Directory.CreateDirectory(Target);
        for (int i = 0; i < Map.GetLength(0); i++)
        {
            string name = Map[i, 1], folder = Target + "/" + name;
            Directory.CreateDirectory(folder);
            File.Copy(Source + "/" + name + "/" + name + ".fbx", folder + "/" + name + ".fbx", true);
            File.Copy(Source + "/" + name + "/" + name + "_Palette.png", folder + "/" + name + "_Palette.png", true);
        }
        int icons = 0;
        foreach (string icon in Directory.GetFiles(Source + "/Icons", "*.png"))
        {
            string file = Path.GetFileName(icon);
            if (file == "icons_sheet.png") continue;
            File.Copy(icon, IconTarget + "/" + file, true);
            icons++;
        }
        AssetDatabase.Refresh();
        foreach (string icon in Directory.GetFiles(IconTarget, "*.png"))
            AssetDatabase.ImportAsset(icon.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);

        ModelSkinSet set = Resources.Load<ModelSkinSet>("ModelSkinSet");
        if (set == null) throw new System.Exception("ModelSkinSet not found");
        for (int i = 0; i < Map.GetLength(0); i++)
        {
            string key = Map[i, 0], name = Map[i, 1], folder = Target + "/" + name;
            string fbx = folder + "/" + name + ".fbx", palette = folder + "/" + name + "_Palette.png";
            var texture = (TextureImporter)AssetImporter.GetAtPath(palette);
            texture.filterMode = FilterMode.Point;
            texture.mipmapEnabled = false;
            texture.sRGBTexture = true;
            texture.textureCompression = TextureImporterCompression.Uncompressed;
            texture.SaveAndReimport();
            Texture2D map = AssetDatabase.LoadAssetAtPath<Texture2D>(palette);
            Material body = MakeMaterial(folder + "/M_" + name + ".mat", map, false);
            Material glow = MakeMaterial(folder + "/M_" + name + "_Glow.mat", map, true);

            var model = (ModelImporter)AssetImporter.GetAtPath(fbx);
            model.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            model.importAnimation = false;
            model.animationType = ModelImporterAnimationType.None;
            // У корня FBX из Blender свой поворот (90° по X): с ним дуло смотрит в +Z, верх оружия — вверх.
            // ModelSkins задаёт localRotation = eulerOffset, поэтому в запись идёт именно этот поворот (у Kenney было 180° по Y).
            model.bakeAxisConversion = false;
            model.SaveAndReimport();
            var remapped = new List<string>();
            foreach (Object part in AssetDatabase.LoadAllAssetsAtPath(fbx))
                if (part is Material embedded)
                {
                    bool shines = embedded.name.ToLowerInvariant().Contains("glow");
                    model.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), shines ? glow : body);
                    remapped.Add(embedded.name + (shines ? "→glow" : ""));
                }
            model.SaveAndReimport();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            ModelSkinSet.Entry entry = null;
            foreach (ModelSkinSet.Entry e in set.entries) if (e != null && e.key == key) entry = e;
            Vector3 oldSize = entry != null && entry.model != null ? Size(entry.model) * entry.sizeMultiplier : Vector3.zero;
            if (entry == null)
            {
                entry = new ModelSkinSet.Entry { key = key, eulerOffset = new Vector3(0f, 180f, 0f), centerOnHand = true };
                var list = new List<ModelSkinSet.Entry>(set.entries) { entry };
                set.entries = list.ToArray();
            }
            entry.model = prefab;
            entry.eulerOffset = prefab.transform.localEulerAngles;
            entry.sizeMultiplier = 1f;   // модели из Blender в метрах (пистолет 0,39 м … тяжёлая винтовка 1,13 м)
            Vector3 newSize = Size(prefab);
            log.AppendLine(key + " ← " + fbx + "  материалы: " + string.Join(", ", remapped) +
                "  размер был " + oldSize.ToString("0.00") + " стал " + newSize.ToString("0.00"));
        }
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        log.AppendLine("Иконок скопировано: " + icons);
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Report, log.ToString());
        Debug.Log("WeaponModelInstall:\n" + log);
    }

    // Диагностика: дочерние объекты (маркеры Muzzle/GripPoint) и поворот корня каждой модели.
    public static void DescribeBatch()
    {
        var log = new StringBuilder();
        for (int i = 0; i < Map.GetLength(0); i++)
        {
            string fbx = Target + "/" + Map[i, 1] + "/" + Map[i, 1] + ".fbx";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            log.AppendLine(Map[i, 1] + " root rot " + prefab.transform.localEulerAngles + " size " + Size(prefab).ToString("0.00"));
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                if (t != prefab.transform && (t.name.Contains("Muzzle") || t.name.Contains("Grip") || t.parent == prefab.transform))
                    log.AppendLine("   " + t.name + " pos " + prefab.transform.InverseTransformPoint(t.position).ToString("0.00") +
                        " rot " + t.localEulerAngles.ToString("0"));
        }
        File.WriteAllText("Logs/weapon-model-describe.txt", log.ToString());
        EditorApplication.Exit(0);
    }

    public static void RunBatch()
    {
        Install();
        EditorApplication.Exit(0);
    }

    static Material MakeMaterial(string path, Texture2D map, bool glow)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", map);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.25f);
        if (glow)
        {
            material.EnableKeyword("_EMISSION");
            material.SetTexture("_EmissionMap", map);
            material.SetColor("_EmissionColor", Color.white * 1.6f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    static Vector3 Size(GameObject prefab)
    {
        var bounds = new Bounds();
        bool any = false;
        foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Matrix4x4 m = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Bounds b = filter.sharedMesh.bounds;
            foreach (Vector3 corner in new[] { b.min, b.max, new Vector3(b.min.x, b.min.y, b.max.z), new Vector3(b.max.x, b.max.y, b.min.z) })
            {
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                else bounds.Encapsulate(p);
            }
        }
        return bounds.size;
    }
}
