using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Импорт наборов Kenney (CC0): один URP/Lit материал на набор с colormap и подстановка во все FBX.
public static class KenneyImport
{
    const string Root = "Assets/Art/Kenney";
    static readonly string[] Kits = { "BlasterKit", "SurvivalKit", "ModularSpaceKit" };

    public static void RunAll()
    {
        AssetDatabase.Refresh();
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        int remapped = 0, models = 0;
        foreach (string kit in Kits)
        {
            string texPath = Root + "/" + kit + "/Models/Textures/colormap.png";
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) { Debug.LogWarning("KenneyImport: no colormap for " + kit); continue; }

            string matDir = Root + "/" + kit + "/Materials";
            Directory.CreateDirectory(matDir);
            string matPath = matDir + "/M_" + kit + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(lit);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(mat);

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/" + kit + "/Models" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var mi = AssetImporter.GetAtPath(p) as ModelImporter;
                if (mi == null) continue;
                models++;
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(p))
                {
                    if (o is Material em)
                    {
                        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), em.name), mat);
                        remapped++;
                    }
                }
                mi.SaveAndReimport();
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("KenneyImport: models=" + models + " remapped=" + remapped);
    }
}
