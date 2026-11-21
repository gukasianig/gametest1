using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Подключает модели мировых предметов из Blender (docs/art/Items/<Имя>/, tools/blender/make_items.py) вместо примитивов:
// копирует FBX и палитру в Assets/Art/Items/<Имя>/, делает URP-материалы (палитра; материал «… Glow» светится)
// и записывает модель в Resources/ModelSkinSet под ключом, по которому её ищет код (ModelSkins.ApplyEnemy/ApplySpecial).
// Повторный запуск безопасен. Меню Tools > Install Blender Item Models; batch: -executeMethod ItemModelInstall.RunBatch
public static class ItemModelInstall
{
    const string Source = "docs/art/Items";
    const string Target = "Assets/Art/Items";
    const string Report = "Logs/item-model-install.txt";

    // ключ ModelSkinSet, папка/имя модели, множитель размера (модель вписывается в габарит исходного примитива),
    // поворот по Y: 180 — как у врагов из Blender (+Y Blender смотрит вперёд по полёту снаряда)
    static readonly (string key, string name, float size, float yaw)[] Map =
    {
        ("XP Crystal", "Crystal", 1.15f, 0f), ("Repair kit", "RepairKit", 1f, 0f), ("Magnet", "Magnet", 1f, 0f),
        // Дрон вписывается в сферу 0,5 м (винты-примитивы создаются позже), поэтому ×2,4 — как прежний размах с винтами.
        ("Mine", "Mine", 1f, 0f), ("Turret", "Turret", 1.75f, 0f), ("Drone", "Drone", 2.4f, 0f), ("Saw", "SawBlade", 1f, 0f),
        ("Small chest", "SmallChest", 1f, 0f),
        // Вторая партия (11.11.2026): сундук события, ящик сброса, короны элит, снаряды врагов.
        ("Guard chest", "GuardChest", 1f, 0f), ("Supply crate", "SupplyCrate", 1f, 0f),
        ("Elite crown", "EliteCrown", 1.3f, 0f), ("Elite crown explosive", "EliteCrownExplosive", 1.3f, 0f),
        ("Acid shot", "AcidShot", 1.1f, 180f), ("Bone quill", "BoneQuill", 1f, 180f), ("Hostile orb", "HostileOrb", 1.15f, 0f),
        ("Mortar spore", "MortarSpore", 1f, 0f),
        // Финальные боссы (12.11.2026): вписываются в сферу Стража 3,2 м; королева ~4,8 м в длину, снайпер ~4,3 м.
        ("Swarm queen", "SwarmQueen", 1.5f, 180f), ("Storm sniper", "StormSniper", 1.35f, 180f),
    };

    [MenuItem("Tools/Install Blender Item Models")]
    public static void Install()
    {
        var log = new StringBuilder();
        Directory.CreateDirectory(Target);
        foreach (var item in Map)
        {
            string folder = Target + "/" + item.name;
            Directory.CreateDirectory(folder);
            File.Copy(Source + "/" + item.name + "/" + item.name + ".fbx", folder + "/" + item.name + ".fbx", true);
            File.Copy(Source + "/" + item.name + "/" + item.name + "_Palette.png", folder + "/" + item.name + "_Palette.png", true);
        }
        AssetDatabase.Refresh();

        ModelSkinSet set = Resources.Load<ModelSkinSet>("ModelSkinSet");
        if (set == null) throw new System.Exception("ModelSkinSet not found");
        foreach (var item in Map)
        {
            string folder = Target + "/" + item.name;
            string fbx = folder + "/" + item.name + ".fbx", palette = folder + "/" + item.name + "_Palette.png";
            var texture = (TextureImporter)AssetImporter.GetAtPath(palette);
            texture.filterMode = FilterMode.Point;   // как у оружия: ячейки палитры не смешиваются
            texture.mipmapEnabled = false;
            texture.sRGBTexture = true;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.textureCompression = TextureImporterCompression.Uncompressed;
            texture.SaveAndReimport();
            Texture2D map = AssetDatabase.LoadAssetAtPath<Texture2D>(palette);
            Material body = MakeMaterial(folder + "/M_" + item.name + ".mat", map, false);
            Material glow = MakeMaterial(folder + "/M_" + item.name + "_Glow.mat", map, true);

            var model = (ModelImporter)AssetImporter.GetAtPath(fbx);
            model.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            model.importAnimation = false;
            model.animationType = ModelImporterAnimationType.None;
            model.bakeAxisConversion = false;   // ModelSkins.ApplyEnemy сохраняет поворот корня FBX
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
            foreach (ModelSkinSet.Entry e in set.entries) if (e != null && e.key == item.key) entry = e;
            if (entry == null)
            {
                entry = new ModelSkinSet.Entry { key = item.key };
                var list = new List<ModelSkinSet.Entry>(set.entries) { entry };
                set.entries = list.ToArray();
            }
            entry.model = prefab;
            entry.eulerOffset = new Vector3(0f, item.yaw, 0f);
            entry.sizeMultiplier = item.size;
            var children = new List<string>();
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true)) if (t != prefab.transform) children.Add(t.name);
            log.AppendLine(item.key + " ← " + fbx + "  материалы: " + string.Join(", ", remapped) + "  части: " + string.Join(", ", children));
        }
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Report, log.ToString());
        Debug.Log("ItemModelInstall:\n" + log);
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
}
