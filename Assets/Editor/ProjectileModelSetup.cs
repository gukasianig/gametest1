using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Материалы для моделей снарядов (Assets/Resources/Projectiles) + отчёт об ориентации. batch: -executeMethod ProjectileModelSetup.Run -quit
public static class ProjectileModelSetup
{
    const string Dir = "Assets/Resources/Projectiles";

    public static void Run()
    {
        string atlasPath = Dir + "/ProjectileAtlas.png";
        if (!File.Exists(atlasPath)) { File.Copy("Assets/Art/ArenaGreen/ArenaGreen_Atlas.png", atlasPath); AssetDatabase.ImportAsset(atlasPath); }
        Shader lit = Shader.Find("Universal Render Pipeline/Lit"), unlit = Shader.Find("Universal Render Pipeline/Unlit");
        Material atlas = Make(Dir + "/M_Projectile.mat", lit, c => { c.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath)); c.SetColor("_BaseColor", Color.white); c.SetFloat("_Smoothness", 0.35f); });
        Material glow = Make(Dir + "/M_ProjGlowOrange.mat", unlit, c => c.SetColor("_BaseColor", new Color(1f, 0.6f, 0.15f)));
        var sb = new StringBuilder();
        foreach (string name in new[] { "bullet", "rocket", "grenade", "ice_shard" })
        {
            string path = Dir + "/" + name + ".fbx";
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) { sb.AppendLine(name + ": no importer"); continue; }
            // Своя палитра рядом с моделью (<name>_Palette.png, например ракета из tools/blender/make_rocket_shot.py) — иначе общий атлас.
            Material body = atlas;
            string palettePath = Dir + "/" + name + "_Palette.png";
            if (File.Exists(palettePath))
            {
                if (AssetImporter.GetAtPath(palettePath) is TextureImporter tex)
                {
                    tex.filterMode = FilterMode.Point;
                    tex.mipmapEnabled = false;
                    tex.textureCompression = TextureImporterCompression.Uncompressed;
                    tex.SaveAndReimport();
                }
                Texture2D palette = AssetDatabase.LoadAssetAtPath<Texture2D>(palettePath);
                body = Make(Dir + "/M_" + name + "_Palette.mat", lit, c => { c.SetTexture("_BaseMap", palette); c.SetColor("_BaseColor", Color.white); c.SetFloat("_Smoothness", 0.2f); });
            }
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(o is Material em)) continue;
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), em.name), em.name.Contains("Glow") ? glow : body);
            }
            imp.SaveAndReimport();
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Mesh mesh = go.GetComponentInChildren<MeshFilter>().sharedMesh;
            Vector3 size = mesh.bounds.size;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            // Нос там, где сечение уже: сравниваем средний радиус вершин у концов длинной оси.
            float min = mesh.bounds.min[axis], max = mesh.bounds.max[axis], len = max - min, rLow = 0f, rHigh = 0f; int nLow = 0, nHigh = 0;
            foreach (Vector3 v in mesh.vertices)
            {
                Vector3 p = v; float r = Mathf.Sqrt(p.sqrMagnitude - p[axis] * p[axis]);
                if (p[axis] < min + len * 0.2f) { rLow += r; nLow++; } else if (p[axis] > max - len * 0.2f) { rHigh += r; nHigh++; }
            }
            sb.AppendLine(name + ": rootRot=" + go.transform.eulerAngles + " rootScale=" + go.transform.localScale + " meshSize=" + size.ToString("0.000") + " longAxis=" + "XYZ"[axis] +
                " avgRadius low=" + (rLow / Mathf.Max(1, nLow)).ToString("0.000") + " high=" + (rHigh / Mathf.Max(1, nHigh)).ToString("0.000") + " (нос там, где радиус меньше)");
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/projectile-models.txt", sb.ToString());
        EditorApplication.Exit(0);
    }

    static Material Make(string path, Shader shader, System.Action<Material> setup)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        setup(m);
        EditorUtility.SetDirty(m);
        return m;
    }
}
