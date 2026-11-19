using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Imports only the three new branch models; leaves existing model entries and GUIDs intact.
public static class RifleBranchAssets
{
    [MenuItem("Tools/Install Rifle Branch Assets")]
    public static void Install()
    {
        AssetDatabase.Refresh();
        ModelSkinSet set = Resources.Load<ModelSkinSet>("ModelSkinSet");
        if (set == null) throw new Exception("ModelSkinSet missing");
        ModelSkinSet.Entry basis = set.Find("Weapon1");
        foreach (string branch in new[] { "Assault", "Minigun", "Heavy" })
        {
            string name = "Rifle" + branch;
            string folder = "Assets/Art/Weapons/" + name;
            string fbx = folder + "/" + name + ".fbx";
            string palette = folder + "/" + name + "_Palette.png";
            if (!File.Exists(fbx) || !File.Exists(palette)) throw new Exception("Generate rifle assets first: " + name);
            var texture = (TextureImporter)AssetImporter.GetAtPath(palette);
            texture.textureType = TextureImporterType.Default;
            texture.filterMode = FilterMode.Point;
            texture.mipmapEnabled = false;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.textureCompression = TextureImporterCompression.Uncompressed;
            texture.SaveAndReimport();
            Texture2D map = AssetDatabase.LoadAssetAtPath<Texture2D>(palette);
            Material body = MaterialAt(folder + "/M_" + name + ".mat", map);
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.bakeAxisConversion = false;
            importer.SaveAndReimport();
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(fbx))
                if (asset is Material embedded)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), body);
            importer.SaveAndReimport();
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            string key = "Rifle_" + branch;
            ModelSkinSet.Entry entry = set.Find(key);
            if (entry == null)
            {
                entry = new ModelSkinSet.Entry { key = key };
                var entries = new List<ModelSkinSet.Entry>(set.entries) { entry };
                set.entries = entries.ToArray();
            }
            entry.model = model;
            entry.eulerOffset = (Quaternion.Euler(0f, 180f, 0f) * model.transform.localRotation).eulerAngles;
            entry.sizeMultiplier = 1f;
            entry.centerOnHand = basis == null || basis.centerOnHand;
            entry.localPosition = basis != null ? basis.localPosition : Vector3.zero;
            entry.muzzleOffset = Vector3.zero;
            ImportIcon("Assets/Resources/UI/Tree/el_" + branch.ToLowerInvariant() + ".png", false);
            ImportIcon("Assets/Resources/UI/Weapons/" + key + ".png", true);
        }
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
    }

    private static Material MaterialAt(string path, Texture2D map)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", map);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Metallic", .15f);
        material.SetFloat("_Smoothness", .25f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ImportIcon(string path, bool sprite)
    {
        var texture = (TextureImporter)AssetImporter.GetAtPath(path);
        if (texture == null) throw new Exception("Missing branch icon: " + path);
        texture.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
        if (sprite) texture.spriteImportMode = SpriteImportMode.Single;
        texture.alphaSource = TextureImporterAlphaSource.FromInput;
        texture.alphaIsTransparency = true;
        texture.mipmapEnabled = false;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.textureCompression = TextureImporterCompression.Uncompressed;
        texture.maxTextureSize = 512;
        texture.SaveAndReimport();
    }
}
