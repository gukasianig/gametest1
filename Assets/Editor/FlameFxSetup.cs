using System.IO;
using UnityEditor;
using UnityEngine;

// Ассеты струи огнемёта (FlameJet): мягкая круглая текстура и два материала частиц URP —
// аддитивный огонь и полупрозрачный дым. Лежат в Resources/Fx, поэтому шейдер частиц попадает в билд.
// Меню Tools > Build Flame FX assets; batch: -executeMethod FlameFxSetup.RunBatch -quit
public static class FlameFxSetup
{
    const string Folder = "Assets/Resources/Fx";

    [MenuItem("Tools/Build Flame FX assets")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        string texturePath = Folder + "/flame_soft.png";
        if (!File.Exists(texturePath))
        {
            const int N = 64;
            var texture = new Texture2D(N, N, TextureFormat.RGBA32, false);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N * 0.5f, N * 0.5f)) / (N * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);   // мягкий край
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        Texture2D soft = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        Material(Folder + "/FlameAdd.mat", soft, true);
        Material(Folder + "/FlameSmoke.mat", soft, false);
        AssetDatabase.SaveAssets();
    }

    public static void RunBatch()
    {
        Build();
        EditorApplication.Exit(0);
    }

    static void Material(string path, Texture2D texture, bool additive)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", additive ? 2f : 0f);
        material.SetFloat("_SrcBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.SrcAlpha : UnityEngine.Rendering.BlendMode.SrcAlpha));
        material.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
    }
}
